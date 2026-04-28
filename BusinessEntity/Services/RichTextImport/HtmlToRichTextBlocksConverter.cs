using System.Net;
using System.Text;
using BusinessEntity.Core.RichText;
using HtmlAgilityPack;

namespace BusinessEntity.Services.RichTextImport
{
    // Общий low-level конвертер HTML -> rich-text blocks/files.
    // Используется и для прямого HTML-импорта, и для Markdown после промежуточного рендера в HTML.
    public class HtmlToRichTextBlocksConverter
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public HtmlToRichTextBlocksConverter(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<RichTextImportContent> ConvertHtmlAsync(
            string html,
            CancellationToken cancellationToken = default)
        {
            var document = new HtmlDocument();
            document.LoadHtml(html ?? string.Empty);

            var root = document.DocumentNode.SelectSingleNode("//body") ?? document.DocumentNode;
            var blocks = new List<RichTextBlock>();
            var files = new List<RichTextEmbeddedFile>();

            foreach (var child in root.ChildNodes)
            {
                await ConvertNodeToBlocksAsync(child, blocks, files, cancellationToken);
            }

            return new RichTextImportContent
            {
                Blocks = blocks,
                Files = files
            };
        }

        // Рекурсивно преобразует HTML-узел в один или несколько поддерживаемых блоков.
        private async Task ConvertNodeToBlocksAsync(
            HtmlNode node,
            List<RichTextBlock> blocks,
            List<RichTextEmbeddedFile> files,
            CancellationToken cancellationToken)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                var text = HtmlEntity.DeEntitize(node.InnerText ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    blocks.Add(new RichTextBlock
                    {
                        Kind = "paragraph",
                        Html = WebUtility.HtmlEncode(text)
                    });
                }
                return;
            }

            if (node.NodeType != HtmlNodeType.Element)
            {
                return;
            }

            var nodeName = node.Name.ToLowerInvariant();
            if (nodeName is "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
            {
                var level = int.Parse(nodeName.Substring(1), System.Globalization.CultureInfo.InvariantCulture);
                var headingHtml = SanitizeInlineHtml(node);
                if (!string.IsNullOrWhiteSpace(headingHtml))
                {
                    blocks.Add(new RichTextBlock
                    {
                        Kind = "heading",
                        Level = level,
                        Html = headingHtml
                    });
                }
                return;
            }

            if (nodeName == "img")
            {
                var imageFile = await TryImportImageAsync(node, cancellationToken);
                if (imageFile != null)
                {
                    files.Add(imageFile);
                    blocks.Add(new RichTextBlock
                    {
                        Kind = "image",
                        ImageId = imageFile.ImageId,
                        DisplayVariant = imageFile.Variant,
                        AltText = node.GetAttributeValue("alt", string.Empty)
                    });
                }
                return;
            }

            if (nodeName is "p" or "div")
            {
                // Если контейнер содержит block-level потомков, обрабатываем их как самостоятельные блоки.
                if (node.ChildNodes.Any(IsBlockLikeNode))
                {
                    foreach (var child in node.ChildNodes)
                    {
                        await ConvertNodeToBlocksAsync(child, blocks, files, cancellationToken);
                    }
                    return;
                }

                var paragraphHtml = SanitizeInlineHtml(node);
                if (!string.IsNullOrWhiteSpace(paragraphHtml))
                {
                    blocks.Add(new RichTextBlock
                    {
                        Kind = "paragraph",
                        Html = paragraphHtml
                    });
                }
                return;
            }

            if (nodeName == "br")
            {
                return;
            }

            foreach (var child in node.ChildNodes)
            {
                await ConvertNodeToBlocksAsync(child, blocks, files, cancellationToken);
            }
        }

        private static bool IsBlockLikeNode(HtmlNode node)
        {
            if (node.NodeType != HtmlNodeType.Element)
            {
                return false;
            }

            var nodeName = node.Name.ToLowerInvariant();
            return nodeName is "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "p" or "div" or "img";
        }

        // Санитизирует inline-html так, чтобы в документ попали только разрешенные теги форматирования.
        private static string SanitizeInlineHtml(HtmlNode node)
        {
            var builder = new StringBuilder();
            foreach (var child in node.ChildNodes)
            {
                AppendSanitizedInlineHtml(child, builder);
            }
            return builder.ToString().Trim();
        }

        // Рекурсивно собирает безопасный inline-html для paragraph/heading блока.
        private static void AppendSanitizedInlineHtml(HtmlNode node, StringBuilder builder)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                builder.Append(WebUtility.HtmlEncode(HtmlEntity.DeEntitize(node.InnerText ?? string.Empty)));
                return;
            }

            if (node.NodeType != HtmlNodeType.Element)
            {
                return;
            }

            var nodeName = node.Name.ToLowerInvariant();
            if (nodeName == "br")
            {
                builder.Append("<br />");
                return;
            }

            if (nodeName is "strong" or "b" or "em" or "i" or "u")
            {
                var normalizedTag = nodeName switch
                {
                    "b" => "strong",
                    "i" => "em",
                    _ => nodeName
                };

                builder.Append('<').Append(normalizedTag).Append('>');
                foreach (var child in node.ChildNodes)
                {
                    AppendSanitizedInlineHtml(child, builder);
                }
                builder.Append("</").Append(normalizedTag).Append('>');
                return;
            }

            foreach (var child in node.ChildNodes)
            {
                AppendSanitizedInlineHtml(child, builder);
            }
        }

        // Импортирует картинку из data-uri или внешнего http/https URL.
        private async Task<RichTextEmbeddedFile?> TryImportImageAsync(HtmlNode imageNode, CancellationToken cancellationToken)
        {
            var src = imageNode.GetAttributeValue("src", string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(src))
            {
                return null;
            }

            byte[] content;
            string contentType;
            string fileName;

            if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var dataUri = ParseDataUri(src);
                content = dataUri.content;
                contentType = dataUri.contentType;
                fileName = $"embedded.{GetExtensionFromContentType(contentType)}";
            }
            else if (Uri.TryCreate(src, UriKind.Absolute, out var absoluteUri) &&
                     (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
            {
                using var client = _httpClientFactory.CreateClient();
                using var response = await client.GetAsync(absoluteUri, cancellationToken);
                response.EnsureSuccessStatusCode();
                contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                fileName = Path.GetFileName(absoluteUri.LocalPath);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = $"external.{GetExtensionFromContentType(contentType)}";
                }
            }
            else
            {
                return null;
            }

            return new RichTextEmbeddedFile
            {
                ImageId = Guid.NewGuid().ToString("N"),
                Variant = "original",
                FileName = fileName,
                ContentType = contentType,
                Content = content
            };
        }

        private static (byte[] content, string contentType) ParseDataUri(string dataUri)
        {
            var commaIndex = dataUri.IndexOf(',');
            if (commaIndex < 0)
            {
                throw new InvalidOperationException("Invalid data URI.");
            }

            var header = dataUri.Substring(5, commaIndex - 5);
            var payload = dataUri[(commaIndex + 1)..];
            var contentType = header.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "application/octet-stream";
            var content = Convert.FromBase64String(payload);
            return (content, contentType);
        }

        private static string GetExtensionFromContentType(string contentType)
        {
            return contentType.ToLowerInvariant() switch
            {
                "image/jpeg" => "jpg",
                "image/png" => "png",
                "image/gif" => "gif",
                "image/webp" => "webp",
                "image/svg+xml" => "svg",
                _ => "bin"
            };
        }
    }
}
