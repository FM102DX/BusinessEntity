using BusinessEntity.Core.DomainEntities;

namespace BusinessEntity.Core.RichText
{
    // Lightweight rich-text document metadata without loading body chunks.
    public class RichTextDocumentShell
    {
        public BusinessEntity.Core.Classes.BusinessEntity Entity { get; set; } = new();

        public RichTextDocument Manifest { get; set; } = new();
    }
}
