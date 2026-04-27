using BusinessEntity.Core.DomainEntities;

namespace BusinessEntity.Core.RichText
{
    // Результат импорта внешнего файла во внутренний rich-text формат.
    public class RichTextDocumentImportResult
    {
        // Manifest, который нужно сохранить в BusinessEntityDataDto.Data.
        public RichTextDocument Manifest { get; set; } = new();

        // Нормализованные чанки документа.
        public IReadOnlyList<RichTextDocumentChunk> Chunks { get; set; } = Array.Empty<RichTextDocumentChunk>();

        // Embedded-файлы, которые должны быть сохранены в локальное storage.
        public IReadOnlyList<RichTextEmbeddedFile> Files { get; set; } = Array.Empty<RichTextEmbeddedFile>();
    }
}
