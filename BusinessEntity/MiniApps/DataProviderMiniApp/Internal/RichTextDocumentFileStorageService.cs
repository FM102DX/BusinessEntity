using System.Text.Json;
using BusinessEntity.Core.RichText;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal;

// Внутренний файловый storage rich-text документа.
// Хранит embedded-изображения документа вне графовой модели BusinessEntity.
internal sealed class RichTextDocumentFileStorageService
{
    private const string MetadataFileName = "metadata.json";
    private readonly string _storageRoot;

    // Вычисляет root-папку storage из конфигурации или относительно content-root приложения.
    public RichTextDocumentFileStorageService(IWebHostEnvironment environment, IConfiguration configuration)
    {
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

    // Полностью заменяет embedded-файлы документа новым набором.
    public async Task SaveFilesAsync(
        Guid businessEntityId,
        IReadOnlyList<RichTextEmbeddedFile> files,
        bool replaceExistingFiles,
        CancellationToken cancellationToken = default)
    {
        if (replaceExistingFiles)
        {
            DeleteDocumentFolder(businessEntityId);
        }

        if (files == null || files.Count == 0)
        {
            return;
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var imageDirectory = GetImageDirectory(businessEntityId, file.ImageId);
            Directory.CreateDirectory(imageDirectory);

            DeleteVariantFiles(imageDirectory, file.Variant);

            var storedFileName = BuildStoredFileName(file.Variant, file.FileName, file.ContentType);
            var contentPath = Path.Combine(imageDirectory, storedFileName);
            var metadataPath = Path.Combine(imageDirectory, MetadataFileName);

            await File.WriteAllBytesAsync(contentPath, file.Content ?? Array.Empty<byte>(), cancellationToken);

            var metadataJson = JsonSerializer.Serialize(
                new FileMetadata
                {
                    FileName = file.FileName ?? string.Empty,
                    ContentType = file.ContentType ?? "application/octet-stream",
                    StoredFileName = storedFileName
                },
                StorageJsonOptions.Default);

            await File.WriteAllTextAsync(metadataPath, metadataJson, cancellationToken);
        }
    }

    // Читает embedded-файл документа по imageId и variant.
    public async Task<RichTextEmbeddedFileContent?> GetFileAsync(
        Guid businessEntityId,
        string imageId,
        string variant,
        CancellationToken cancellationToken = default)
    {
        var imageDirectory = GetImageDirectory(businessEntityId, imageId);
        var metadataPath = Path.Combine(imageDirectory, MetadataFileName);

        if (!File.Exists(metadataPath))
        {
            return null;
        }

        var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
        var metadata = JsonSerializer.Deserialize<FileMetadata>(metadataJson, StorageJsonOptions.Default)
            ?? new FileMetadata();
        var contentPath = ResolveContentPath(imageDirectory, variant, metadata);
        if (contentPath == null)
        {
            return null;
        }

        return new RichTextEmbeddedFileContent
        {
            FileName = metadata.FileName ?? string.Empty,
            ContentType = metadata.ContentType ?? "application/octet-stream",
            Content = await File.ReadAllBytesAsync(contentPath, cancellationToken)
        };
    }

    // Полностью удаляет локальное файловое хранилище документа.
    public void DeleteDocumentFolder(Guid businessEntityId)
    {
        var documentDirectory = GetDocumentDirectory(businessEntityId);
        if (Directory.Exists(documentDirectory))
        {
            Directory.Delete(documentDirectory, recursive: true);
        }
    }

    // Полностью очищает root rich-document файлового storage.
    public void DeleteAll()
    {
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    // Возвращает каталог документа.
    private string GetDocumentDirectory(Guid businessEntityId)
    {
        return Path.Combine(_storageRoot, "business-entities", businessEntityId.ToString("D"));
    }

    // Возвращает каталог конкретного embedded-изображения.
    private string GetImageDirectory(Guid businessEntityId, string imageId)
    {
        return Path.Combine(GetDocumentDirectory(businessEntityId), "images", imageId);
    }

    private static string BuildStoredFileName(string? variant, string? fileName, string? contentType)
    {
        var safeVariant = SanitizeFileName(string.IsNullOrWhiteSpace(variant) ? "original" : variant.Trim());
        var extension = ResolveFileExtension(fileName, contentType);
        return $"{safeVariant}{extension}";
    }

    private static string? ResolveContentPath(string imageDirectory, string? variant, FileMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata.StoredFileName))
        {
            var storedFileName = Path.GetFileName(metadata.StoredFileName);
            var storedPath = Path.Combine(imageDirectory, storedFileName);
            if (File.Exists(storedPath))
            {
                return storedPath;
            }
        }

        var newFormatPath = Path.Combine(
            imageDirectory,
            BuildStoredFileName(variant, metadata.FileName, metadata.ContentType));
        if (File.Exists(newFormatPath))
        {
            return newFormatPath;
        }

        var safeVariant = SanitizeFileName(string.IsNullOrWhiteSpace(variant) ? "original" : variant.Trim());
        var legacyPath = Path.Combine(imageDirectory, $"{safeVariant}.bin");
        if (File.Exists(legacyPath))
        {
            return legacyPath;
        }

        return Directory
            .EnumerateFiles(imageDirectory, $"{safeVariant}.*")
            .FirstOrDefault();
    }

    private static void DeleteVariantFiles(string imageDirectory, string? variant)
    {
        var safeVariant = SanitizeFileName(string.IsNullOrWhiteSpace(variant) ? "original" : variant.Trim());
        foreach (var filePath in Directory.EnumerateFiles(imageDirectory, $"{safeVariant}.*"))
        {
            if (!string.Equals(Path.GetFileName(filePath), MetadataFileName, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(filePath);
            }
        }
    }

    private static string ResolveFileExtension(string? fileName, string? contentType)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (IsAllowedFileExtension(extension))
        {
            return extension.ToLowerInvariant();
        }

        return (contentType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            _ => ".dat"
        };
    }

    private static bool IsAllowedFileExtension(string? extension)
    {
        return (extension ?? string.Empty).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" => true,
            _ => false
        };
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' })
            .ToHashSet();
        var chars = value
            .Select(x => invalidChars.Contains(x) || char.IsControl(x) ? '_' : x)
            .ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized)
            ? "file"
            : sanitized;
    }

    // Sidecar-метаданные локально сохраненного embedded-файла.
    private sealed class FileMetadata
    {
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public string? StoredFileName { get; set; }
    }
}
