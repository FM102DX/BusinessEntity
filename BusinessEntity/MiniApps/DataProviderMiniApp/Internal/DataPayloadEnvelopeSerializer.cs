using BusinessEntity.Core.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal;

// Формирует и читает канонический versioned JSON envelope для payload BusinessEntityData.
internal static class DataPayloadEnvelopeSerializer
{
    private const int CurrentSchemaVersion = 1;

    // Заворачивает raw payload JSON в storage-envelope с версией схемы и логическим kind.
    public static string CreateEnvelopeJson(BusinessEntityDto entity, string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var kind = GetKind(entity.EntityType);
        var payload = CreateEnvelopePayload(entity.EntityType, payloadJson, kind);
        var envelope = new DataPayloadEnvelopeRaw
        {
            SchemaVersion = CurrentSchemaVersion,
            Kind = kind,
            Payload = payload
        };

        return JsonSerializer.Serialize(envelope, StorageJsonOptions.Default);
    }

    // Достает typed payload из storage-envelope и выполняет минимальную валидацию формата.
    public static T? DeserializePayload<T>(string envelopeJson)
    {
        if (string.IsNullOrWhiteSpace(envelopeJson))
        {
            return default;
        }

        var envelope = JsonSerializer.Deserialize<DataPayloadEnvelopeRaw>(envelopeJson, StorageJsonOptions.Default)
            ?? throw new InvalidOperationException("Stored payload envelope is invalid.");

        if (envelope.SchemaVersion != CurrentSchemaVersion)
        {
            throw new NotSupportedException($"Unsupported payload schemaVersion '{envelope.SchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Kind))
        {
            throw new InvalidOperationException("Stored payload envelope does not contain kind.");
        }

        if (typeof(T) == typeof(string) && string.Equals(envelope.Kind, nameof(BusinessEntityTypeEnum.Document), StringComparison.Ordinal))
        {
            return (T?)(object?)ReadDocumentText(envelope.Payload);
        }

        return JsonSerializer.Deserialize<T>(envelope.Payload.GetRawText(), StorageJsonOptions.Default);
    }

    // Возвращает компактную длину JSON-строки для логов и диагностики.
    public static int GetJsonLength(string? json)
    {
        return string.IsNullOrEmpty(json) ? 0 : json.Length;
    }

    // Преобразует payload документа из старого string-вида в объектный payload envelope.
    private static JsonElement CreateEnvelopePayload(BusinessEntityTypeEnum entityType, string payloadJson, string kind)
    {
        var normalizedPayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "null" : payloadJson;

        using var payloadDocument = JsonDocument.Parse(normalizedPayloadJson);
        if (entityType == BusinessEntityTypeEnum.Document && payloadDocument.RootElement.ValueKind == JsonValueKind.String)
        {
            return ToDetachedJsonElement(new DocumentPayloadBody
            {
                Text = payloadDocument.RootElement.GetString() ?? string.Empty,
                Tag = kind
            });
        }

        return payloadDocument.RootElement.Clone();
    }

    // Читает текст документа из object-payload внутри envelope.
    private static string ReadDocumentText(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("text", out var textElement))
        {
            return textElement.GetString() ?? string.Empty;
        }

        if (payload.ValueKind == JsonValueKind.String)
        {
            return payload.GetString() ?? string.Empty;
        }

        throw new InvalidOperationException("Document payload envelope does not contain text.");
    }

    // Возвращает стабильный storage-kind, независимый от CLR type name.
    private static string GetKind(BusinessEntityTypeEnum entityType)
    {
        return entityType == BusinessEntityTypeEnum.Undefined ? "Undefined" : entityType.ToString();
    }

    // Создает detached JsonElement, который безопасно можно вложить в envelope-объект.
    private static JsonElement ToDetachedJsonElement<T>(T value)
    {
        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(value, StorageJsonOptions.Default));
        return jsonDocument.RootElement.Clone();
    }

    // Raw-envelope storage contract с фиксированными именами JSON-ключей.
    private sealed class DataPayloadEnvelopeRaw
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("payload")]
        public JsonElement Payload { get; set; }
    }

    // Временный payload-контракт документа для совместимости с текущим вызовом UpdateDataAsync(id, string).
    private sealed class DocumentPayloadBody
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("tag")]
        public string Tag { get; set; } = string.Empty;
    }
}
