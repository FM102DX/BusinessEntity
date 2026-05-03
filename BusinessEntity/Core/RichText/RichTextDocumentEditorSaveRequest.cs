namespace BusinessEntity.Core.RichText
{
    public sealed class RichTextDocumentEditorSaveRequest
    {
        public int SavedChunkCount { get; set; }

        public string Title { get; set; } = string.Empty;
    }
}
