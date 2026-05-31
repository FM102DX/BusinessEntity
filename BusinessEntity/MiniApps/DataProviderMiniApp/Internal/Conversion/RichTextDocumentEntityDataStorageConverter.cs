using System.Text.Json.Serialization;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.DomainEntities;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal.Conversion;

// Конвертер manifest-а rich-text документа.
// Тело документа здесь не хранится: только storage-настройки и признаки документа.
internal sealed class RichTextDocumentEntityDataStorageConverter : EntityDataStorageConverterBase<RichTextDocument>
{
    // Rich-text manifest привязан к типу RichTextDocument.
    public override BusinessEntityTypeEnum SupportedType => BusinessEntityTypeEnum.RichTextDocument;

    // Формирует компактное body manifest-а rich-text документа.
    public override string SerializePayload(RichTextDocument data)
    {
        return SerializeBody(new RichTextDocumentPayloadBody
        {
            Tag = data.Tag ?? string.Empty,
            ContentStorage = data.ContentStorage ?? "ChunkedBlocks",
            EditorFormat = data.EditorFormat ?? "BlockJsonWithInlineHtml",
            ChunkPolicy = data.ChunkPolicy ?? "RichTextMvpV1",
            EmbeddedFileStorage = data.EmbeddedFileStorage ?? "LocalDocumentFiles",
            SupportsImages = data.SupportsImages,
            PublishedVersion = data.PublishedVersion
        });
    }

    // Восстанавливает typed manifest из storage-body.
    public override RichTextDocument DeserializePayload(string payloadJson)
    {
        var body = DeserializeBody<RichTextDocumentPayloadBody>(payloadJson);
        return new RichTextDocument
        {
            Tag = body.Tag ?? string.Empty,
            ContentStorage = body.ContentStorage ?? "ChunkedBlocks",
            EditorFormat = body.EditorFormat ?? "BlockJsonWithInlineHtml",
            ChunkPolicy = body.ChunkPolicy ?? "RichTextMvpV1",
            EmbeddedFileStorage = body.EmbeddedFileStorage ?? "LocalDocumentFiles",
            SupportsImages = body.SupportsImages,
            PublishedVersion = body.PublishedVersion
        };
    }

    // Внутренний JSON-контракт manifest-а rich-text документа.
    private sealed class RichTextDocumentPayloadBody
    {
        [JsonPropertyName("tag")]
        public string? Tag { get; set; }

        [JsonPropertyName("contentStorage")]
        public string? ContentStorage { get; set; }

        [JsonPropertyName("editorFormat")]
        public string? EditorFormat { get; set; }

        [JsonPropertyName("chunkPolicy")]
        public string? ChunkPolicy { get; set; }

        [JsonPropertyName("embeddedFileStorage")]
        public string? EmbeddedFileStorage { get; set; }

        [JsonPropertyName("supportsImages")]
        public bool SupportsImages { get; set; }

        [JsonPropertyName("publishedVersion")]
        public int PublishedVersion { get; set; }
    }
}
