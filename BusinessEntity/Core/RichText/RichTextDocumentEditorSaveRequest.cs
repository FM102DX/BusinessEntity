namespace BusinessEntity.Core.RichText
{
    public sealed class RichTextDocumentEditorSaveRequest
    {
        public int SavedChunkCount { get; set; }

        public string Title { get; set; } = string.Empty;

        public string VersionDescription { get; set; } = string.Empty;
    }
}
