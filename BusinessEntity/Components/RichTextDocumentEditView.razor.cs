using BusinessEntity.Core.RichText;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Connectors;
using BusinessEntity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BusinessEntity.Components
{
    public partial class RichTextDocumentEditView : ComponentBase, IAsyncDisposable
    {
        private readonly string EditorViewportElementId = $"rich-text-editor-viewport-{Guid.NewGuid():N}";
        private readonly string OutlineTreeElementId = $"rich-text-edit-outline-tree-{Guid.NewGuid():N}";
        private bool _outlineTreeRegistered;

        [Parameter] public string EditableEntityName { get; set; } = string.Empty;
        [Parameter] public string? TitleValidationMessage { get; set; }
        [Parameter] public Guid BusinessEntityId { get; set; }
        [Parameter] public RichTextDocumentChunkWindow? InitialChunkWindow { get; set; }
        [Parameter] public bool IsInitialContentLoading { get; set; }
        [Parameter] public bool IsOutlineLoading { get; set; }
        [Parameter] public bool IsBusy { get; set; }
        [Parameter] public bool IsSaving { get; set; }
        [Parameter] public bool IsRebuildingTableOfContents { get; set; }
        [Parameter] public string? StatusMessage { get; set; }
        [Parameter] public RichTextDocumentViewportPosition? InitialTargetPosition { get; set; }
        [Parameter] public IReadOnlyList<RichTextDocumentOutlineNode>? OutlineNodes { get; set; }
        [Parameter] public EventCallback<string?> OnTitleChanged { get; set; }
        [Parameter] public EventCallback OnSaveRequested { get; set; }
        [Parameter] public EventCallback<RichTextDocumentViewportPosition?> OnReadModeRequested { get; set; }
        [Parameter] public EventCallback OnRebuildTableOfContents { get; set; }

        [Inject] public IJSRuntime JS { get; set; } = default!;
        [Inject] public RichTextDocumentSettingsService RichTextDocumentSettingsService { get; set; } = default!;
        [Inject] public RichTextDocumentHelper RichTextDocumentHelper { get; set; } = default!;
        [Inject] public IUserConnector UserConnector { get; set; } = default!;

        private IReadOnlyList<RichTextDocumentOutlineNode> LocalOutlineNodes { get; set; } = Array.Empty<RichTextDocumentOutlineNode>();
        private IReadOnlyList<RichTextDocumentOutlineNode> VisibleOutlineNodes { get; set; } = Array.Empty<RichTextDocumentOutlineNode>();
        private IReadOnlyList<RichTextDocumentBookmark> Bookmarks { get; set; } = Array.Empty<RichTextDocumentBookmark>();
        private RichTextDocumentEditorViewport? EditorViewport { get; set; }
        private int DisplayLevelCount { get; set; } = 2;
        private bool HideTableOfContentsScrollbar { get; set; } = true;
        private Guid _bookmarksLoadedForEntityId;
        private Guid? ActiveBookmarkId { get; set; }
        private string? WidgetStatusMessage { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var settings = await RichTextDocumentSettingsService.GetSettingsAsync();
            HideTableOfContentsScrollbar = settings.HideTableOfContentsScrollbar;
        }

        protected override void OnParametersSet()
        {
            LocalOutlineNodes = OutlineNodes ?? Array.Empty<RichTextDocumentOutlineNode>();
            VisibleOutlineNodes = FilterOutlineNodes(LocalOutlineNodes, DisplayLevelCount);
        }

        protected override async Task OnParametersSetAsync()
        {
            if (BusinessEntityId != Guid.Empty && BusinessEntityId != _bookmarksLoadedForEntityId)
            {
                await LoadBookmarksAsync();
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (VisibleOutlineNodes.Count == 0)
            {
                return;
            }

            if (!_outlineTreeRegistered)
            {
                await JS.InvokeVoidAsync("richTextOutlineViewport.registerViewport", OutlineTreeElementId);
                _outlineTreeRegistered = true;
            }
            else
            {
                await JS.InvokeVoidAsync("richTextOutlineViewport.syncViewportSize", OutlineTreeElementId);
            }
        }

        public Task<int> SaveAsync()
        {
            return EditorViewport?.SaveAsync() ?? Task.FromResult(0);
        }

        private Task HandleTitleInputAsync(ChangeEventArgs args)
        {
            return OnTitleChanged.InvokeAsync(args.Value?.ToString());
        }

        private Task HandleSaveAsync()
        {
            return OnSaveRequested.InvokeAsync();
        }

        private async Task HandleReadModeAsync()
        {
            var position = EditorViewport == null
                ? null
                : await EditorViewport.GetCurrentViewportPositionAsync();
            await OnReadModeRequested.InvokeAsync(position);
        }

        private Task HandleRebuildTableOfContentsAsync()
        {
            return OnRebuildTableOfContents.InvokeAsync();
        }

        private Task HandleDisplayLevelCountChangedAsync(int value)
        {
            DisplayLevelCount = Math.Clamp(value, 1, 3);
            VisibleOutlineNodes = FilterOutlineNodes(LocalOutlineNodes, DisplayLevelCount);
            return Task.CompletedTask;
        }

        private Task HandleHeadingSelectedAsync(RichTextDocumentOutlineNode node)
        {
            return EditorViewport?.ScrollToHeadingAsync(node.HeadingId, node.ChunkSortOrder) ?? Task.CompletedTask;
        }

        private Task HandleSearchNextAsync(string query)
        {
            return HandleSearchAsync(query, searchDown: true);
        }

        private Task HandleSearchPreviousAsync(string query)
        {
            return HandleSearchAsync(query, searchDown: false);
        }

        private async Task HandleSearchAsync(string query, bool searchDown)
        {
            WidgetStatusMessage = null;
            if (EditorViewport == null || BusinessEntityId == Guid.Empty)
            {
                return;
            }

            var origin = await EditorViewport.GetCurrentViewportPositionAsync();
            var result = await RichTextDocumentHelper.FindTextAsync(BusinessEntityId, query, origin, searchDown);
            if (result == null)
            {
                WidgetStatusMessage = "Ничего не найдено.";
                return;
            }

            await EditorViewport.ScrollToPositionAsync(result.Position);
            WidgetStatusMessage = string.IsNullOrWhiteSpace(result.Preview)
                ? "Найдено."
                : result.Preview;
        }

        private async Task HandleCreateBookmarkAsync()
        {
            WidgetStatusMessage = null;
            if (EditorViewport == null || BusinessEntityId == Guid.Empty)
            {
                return;
            }

            var selection = await EditorViewport.GetCurrentTextSelectionAsync();
            var bookmark = await UserConnector.AddRichDocBookmarkAsync(BusinessEntityId, selection);
            if (bookmark == null)
            {
                WidgetStatusMessage = "Выделите текст в документе.";
                return;
            }

            ActiveBookmarkId = bookmark.Id;
            await LoadBookmarksAsync();
            WidgetStatusMessage = "Закладка создана.";
        }

        private async Task HandleBookmarkSelectedAsync(RichTextDocumentBookmark bookmark)
        {
            ActiveBookmarkId = bookmark.Id;
            WidgetStatusMessage = null;
            if (EditorViewport != null)
            {
                await EditorViewport.ScrollToPositionAsync(bookmark.Position);
            }
        }

        private async Task HandleBookmarkDeletedAsync(Guid bookmarkId)
        {
            var deleted = await UserConnector.DeleteRichDocBookmarkAsync(bookmarkId);
            if (deleted && ActiveBookmarkId == bookmarkId)
            {
                ActiveBookmarkId = null;
            }

            await LoadBookmarksAsync();
            WidgetStatusMessage = deleted ? "Закладка удалена." : "Закладка не найдена.";
        }

        private async Task LoadBookmarksAsync()
        {
            if (BusinessEntityId == Guid.Empty)
            {
                Bookmarks = Array.Empty<RichTextDocumentBookmark>();
                _bookmarksLoadedForEntityId = Guid.Empty;
                return;
            }

            Bookmarks = await UserConnector.GetRichDocBookmarksAsync(BusinessEntityId);
            _bookmarksLoadedForEntityId = BusinessEntityId;
        }

        private static IReadOnlyList<RichTextDocumentOutlineNode> FilterOutlineNodes(
            IReadOnlyList<RichTextDocumentOutlineNode> nodes,
            int maxLevel)
        {
            if (nodes.Count == 0)
            {
                return Array.Empty<RichTextDocumentOutlineNode>();
            }

            var result = new List<RichTextDocumentOutlineNode>();
            foreach (var node in nodes)
            {
                if (node.Level > maxLevel)
                {
                    continue;
                }

                result.Add(new RichTextDocumentOutlineNode
                {
                    HeadingId = node.HeadingId,
                    ChunkSortOrder = node.ChunkSortOrder,
                    Title = node.Title,
                    Level = node.Level,
                    IsExpanded = node.IsExpanded,
                    Children = FilterOutlineNodes(node.Children, maxLevel).ToList()
                });
            }

            return result;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_outlineTreeRegistered)
            {
                return;
            }

            try
            {
                await JS.InvokeVoidAsync("richTextOutlineViewport.unregisterViewport", OutlineTreeElementId);
            }
            catch (JSDisconnectedException)
            {
                // Blazor Server can disconnect JS runtime during teardown.
            }
        }
    }
}
