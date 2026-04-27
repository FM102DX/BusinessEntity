using System.Text.Json;
using BusinessEntity.Core.RichText;
using Microsoft.AspNetCore.Hosting;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal;

// Внутренний файловый storage rich-text документа.
// Хранит embedded-изображения документа вне графовой модели BusinessEntity.
internal sealed class RichTextDocumentFileStorageService
{
    private const string MetadataFileName = "metadata.json";
    private readonly string _storageRoot;

    // Вычисляет root-папку storage относительно content-root приложения.
    public RichTextDocumentFileStorageService(IWebHostEnvironment environment)
    {
        var contentRoot = environment.ContentRootPath ?? AppContext.BaseDirectory;
        _storageRoot = Path.Combine(contentRoot, "App_Data", "RichDocumentData");
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

            var contentPath = Path.Combine(imageDirectory, $"{file.Variant}.bin");
            var metadataPath = Path.Combine(imageDirectory, MetadataFileName);

            await File.WriteAllBytesAsync(contentPath, file.Content ?? Array.Empty<byte>(), cancellationToken);

            var metadataJson = JsonSerializer.Serialize(
                new FileMetadata
                {
                    FileName = file.FileName ?? string.Empty,
                    ContentType = file.ContentType ?? "application/octet-stream"
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
        var contentPath = Path.Combine(imageDirectory, $"{variant}.bin");
        var metadataPath = Path.Combine(imageDirectory, MetadataFileName);

        if (!File.Exists(contentPath) || !File.Exists(metadataPath))
        {
            return null;
        }

        var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
        var metadata = JsonSerializer.Deserialize<FileMetadata>(metadataJson, StorageJsonOptions.Default)
            ?? new FileMetadata();

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
        return Path.Combine(_storageRoot, businessEntityId.ToString("D"));
    }

    // Возвращает каталог конкретного embedded-изображения.
    private string GetImageDirectory(Guid businessEntityId, string imageId)
    {
        return Path.Combine(GetDocumentDirectory(businessEntityId), "images", imageId);
    }

    // Sidecar-метаданные локально сохраненного embedded-файла.
    private sealed class FileMetadata
    {
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
    }
}
