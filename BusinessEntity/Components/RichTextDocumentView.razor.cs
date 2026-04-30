using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace BusinessEntity.Components
{
    public partial class RichTextDocumentView : ComponentBase
    {
        // Stable DOM id of the scrollable rich-text viewport.
        private readonly string ViewportElementId = $"rich-text-viewport-{Guid.NewGuid():N}";

        [Parameter] public string EntityName { get; set; } = string.Empty;
        [Parameter] public string HtmlContent { get; set; } = string.Empty;
        [Parameter] public bool IsBusy { get; set; }
        [Parameter] public bool IsRebuildingTableOfContents { get; set; }
        [Parameter] public string? StatusMessage { get; set; }
        [Parameter] public IReadOnlyList<RichTextDocumentOutlineNode>? OutlineNodes { get; set; }
        [Parameter] public EventCallback<InputFileChangeEventArgs> OnImportSelected { get; set; }
        [Parameter] public EventCallback OnRebuildTableOfContents { get; set; }

        [Inject] public IJSRuntime JSRuntime { get; set; } = default!;

        // HTML document assembled from persisted chunk HtmlCache values.
        private string RenderedHtmlContent { get; set; } = string.Empty;

        // Hierarchical table of contents loaded from persisted chunk properties.
        private IReadOnlyList<RichTextDocumentOutlineNode> LocalOutlineNodes { get; set; } = Array.Empty<RichTextDocumentOutlineNode>();

        private IReadOnlyList<RichTextDocumentOutlineNode> VisibleOutlineNodes { get; set; } = Array.Empty<RichTextDocumentOutlineNode>();

        // Number of heading levels displayed in the outline. H1-H2 is the default view.
        private int DisplayLevelCount { get; set; } = 2;

        protected override void OnParametersSet()
        {
            BuildDocumentPresentation();
        }

        // Forwards file selection to the page-level import orchestration.
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

        // Scrolls the viewport to a stable heading anchor from the persisted table of contents.
        private Task HandleHeadingSelectedAsync(string headingId)
        {
            return JSRuntime.InvokeVoidAsync("richTextViewport.scrollToHeading", ViewportElementId, headingId).AsTask();
        }

        // Uses persisted HTML and persisted outline data without reparsing headings from the DOM.
        private void BuildDocumentPresentation()
        {
            if (string.IsNullOrWhiteSpace(HtmlContent))
            {
                RenderedHtmlContent = string.Empty;
                LocalOutlineNodes = Array.Empty<RichTextDocumentOutlineNode>();
                VisibleOutlineNodes = Array.Empty<RichTextDocumentOutlineNode>();
                return;
            }

            RenderedHtmlContent = HtmlContent;
            LocalOutlineNodes = OutlineNodes ?? Array.Empty<RichTextDocumentOutlineNode>();
            VisibleOutlineNodes = FilterOutlineNodes(LocalOutlineNodes, DisplayLevelCount);
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
                    Title = node.Title,
                    Level = node.Level,
                    IsExpanded = node.IsExpanded,
                    Children = FilterOutlineNodes(node.Children, maxLevel).ToList()
                });
            }

            return result;
        }
    }
}
