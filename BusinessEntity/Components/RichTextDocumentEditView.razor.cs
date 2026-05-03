using BusinessEntity.Core.RichText;
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
        [Parameter] public long? InitialTargetSortOrder { get; set; }
        [Parameter] public IReadOnlyList<RichTextDocumentOutlineNode>? OutlineNodes { get; set; }
        [Parameter] public EventCallback<string?> OnTitleChanged { get; set; }
        [Parameter] public EventCallback OnSaveRequested { get; set; }
        [Parameter] public EventCallback OnReadModeRequested { get; set; }
        [Parameter] public EventCallback OnRebuildTableOfContents { get; set; }

        [Inject] public IJSRuntime JS { get; set; } = default!;
        [Inject] public RichTextDocumentSettingsService RichTextDocumentSettingsService { get; set; } = default!;

        private IReadOnlyList<RichTextDocumentOutlineNode> LocalOutlineNodes { get; set; } = Array.Empty<RichTextDocumentOutlineNode>();
        private IReadOnlyList<RichTextDocumentOutlineNode> VisibleOutlineNodes { get; set; } = Array.Empty<RichTextDocumentOutlineNode>();
        private RichTextDocumentEditorViewport? EditorViewport { get; set; }
        private int DisplayLevelCount { get; set; } = 2;
        private bool HideTableOfContentsScrollbar { get; set; } = true;

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

        private Task HandleReadModeAsync()
        {
            return OnReadModeRequested.InvokeAsync();
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
