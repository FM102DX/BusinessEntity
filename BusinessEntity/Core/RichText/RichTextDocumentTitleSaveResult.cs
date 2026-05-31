namespace BusinessEntity.Core.RichText
{
    public sealed class RichTextDocumentTitleSaveResult
    {
        public BusinessEntity.Core.Classes.BusinessEntity Entity { get; set; } = default!;

        public string Title { get; set; } = string.Empty;

        public bool TitleChanged { get; set; }
    }
}
