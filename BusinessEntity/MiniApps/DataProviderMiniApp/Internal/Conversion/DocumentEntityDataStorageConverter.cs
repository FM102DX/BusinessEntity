using System.Text.Json.Serialization;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.DomainEntities;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal.Conversion;

// Конвертер payload документа: хранит только текст и tag без общих entity-метаданных.
internal sealed class DocumentEntityDataStorageConverter : EntityDataStorageConverterBase<Document>
{
    // Документный payload привязан к типу Document.
    public override BusinessEntityTypeEnum SupportedType => BusinessEntityTypeEnum.Document;

    // Формирует компактное storage-body документа.
    public override string SerializePayload(Document data)
    {
        return SerializeBody(new DocumentPayloadBody
        {
            Text = data.Text ?? string.Empty,
            Tag = data.Tag
        });
    }

    // Восстанавливает typed документный payload из storage-body.
    public override Document DeserializePayload(string payloadJson)
    {
        var body = DeserializeBody<DocumentPayloadBody>(payloadJson);
        return new Document
        {
            Text = body.Text ?? string.Empty,
            Tag = body.Tag ?? string.Empty
        };
    }

    // Внутренний контракт JSON-тела документа в storage.
    private sealed class DocumentPayloadBody
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("tag")]
        public string? Tag { get; set; }
    }
}
