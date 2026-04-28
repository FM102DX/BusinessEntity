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
    }
}
