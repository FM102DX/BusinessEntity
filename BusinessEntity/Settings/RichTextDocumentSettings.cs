namespace BusinessEntity.Settings
{
    public class RichTextDocumentSettings
    {
        public const string SectionName = "RichTextDocument";

        public int InitialChunkCount { get; set; } = 2;

        public int TableOfContentsBeforeBuffer { get; set; } = 2;

        public int TableOfContentsAfterBuffer { get; set; } = 5;

        public int ScrollPreviousChunkCount { get; set; } = 1;

        public bool HideTableOfContentsScrollbar { get; set; } = true;

        public int GetInitialChunkCount()
        {
            return Math.Max(InitialChunkCount, 1);
        }

        public int GetTableOfContentsBeforeBuffer()
        {
            return Math.Max(TableOfContentsBeforeBuffer, 0);
        }

        public int GetTableOfContentsWindowChunkCount()
        {
            return GetTableOfContentsBeforeBuffer() + 1 + Math.Max(TableOfContentsAfterBuffer, 0);
        }

        public int GetScrollPreviousChunkCount()
        {
            return Math.Max(ScrollPreviousChunkCount, 0);
        }

        public int GetScrollWindowChunkCount()
        {
            return GetScrollPreviousChunkCount() + 1 + Math.Max(TableOfContentsAfterBuffer, 0);
        }
    }
}
