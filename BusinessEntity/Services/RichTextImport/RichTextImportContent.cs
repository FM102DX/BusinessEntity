using BusinessEntity.Core.RichText;

namespace BusinessEntity.Services.RichTextImport
{
    // Промежуточный результат import-конвертера.
    // На этом этапе документ уже разложен на блоки и embedded-файлы,
    // но еще не порезан на технические чанки хранения.
    public class RichTextImportContent
    {
        public IReadOnlyList<RichTextBlock> Blocks { get; init; } = Array.Empty<RichTextBlock>();
        public IReadOnlyList<RichTextEmbeddedFile> Files { get; init; } = Array.Empty<RichTextEmbeddedFile>();
    }
}
