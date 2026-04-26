using System.Text.Json;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Conversion;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal.Conversion;

// Базовая реализация typed storage-конвертера с общей логикой приведения типов.
internal abstract class EntityDataStorageConverterBase<TData> : IEntityDataStorageConverter<TData>
    where TData : class, IBusinessEntityData, new()
{
    // Тип business-объекта, который обрабатывает конкретный конвертер.
    public abstract BusinessEntityTypeEnum SupportedType { get; }

    // Сериализует конкретный typed payload в raw JSON тела payload.
    public abstract string SerializePayload(TData data);

    // Десериализует raw JSON тела payload в конкретный typed payload.
    public abstract TData DeserializePayload(string payloadJson);

    // Приводит базовый payload-контракт к ожидаемому typed классу.
    string IEntityDataStorageConverter.SerializePayload(IBusinessEntityData data)
    {
        if (data is not TData typedData)
        {
            throw new InvalidOperationException(
                $"Storage converter for '{SupportedType}' cannot serialize payload of runtime type '{data.GetType().Name}'.");
        }

        return SerializePayload(typedData);
    }

    // Возвращает typed payload как базовый интерфейс для фабрики.
    IBusinessEntityData IEntityDataStorageConverter.DeserializePayload(string payloadJson)
    {
        return DeserializePayload(payloadJson);
    }

    // Сериализует внутреннее payload-body через общие storage JSON-options.
    protected static string SerializeBody<TBody>(TBody body)
    {
        return JsonSerializer.Serialize(body, StorageJsonOptions.Default);
    }

    // Десериализует внутреннее payload-body через общие storage JSON-options.
    protected static TBody DeserializeBody<TBody>(string payloadJson)
    {
        return JsonSerializer.Deserialize<TBody>(payloadJson, StorageJsonOptions.Default)
            ?? throw new InvalidOperationException("Stored payload body is invalid.");
    }
}
