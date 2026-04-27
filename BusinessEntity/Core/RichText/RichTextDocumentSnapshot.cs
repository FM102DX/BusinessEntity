using BusinessEntity.Core.Classes;
using BusinessEntity.Core.DomainEntities;

namespace BusinessEntity.Core.RichText
{
    // Полный readonly-снимок rich-text документа для просмотра.
    public class RichTextDocumentSnapshot
    {
        // Базовая business-entity документа.
        public BusinessEntity.Core.Classes.BusinessEntity Entity { get; set; } = new();

        // Manifest rich-text документа.
        public RichTextDocument Manifest { get; set; } = new();

        // Технические чанки документа в порядке отображения.
        public IReadOnlyList<RichTextDocumentChunk> Chunks { get; set; } = Array.Empty<RichTextDocumentChunk>();
    }
}
