using System.Text.Json.Serialization;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.DomainEntities;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal.Conversion;

// Конвертер payload системных параметров: хранит прикладные поля настроек и tag.
internal sealed class SysParametersEntityDataStorageConverter : EntityDataStorageConverterBase<SysParameters>
{
    // Payload системных параметров привязан к типу SysParametersTp.
    public override BusinessEntityTypeEnum SupportedType => BusinessEntityTypeEnum.SysParametersTp;

    // Сериализует полезные поля системных параметров без entity-метаданных.
    public override string SerializePayload(SysParameters data)
    {
        return SerializeBody(new SysParametersPayloadBody
        {
            CompanyName = data.CompanyName ?? string.Empty,
            RichTextChunkCharLimit = data.RichTextChunkCharLimit,
            RichTextInitialChunkCount = data.RichTextInitialChunkCount,
            RichTextTableOfContentsBeforeBuffer = data.RichTextTableOfContentsBeforeBuffer,
            RichTextTableOfContentsAfterBuffer = data.RichTextTableOfContentsAfterBuffer,
            RichTextScrollPreviousChunkCount = data.RichTextScrollPreviousChunkCount,
            RichTextHideTableOfContentsScrollbar = data.RichTextHideTableOfContentsScrollbar,
            RichTextEditChunksBeforeFocused = data.RichTextEditChunksBeforeFocused,
            RichTextEditChunksAfterFocused = data.RichTextEditChunksAfterFocused,
            RichTextEditChunksOnOpen = data.RichTextEditChunksOnOpen,
            Tag = data.Tag
        });
    }

    // Восстанавливает typed payload системных параметров.
    public override SysParameters DeserializePayload(string payloadJson)
    {
        var body = DeserializeBody<SysParametersPayloadBody>(payloadJson);
        return new SysParameters
        {
            CompanyName = body.CompanyName ?? string.Empty,
            RichTextChunkCharLimit = body.RichTextChunkCharLimit <= 0 ? 12000 : body.RichTextChunkCharLimit,
            RichTextInitialChunkCount = body.RichTextInitialChunkCount <= 0 ? 2 : body.RichTextInitialChunkCount,
            RichTextTableOfContentsBeforeBuffer = body.RichTextTableOfContentsBeforeBuffer < 0 ? 2 : body.RichTextTableOfContentsBeforeBuffer,
            RichTextTableOfContentsAfterBuffer = body.RichTextTableOfContentsAfterBuffer < 0 ? 5 : body.RichTextTableOfContentsAfterBuffer,
            RichTextScrollPreviousChunkCount = body.RichTextScrollPreviousChunkCount < 0 ? 1 : body.RichTextScrollPreviousChunkCount,
            RichTextHideTableOfContentsScrollbar = body.RichTextHideTableOfContentsScrollbar,
            RichTextEditChunksBeforeFocused = body.RichTextEditChunksBeforeFocused < 0 ? 1 : body.RichTextEditChunksBeforeFocused,
            RichTextEditChunksAfterFocused = body.RichTextEditChunksAfterFocused < 0 ? 1 : body.RichTextEditChunksAfterFocused,
            RichTextEditChunksOnOpen = body.RichTextEditChunksOnOpen <= 0 ? 2 : body.RichTextEditChunksOnOpen,
            Tag = body.Tag ?? string.Empty
        };
    }

    // Внутренний storage-контракт payload тела системных параметров.
    private sealed class SysParametersPayloadBody
    {
        [JsonPropertyName("companyName")]
        public string? CompanyName { get; set; }

        [JsonPropertyName("tag")]
        public string? Tag { get; set; }

        [JsonPropertyName("richTextChunkCharLimit")]
        public int RichTextChunkCharLimit { get; set; } = 12000;

        [JsonPropertyName("richTextInitialChunkCount")]
        public int RichTextInitialChunkCount { get; set; } = 2;

        [JsonPropertyName("richTextTableOfContentsBeforeBuffer")]
        public int RichTextTableOfContentsBeforeBuffer { get; set; } = 2;

        [JsonPropertyName("richTextTableOfContentsAfterBuffer")]
        public int RichTextTableOfContentsAfterBuffer { get; set; } = 5;

        [JsonPropertyName("richTextScrollPreviousChunkCount")]
        public int RichTextScrollPreviousChunkCount { get; set; } = 1;

        [JsonPropertyName("richTextHideTableOfContentsScrollbar")]
        public bool RichTextHideTableOfContentsScrollbar { get; set; } = true;

        [JsonPropertyName("richTextEditChunksBeforeFocused")]
        public int RichTextEditChunksBeforeFocused { get; set; } = 1;

        [JsonPropertyName("richTextEditChunksAfterFocused")]
        public int RichTextEditChunksAfterFocused { get; set; } = 1;

        [JsonPropertyName("richTextEditChunksOnOpen")]
        public int RichTextEditChunksOnOpen { get; set; } = 2;
    }
}
