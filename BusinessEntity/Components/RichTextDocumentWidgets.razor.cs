using BusinessEntity.Core.Classes;
using BusinessEntity.Core.RichText;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Connectors;
using Microsoft.AspNetCore.Components;

namespace BusinessEntity.Components
{
    public partial class RichTextDocumentWidgets : ComponentBase
    {
        [Parameter] public IReadOnlyList<RichTextDocumentBookmark> Bookmarks { get; set; } = Array.Empty<RichTextDocumentBookmark>();
        [Parameter] public Guid BusinessEntityId { get; set; }
        [Parameter] public Guid? ActiveBookmarkId { get; set; }
        [Parameter] public int VersionsRefreshToken { get; set; }
        [Parameter] public int ViewedVersion { get; set; } = 1;
        [Parameter] public int LatestVersion { get; set; } = 1;
        [Parameter] public EventCallback<string> OnSearchNext { get; set; }
        [Parameter] public EventCallback<string> OnSearchPrevious { get; set; }
        [Parameter] public EventCallback OnCreateBookmark { get; set; }
        [Parameter] public EventCallback<RichTextDocumentBookmark> OnBookmarkSelected { get; set; }
        [Parameter] public EventCallback<Guid> OnBookmarkDeleted { get; set; }
        [Parameter] public EventCallback<int> OnVersionSelected { get; set; }

        [Inject] public IDataProviderConnector DataProviderConnector { get; set; } = default!;

        private string SearchQuery { get; set; } = string.Empty;
        private Guid? SelectedBookmarkId { get; set; }
        private RichTextDocumentWidgetTab ActiveTab { get; set; } = RichTextDocumentWidgetTab.Search;
        private IReadOnlyList<BusinessEntityDataVersionInfo> Versions { get; set; } = Array.Empty<BusinessEntityDataVersionInfo>();
        private bool IsVersionsLoading { get; set; }
        private string? VersionsError { get; set; }
        private Guid _versionsLoadedForEntityId;
        private int _versionsLoadedForRefreshToken = -1;
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

        protected override async Task OnParametersSetAsync()
        {
            if (BusinessEntityId == Guid.Empty)
            {
                Versions = Array.Empty<BusinessEntityDataVersionInfo>();
                _versionsLoadedForEntityId = Guid.Empty;
                return;
            }

            if (BusinessEntityId == _versionsLoadedForEntityId &&
                VersionsRefreshToken == _versionsLoadedForRefreshToken)
            {
                return;
            }

            await LoadVersionsAsync();
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

        private void SetActiveTab(RichTextDocumentWidgetTab tab)
        {
            ActiveTab = tab;
        }

        private async Task SelectVersionAsync(int version)
        {
            if (version <= 0 || version == ViewedVersion)
            {
                return;
            }

            await OnVersionSelected.InvokeAsync(version);
        }

        private string GetTabButtonClass(RichTextDocumentWidgetTab tab)
        {
            return ActiveTab == tab
                ? "rich-text-document-tabs__tab rich-text-document-tabs__tab--active"
                : "rich-text-document-tabs__tab";
        }

        private async Task LoadVersionsAsync()
        {
            IsVersionsLoading = true;
            VersionsError = null;

            try
            {
                Versions = await DataProviderConnector.GetDataVersionsAsync(BusinessEntityId);
                _versionsLoadedForEntityId = BusinessEntityId;
                _versionsLoadedForRefreshToken = VersionsRefreshToken;
            }
            catch (Exception ex)
            {
                Versions = Array.Empty<BusinessEntityDataVersionInfo>();
                VersionsError = ex.Message;
            }
            finally
            {
                IsVersionsLoading = false;
            }
        }

        private static string FormatVersionDate(DateTime date)
        {
            return date.ToLocalTime().ToString("g");
        }

        private string GetBookmarkButtonClass(RichTextDocumentBookmark bookmark)
        {
            var isSelected = SelectedBookmarkId == bookmark.Id;
            return isSelected
                ? "rich-text-document-bookmarks__item rich-text-document-bookmarks__item--selected"
                : "rich-text-document-bookmarks__item";
        }

        private string GetVersionRowClass(BusinessEntityDataVersionInfo version)
        {
            var normalizedVersion = version.Version <= 0 ? 1 : version.Version;
            return normalizedVersion == ViewedVersion
                ? "rich-text-document-versions__row rich-text-document-versions__row--selected"
                : "rich-text-document-versions__row";
        }

        private enum RichTextDocumentWidgetTab
        {
            Search,
            Bookmarks,
            Versions
        }
    }
}
