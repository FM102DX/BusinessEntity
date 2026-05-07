using BusinessEntity.Core.RichText;
using Microsoft.AspNetCore.Components;

namespace BusinessEntity.Components
{
    public partial class RichTextDocumentWidgets : ComponentBase
    {
        [Parameter] public IReadOnlyList<RichTextDocumentBookmark> Bookmarks { get; set; } = Array.Empty<RichTextDocumentBookmark>();
        [Parameter] public Guid? ActiveBookmarkId { get; set; }
        [Parameter] public string? StatusMessage { get; set; }
        [Parameter] public EventCallback<string> OnSearchNext { get; set; }
        [Parameter] public EventCallback<string> OnSearchPrevious { get; set; }
        [Parameter] public EventCallback OnCreateBookmark { get; set; }
        [Parameter] public EventCallback<RichTextDocumentBookmark> OnBookmarkSelected { get; set; }
        [Parameter] public EventCallback<Guid> OnBookmarkDeleted { get; set; }

        private string SearchQuery { get; set; } = string.Empty;
        private Guid? SelectedBookmarkId { get; set; }
        private bool IsSearchDisabled => string.IsNullOrWhiteSpace(SearchQuery);
        private bool CanNavigateBookmarks => Bookmarks.Count > 0;

        protected override void OnParametersSet()
        {
            if (ActiveBookmarkId.HasValue)
            {
                SelectedBookmarkId = ActiveBookmarkId;
            }
            else if (SelectedBookmarkId.HasValue && Bookmarks.All(x => x.Id != SelectedBookmarkId.Value))
            {
                SelectedBookmarkId = null;
            }
        }

        private void HandleSearchInput(ChangeEventArgs args)
        {
            SearchQuery = args.Value?.ToString() ?? string.Empty;
        }

        private Task HandleSearchNextAsync()
        {
            return OnSearchNext.InvokeAsync(SearchQuery);
        }

        private Task HandleSearchPreviousAsync()
        {
            return OnSearchPrevious.InvokeAsync(SearchQuery);
        }

        private Task HandleCreateBookmarkAsync()
        {
            return OnCreateBookmark.InvokeAsync();
        }

        private async Task SelectBookmarkAsync(RichTextDocumentBookmark bookmark)
        {
            SelectedBookmarkId = bookmark.Id;
            await OnBookmarkSelected.InvokeAsync(bookmark);
        }

        private Task HandleDeleteBookmarkAsync()
        {
            return SelectedBookmarkId.HasValue
                ? OnBookmarkDeleted.InvokeAsync(SelectedBookmarkId.Value)
                : Task.CompletedTask;
        }

        private Task HandlePreviousBookmarkAsync()
        {
            return NavigateBookmarkAsync(-1);
        }

        private Task HandleNextBookmarkAsync()
        {
            return NavigateBookmarkAsync(1);
        }

        private async Task NavigateBookmarkAsync(int direction)
        {
            if (Bookmarks.Count == 0)
            {
                return;
            }

            var currentIndex = SelectedBookmarkId.HasValue
                ? Bookmarks.ToList().FindIndex(x => x.Id == SelectedBookmarkId.Value)
                : -1;

            var nextIndex = currentIndex < 0
                ? (direction > 0 ? 0 : Bookmarks.Count - 1)
                : (currentIndex + direction + Bookmarks.Count) % Bookmarks.Count;

            await SelectBookmarkAsync(Bookmarks[nextIndex]);
        }

        private string GetBookmarkButtonClass(RichTextDocumentBookmark bookmark)
        {
            var isSelected = SelectedBookmarkId == bookmark.Id;
            return isSelected
                ? "rich-text-document-bookmarks__item rich-text-document-bookmarks__item--selected"
                : "rich-text-document-bookmarks__item";
        }
    }
}
