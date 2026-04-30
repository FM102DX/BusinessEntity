using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BusinessEntity.Core.RichText;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal;

// Общая low-level логика сериализации чанков rich-text документа.
// Здесь нет доступа к репозиториям: только envelope, html-cache и plain-text derivation.
internal static class RichTextChunkStorageSerializer
{
    private const string ChunkStorageKind = "RichTextDocumentChunk";

    // Сериализует набор блоков чанка в versioned envelope.
    public static string SerializeChunkData(IReadOnlyList<RichTextBlock> blocks)
    {
        var payloadJson = JsonSerializer.Serialize(
            new RichTextChunkPayloadBody
            {
                Blocks = blocks?.ToList() ?? new List<RichTextBlock>()
            },
            StorageJsonOptions.Default);

        return DataPayloadEnvelopeSerializer.CreateEnvelopeJson(ChunkStorageKind, payloadJson);
    }

    // Десериализует versioned envelope чанка в набор блоков.
    public static List<RichTextBlock> DeserializeChunkData(string envelopeJson)
    {
        var envelope = DataPayloadEnvelopeSerializer.ReadEnvelope(envelopeJson);
        if (!string.Equals(envelope.Kind, ChunkStorageKind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported rich-text chunk kind '{envelope.Kind}'.");
        }

        var body = JsonSerializer.Deserialize<RichTextChunkPayloadBody>(envelope.PayloadJson, StorageJsonOptions.Default)
            ?? new RichTextChunkPayloadBody();

        return body.Blocks ?? new List<RichTextBlock>();
    }

    // Извлекает plain-text содержимое чанка для будущего поиска.
    public static string BuildPlainText(IReadOnlyList<RichTextBlock> blocks)
    {
        if (blocks == null || blocks.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var block in blocks)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            switch (block.Kind)
            {
                case "heading":
                case "paragraph":
                    builder.Append(WebUtility.HtmlDecode(StripTags(block.Html)));
                    break;
                case "image":
                    if (!string.IsNullOrWhiteSpace(block.AltText))
                    {
                        builder.Append(block.AltText);
                    }
                    break;
            }
        }

        return builder.ToString().Trim();
    }

    // Собирает готовый readonly HTML чанка.
    public static string BuildHtmlCache(Guid businessEntityId, Guid chunkId, IReadOnlyList<RichTextBlock> blocks)
    {
        if (blocks == null || blocks.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
        {
            var block = blocks[blockIndex];
            switch (block.Kind)
            {
                case "heading":
                    var level = Math.Clamp(block.Level <= 0 ? 2 : block.Level, 1, 6);
                    var anchorAttributes = level <= 3
                        ? $" id=\"{BuildBlockAnchor(chunkId, blockIndex)}\" data-chunk-id=\"{chunkId:D}\" data-block-index=\"{blockIndex}\""
                        : string.Empty;
                    builder.Append($"<h{level}{anchorAttributes}>{block.Html}</h{level}>");
                    break;
                case "paragraph":
                    builder.Append($"<p>{block.Html}</p>");
                    break;
                case "image":
                    var encodedAlt = WebUtility.HtmlEncode(block.AltText ?? string.Empty);
                    var imageId = Uri.EscapeDataString(block.ImageId ?? string.Empty);
                    var variant = Uri.EscapeDataString(string.IsNullOrWhiteSpace(block.DisplayVariant) ? "original" : block.DisplayVariant);
                    builder.Append(
                        $"<p class=\"rich-text-image\"><img src=\"/rich-document-files/{businessEntityId:D}/images/{imageId}/{variant}\" alt=\"{encodedAlt}\" /></p>");
                    break;
            }
        }

        return builder.ToString();
    }

    // Builds a stable DOM anchor for a block inside a persisted rich-text chunk.
    public static string BuildBlockAnchor(Guid chunkId, int blockIndex)
    {
        return $"rt-chunk-{chunkId:N}-block-{blockIndex}";
    }

    // Extracts readable text from sanitized inline HTML.
    public static string BuildInlineText(string? html)
    {
        return WebUtility.HtmlDecode(StripTags(html)).Trim();
    }

    // Считает количество текстовых символов чанка.
    public static int BuildCharCount(IReadOnlyList<RichTextBlock> blocks)
    {
        return BuildPlainText(blocks).Length;
    }

    // Считает checksum сериализованного envelope JSON.
    public static string BuildChecksum(string envelopeJson)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(envelopeJson ?? string.Empty);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    // Грубое, но достаточное для MVP удаление HTML-тегов из inline-html.
    private static string StripTags(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(html.Length);
        var insideTag = false;
        foreach (var ch in html)
        {
            if (ch == '<')
            {
                insideTag = true;
                continue;
            }

            if (ch == '>')
            {
                insideTag = false;
                continue;
            }

            if (!insideTag)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    // JSON-body чанка внутри envelope.
    private sealed class RichTextChunkPayloadBody
    {
        [JsonPropertyName("blocks")]
        public List<RichTextBlock>? Blocks { get; set; }
    }
}
