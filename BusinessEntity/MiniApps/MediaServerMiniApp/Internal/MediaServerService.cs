using System.Text.Json;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Connectors;
using BusinessEntity.MiniApps.MediaServerMiniApp.Contracts;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Connectors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace BusinessEntity.MiniApps.MediaServerMiniApp.Internal;

// Реальная логика общего мультимедиа-хранилища.
public sealed class MediaServerService : IMediaServerService
{
    private const long MaxVideoBytes = 2L * 1024L * 1024L * 1024L;
    private const string MetadataFileName = "metadata.json";

    private readonly IDataProviderConnector _dataProviderConnector;
    private readonly IBusinessEntityFactory _businessEntityFactory;
    private readonly IUserConnector? _userConnector;
    private readonly string _storageRoot;

    public MediaServerService(
        IDataProviderConnector dataProviderConnector,
        IBusinessEntityFactory businessEntityFactory,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        IUserConnector? userConnector = null)
    {
        _dataProviderConnector = dataProviderConnector;
        _businessEntityFactory = businessEntityFactory;
        _userConnector = userConnector;

        var contentRoot = environment.ContentRootPath ?? AppContext.BaseDirectory;
        var configuredRoot = configuration["Storage:RootPath"];
        var storageRoot = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(contentRoot, "App_Data", "RichDocumentData")
            : configuredRoot.Trim();

        _storageRoot = Path.GetFullPath(
            Path.IsPathRooted(storageRoot)
                ? storageRoot
                : Path.Combine(contentRoot, storageRoot));
    }

    public void EnsureStorageInitialized()
    {
        Directory.CreateDirectory(_storageRoot);
    }

    public async Task<IReadOnlyList<MediaVideoInfo>> GetVideosAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dataProviderConnector.GetAllAsync(cancellationToken);
        var result = new List<MediaVideoInfo>();

        foreach (var entity in entities
                     .Where(x => x.EntityType == BusinessEntityTypeEnum.MediaVideo)
                     .OrderByDescending(x => x.CreatedDate))
        {
            var data = await _dataProviderConnector.GetDataAsync<MediaVideo>(entity.Id, cancellationToken);
            if (data == null)
            {
                continue;
            }

            result.Add(ToInfo(entity.Id, data));
        }

        return result;
    }

    public async Task<MediaVideoInfo?> GetVideoAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        var data = await _dataProviderConnector.GetDataAsync<MediaVideo>(videoId, cancellationToken);
        return data == null ? null : ToInfo(videoId, data);
    }

    public async Task<MediaVideoInfo> UploadVideoAsync(
        Stream content,
        string fileName,
        string? contentType,
        long? length,
        CancellationToken cancellationToken = default)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        var safeFileName = SanitizeFileName(Path.GetFileName(fileName));
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = "video";
        }

        var normalizedContentType = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();

        if (!IsSupportedVideo(safeFileName, normalizedContentType))
        {
            throw new InvalidOperationException("Переданный файл не похож на поддерживаемое видео.");
        }

        if (length.HasValue && length.Value > MaxVideoBytes)
        {
            throw new InvalidOperationException("Видео слишком большое для загрузки.");
        }

        var displayName = Path.GetFileNameWithoutExtension(safeFileName);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = safeFileName;
        }

        var typedEntity = _businessEntityFactory.Create<MediaVideo>(
            BusinessEntityTypeEnum.MediaVideo,
            displayName);
        typedEntity.Name = displayName;
        typedEntity.BusinessEntityType = BusinessEntityTypeEnum.MediaVideo;
        typedEntity.EntityType = BusinessEntityTypeEnum.MediaVideo;

        var extension = ResolveVideoExtension(safeFileName, normalizedContentType);
        var relativePath = BuildStorageRelativePath(typedEntity.Id, extension);
        var physicalPath = ResolvePhysicalPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

        var savedEntity = await _dataProviderConnector.AddAsync(typedEntity, cancellationToken);
        try
        {
            await using (var output = new FileStream(
                             physicalPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 1024 * 128,
                             useAsync: true))
            {
                await content.CopyToAsync(output, cancellationToken);
            }

            var fileInfo = new FileInfo(physicalPath);
            var userId = await ResolveCurrentUserIdAsync(cancellationToken);
            var data = new MediaVideo
            {
                Id = savedEntity.Id,
                Name = displayName,
                Tag = BusinessEntityTypeEnum.MediaVideo.ToString(),
                FileName = safeFileName,
                DisplayName = displayName,
                ContentType = normalizedContentType,
                OriginalSizeBytes = fileInfo.Length,
                DurationSeconds = null,
                UploadedByUserId = userId,
                UploadedDate = DateTime.UtcNow,
                StorageRelativePath = NormalizeRelativePath(relativePath),
                EmbedUrl = BuildEmbedUrl(savedEntity.Id)
            };

            await _dataProviderConnector.UpdateDataAsync(savedEntity.Id, data, cancellationToken);
            await WriteMetadataAsync(physicalPath, data, cancellationToken);
            return ToInfo(savedEntity.Id, data);
        }
        catch
        {
            DeleteEntityStorageFolder(savedEntity.Id);
            await _dataProviderConnector.DeleteAsync(savedEntity.Id, cancellationToken);
            throw;
        }
    }

    public async Task<MediaVideoInfo> RenameVideoAsync(Guid videoId, string displayName, CancellationToken cancellationToken = default)
    {
        var trimmed = (displayName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Отображаемое имя видео не должно быть пустым.");
        }

        var entity = await _dataProviderConnector.GetByIdAsync(videoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Видео '{videoId}' не найдено.");
        var data = await _dataProviderConnector.GetDataAsync<MediaVideo>(videoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Payload видео '{videoId}' не найден.");

        entity.Name = trimmed;
        entity.LastModifiedDate = DateTime.UtcNow;
        data.Name = trimmed;
        data.DisplayName = trimmed;

        await _dataProviderConnector.UpdateAsync(entity, cancellationToken);
        await _dataProviderConnector.UpdateDataAsync(videoId, data, cancellationToken);
        await WriteMetadataAsync(ResolvePhysicalPath(data.StorageRelativePath), data, cancellationToken);
        return ToInfo(videoId, data);
    }

    public async Task DeleteVideoAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        DeleteEntityStorageFolder(videoId);
        await _dataProviderConnector.DeleteAsync(videoId, cancellationToken);
    }

    public async Task<MediaVideoFileContent?> GetVideoFileAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        var data = await _dataProviderConnector.GetDataAsync<MediaVideo>(videoId, cancellationToken);
        if (data == null || string.IsNullOrWhiteSpace(data.StorageRelativePath))
        {
            return null;
        }

        var physicalPath = ResolvePhysicalPath(data.StorageRelativePath);
        if (!File.Exists(physicalPath))
        {
            return null;
        }

        return new MediaVideoFileContent
        {
            PhysicalPath = physicalPath,
            FileName = string.IsNullOrWhiteSpace(data.FileName) ? Path.GetFileName(physicalPath) : data.FileName,
            ContentType = string.IsNullOrWhiteSpace(data.ContentType) ? "application/octet-stream" : data.ContentType
        };
    }

    private async Task<Guid?> ResolveCurrentUserIdAsync(CancellationToken cancellationToken)
    {
        if (_userConnector == null)
        {
            return null;
        }

        try
        {
            var user = await _userConnector.EnsureCurrentUserAsync(cancellationToken);
            return user?.Id;
        }
        catch
        {
            return null;
        }
    }

    private static MediaVideoInfo ToInfo(Guid id, MediaVideo data)
    {
        return new MediaVideoInfo
        {
            Id = id,
            FileName = data.FileName ?? string.Empty,
            DisplayName = string.IsNullOrWhiteSpace(data.DisplayName)
                ? data.FileName ?? string.Empty
                : data.DisplayName,
            ContentType = string.IsNullOrWhiteSpace(data.ContentType) ? "application/octet-stream" : data.ContentType,
            OriginalSizeBytes = data.OriginalSizeBytes,
            DurationSeconds = data.DurationSeconds,
            UploadedByUserId = data.UploadedByUserId,
            UploadedDate = data.UploadedDate,
            EmbedUrl = string.IsNullOrWhiteSpace(data.EmbedUrl) ? BuildEmbedUrl(id) : data.EmbedUrl
        };
    }

    private static string BuildEmbedUrl(Guid videoId)
    {
        return $"/media-server-files/videos/{videoId:D}/original";
    }

    private static string BuildStorageRelativePath(Guid videoId, string extension)
    {
        return Path.Combine("business-entities", videoId.ToString("D"), "videos", $"original{extension}");
    }

    private string ResolvePhysicalPath(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, normalized));
        var storageRootWithSeparator = _storageRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!string.Equals(fullPath, _storageRoot, StringComparison.OrdinalIgnoreCase) &&
            !fullPath.StartsWith(storageRootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Storage path is outside configured root.");
        }

        return fullPath;
    }

    private void DeleteEntityStorageFolder(Guid videoId)
    {
        var folder = Path.Combine(_storageRoot, "business-entities", videoId.ToString("D"));
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task WriteMetadataAsync(string physicalPath, MediaVideo data, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(physicalPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        var json = JsonSerializer.Serialize(
            new
            {
                data.FileName,
                data.DisplayName,
                data.ContentType,
                data.OriginalSizeBytes,
                data.DurationSeconds,
                data.UploadedByUserId,
                data.UploadedDate,
                data.StorageRelativePath,
                data.EmbedUrl
            },
            new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(Path.Combine(directory, MetadataFileName), json, cancellationToken);
    }

    private static bool IsSupportedVideo(string fileName, string contentType)
    {
        if ((contentType ?? string.Empty).StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".mp4" or ".webm" or ".ogv" or ".ogg" or ".mov" or ".m4v" => true,
            _ => false
        };
    }

    private static string ResolveVideoExtension(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.ToLowerInvariant();
        }

        return (contentType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "video/mp4" => ".mp4",
            "video/webm" => ".webm",
            "video/ogg" => ".ogv",
            "video/quicktime" => ".mov",
            "video/x-m4v" => ".m4v",
            _ => ".video"
        };
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' })
            .ToHashSet();

        var sanitized = new string((value ?? string.Empty)
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray());

        return sanitized.Trim();
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return (relativePath ?? string.Empty)
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
