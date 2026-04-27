using System.Net;
using System.Text;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.Core.RichText;
using HtmlAgilityPack;
using Markdig;

namespace BusinessEntity.Services
{
    // Импортирует TXT/MD/HTML во внутренний MVP-формат rich-text документа.
    // На этом этапе сервис только нормализует содержимое; сохранение выполняет BusinessEntityHelper.
    public class RichTextDocumentImportService
    {
        private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        private readonly IHttpClientFactory _httpClientFactory;

        public RichTextDocumentImportService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // Читает загруженный файл и строит manifest + chunks + embedded-файлы.
        public async Task<RichTextDocumentImportResult> ImportAsync(
            string fileName,
            Stream contentStream,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidOperationException("File name is empty.");
            }

            using var memoryStream = new MemoryStream();
            await contentStream.CopyToAsync(memoryStream, cancellationToken);
            var fileBytes = memoryStream.ToArray();
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            List<RichTextBlock> blocks;
            List<RichTextEmbeddedFile> files;

            switch (extension)
            {
                case ".txt":
                    blocks = BuildBlocksFromPlainText(DecodeText(fileBytes));
                    files = new List<RichTextEmbeddedFile>();
                    break;
                case ".md":
                case ".markdown":
                    var markdownText = DecodeText(fileBytes);
                    var htmlFromMarkdown = Markdown.ToHtml(markdownText, MarkdownPipeline);
                    (blocks, files) = await BuildBlocksFromHtmlAsync(htmlFromMarkdown, cancellationToken);
                    break;
                case ".html":
                case ".htm":
                    (blocks, files) = await BuildBlocksFromHtmlAsync(DecodeText(fileBytes), cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException("Поддерживаются только файлы .txt, .md, .markdown, .html и .htm.");
            }

            var chunks = BuildChunks(blocks);
            return new RichTextDocumentImportResult
            {
                Manifest = new RichTextDocument
                {
                    Tag = "RichTextDocument"
                },
                Chunks = chunks,
                Files = files
            };
        }

        // Строит paragraph-блоки из простого текста.
        private static List<RichTextBlock> BuildBlocksFromPlainText(string text)
        {
            var paragraphs = text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split("\n\n", StringSplitOptions.None)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (paragraphs.Count == 0)
            {
                return new List<RichTextBlock>();
            }

            return paragraphs
                .Select(paragraph => new RichTextBlock
                {
                    Kind = "paragraph",
                    Html = WebUtility.HtmlEncode(paragraph).Replace("\n", "<br />", StringComparison.Ordinal)
                })
                .ToList();
        }

        // Парсит HTML и нормализует его в MVP-блоки rich-text документа.
        private async Task<(List<RichTextBlock> blocks, List<RichTextEmbeddedFile> files)> BuildBlocksFromHtmlAsync(
            string html,
            CancellationToken cancellationToken)
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

            return (blocks, files);
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
                // Если контейнер содержит явные block-level элементы, спускаемся рекурсивно.
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

            // Для unsupported container-элементов рекурсивно обрабатываем потомков.
            foreach (var child in node.ChildNodes)
            {
                await ConvertNodeToBlocksAsync(child, blocks, files, cancellationToken);
            }
        }

        // Проверяет, содержит ли узел block-level смысл для текущего MVP.
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

        // Режет блоки на chunks с умеренным размером для MVP.
        private static IReadOnlyList<RichTextDocumentChunk> BuildChunks(IReadOnlyList<RichTextBlock> blocks)
        {
            const int maxBlocksPerChunk = 24;
            const int maxCharsPerChunk = 12000;

            if (blocks == null || blocks.Count == 0)
            {
                return new[]
                {
                    new RichTextDocumentChunk
                    {
                        SortOrder = 0,
                        Blocks = new List<RichTextBlock>()
                    }
                };
            }

            var result = new List<RichTextDocumentChunk>();
            var currentBlocks = new List<RichTextBlock>();
            var currentChars = 0;
            var sortOrder = 0L;

            foreach (var block in blocks)
            {
                var blockCharCount = (block.Html ?? string.Empty).Length + (block.AltText ?? string.Empty).Length;
                if (currentBlocks.Count >= maxBlocksPerChunk || (currentChars + blockCharCount) > maxCharsPerChunk)
                {
                    result.Add(new RichTextDocumentChunk
                    {
                        SortOrder = sortOrder++,
                        Blocks = currentBlocks.ToList()
                    });
                    currentBlocks.Clear();
                    currentChars = 0;
                }

                currentBlocks.Add(block);
                currentChars += blockCharCount;
            }

            if (currentBlocks.Count > 0)
            {
                result.Add(new RichTextDocumentChunk
                {
                    SortOrder = sortOrder,
                    Blocks = currentBlocks.ToList()
                });
            }

            return result;
        }

        // Декодирует текстовый файл как UTF-8 с fallback без BOM.
        private static string DecodeText(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes ?? Array.Empty<byte>());
        }

        // Парсит data-uri картинки в бинарное содержимое и MIME-тип.
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

        // Возвращает файловое расширение для MIME-типа.
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
