using System.Net;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace BusinessEntity.Components
{
    public partial class RichTextDocumentView : ComponentBase
    {
        // Стабильный DOM-id scrollable viewport rich-text документа.
        private readonly string ViewportElementId = $"rich-text-viewport-{Guid.NewGuid():N}";

        [Parameter] public string EntityName { get; set; } = string.Empty;
        [Parameter] public string HtmlContent { get; set; } = string.Empty;
        [Parameter] public bool IsBusy { get; set; }
        [Parameter] public string? StatusMessage { get; set; }
        [Parameter] public EventCallback<InputFileChangeEventArgs> OnImportSelected { get; set; }

        [Inject] public IJSRuntime JSRuntime { get; set; } = default!;

        // Подготовленный HTML документа с id на H1-H3, чтобы по ним можно было прокручивать viewport.
        private string RenderedHtmlContent { get; set; } = string.Empty;
        // Иерархическое оглавление документа.
        private IReadOnlyList<RichTextDocumentOutlineNode> OutlineNodes { get; set; } = Array.Empty<RichTextDocumentOutlineNode>();

        protected override void OnParametersSet()
        {
            BuildDocumentPresentation();
        }

        // Пробрасывает событие выбора файла наружу, в page-level orchestration.
        private Task HandleImportSelected(InputFileChangeEventArgs args)
        {
            return OnImportSelected.InvokeAsync(args);
        }

        // Прокручивает viewport документа к выбранному заголовку из дерева содержания.
        private Task HandleHeadingSelectedAsync(string headingId)
        {
            return JSRuntime.InvokeVoidAsync("richTextViewport.scrollToHeading", ViewportElementId, headingId).AsTask();
        }

        // Строит одновременно HTML для viewport и дерево содержания H1-H3.
        private void BuildDocumentPresentation()
        {
            if (string.IsNullOrWhiteSpace(HtmlContent))
            {
                RenderedHtmlContent = string.Empty;
                OutlineNodes = Array.Empty<RichTextDocumentOutlineNode>();
                return;
            }

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(HtmlContent);

            var outlineNodes = BuildOutlineNodes(htmlDocument);
            OutlineNodes = outlineNodes;
            RenderedHtmlContent = htmlDocument.DocumentNode.InnerHtml;
        }

        // Собирает иерархию содержания из заголовков H1-H3 и одновременно вшивает в них стабильные DOM-id.
        private static IReadOnlyList<RichTextDocumentOutlineNode> BuildOutlineNodes(HtmlDocument htmlDocument)
        {
            var headingNodes = htmlDocument.DocumentNode
                .SelectNodes("//h1|//h2|//h3")
                ?.ToList()
                ?? new List<HtmlNode>();

            if (headingNodes.Count == 0)
            {
                return Array.Empty<RichTextDocumentOutlineNode>();
            }

            var roots = new List<RichTextDocumentOutlineNode>();
            var stack = new Stack<RichTextDocumentOutlineNode>();
            var index = 0;

            foreach (var headingNode in headingNodes)
            {
                index++;
                var level = ParseHeadingLevel(headingNode.Name);
                var title = WebUtility.HtmlDecode(headingNode.InnerText ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = $"Heading {index}";
                }

                var headingId = $"rt-heading-{index}";
                headingNode.SetAttributeValue("id", headingId);
                headingNode.SetAttributeValue("data-outline-level", level.ToString(System.Globalization.CultureInfo.InvariantCulture));

                var outlineNode = new RichTextDocumentOutlineNode
                {
                    HeadingId = headingId,
                    Title = title,
                    Level = level,
                    IsExpanded = true
                };

                while (stack.Count > 0 && stack.Peek().Level >= level)
                {
                    stack.Pop();
                }

                if (stack.Count == 0)
                {
                    roots.Add(outlineNode);
                }
                else
                {
                    stack.Peek().Children.Add(outlineNode);
                }

                stack.Push(outlineNode);
            }

            return roots;
        }

        // Возвращает числовой уровень заголовка h1/h2/h3.
        private static int ParseHeadingLevel(string headingTagName)
        {
            return headingTagName.ToLowerInvariant() switch
            {
                "h1" => 1,
                "h2" => 2,
                "h3" => 3,
                _ => 3
            };
        }
    }
}
