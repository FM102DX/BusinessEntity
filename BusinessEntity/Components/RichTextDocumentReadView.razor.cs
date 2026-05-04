using BusinessEntity.Core.RichText;
using BusinessEntity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace BusinessEntity.Components
{
    public partial class RichTextDocumentReadView : ComponentBase, IAsyncDisposable
    {
        private readonly string ViewportElementId = $"rich-text-read-viewport-{Guid.NewGuid():N}";
        private readonly string OutlineTreeElementId = $"rich-text-read-outline-tree-{Guid.NewGuid():N}";
        private bool _outlineTreeRegistered;

        [Parameter] public string EntityName { get; set; } = string.Empty;
        [Parameter] public Guid BusinessEntityId { get; set; }
        [Parameter] public RichTextDocumentChunkWindow? InitialChunkWindow { get; set; }
        [Parameter] public bool IsInitialContentLoading { get; set; }
        [Parameter] public bool IsOutlineLoading { get; set; }
        [Parameter] public bool IsBusy { get; set; }
        [Parameter] public bool IsRebuildingTableOfContents { get; set; }
        [Parameter] public string? StatusMessage { get; set; }
        [Parameter] public IReadOnlyList<RichTextDocumentOutlineNode>? OutlineNodes { get; set; }
        [Parameter] public EventCallback<InputFileChangeEventArgs> OnImportSelected { get; set; }
        [Parameter] public EventCallback OnRebuildTableOfContents { get; set; }
        [Parameter] public RichTextDocumentViewportPosition? InitialTargetPosition { get; set; }
        [Parameter] public EventCallback<RichTextDocumentViewportPosition?> OnEditRequested { get; set; }

        [Inject] public IJSRuntime JS { get; set; } = default!;
        [Inject] public RichTextDocumentSettingsService RichTextDocumentSettingsService { get; set; } = default!;

        private IReadOnlyList<RichTextDocumentOutlineNode> LocalOutlineNodes { get; set; } = Array.Empty<RichTextDocumentOutlineNode>();
        private IReadOnlyList<RichTextDocumentOutlineNode> VisibleOutlineNodes { get; set; } = Array.Empty<RichTextDocumentOutlineNode>();
        private RichTextDocumentViewport? Viewport { get; set; }
        private int DisplayLevelCount { get; set; } = 2;
        private bool HideTableOfContentsScrollbar { get; set; } = true;

        private bool IsDocumentEmpty =>
            !IsInitialContentLoading &&
            (InitialChunkWindow == null ||
            InitialChunkWindow.TotalChunkCount == 0 ||
            (InitialChunkWindow.TotalChunkCount == 1 &&
             InitialChunkWindow.Chunks.All(chunk => string.IsNullOrWhiteSpace(chunk.HtmlCache))));

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

        private Task HandleImportSelected(InputFileChangeEventArgs args)
        {
            return OnImportSelected.InvokeAsync(args);
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
            return Viewport?.ScrollToHeadingAsync(node.HeadingId, node.ChunkSortOrder) ?? Task.CompletedTask;
        }

        private async Task HandleEditAsync()
        {
            var position = Viewport == null
                ? null
                : await Viewport.GetCurrentViewportPositionAsync();
            await OnEditRequested.InvokeAsync(position);
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
