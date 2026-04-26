using BusinessEntity.Core.Classes;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Conversion;

// Разрешает нужный payload-конвертер по типу business-объекта или storage-kind.
public interface IEntityDataStorageConverterFactory
{
    // Возвращает обязательный конвертер по enum-типу сущности.
    IEntityDataStorageConverter GetRequiredConverter(BusinessEntityTypeEnum entityType);

    // Возвращает обязательный конвертер по строковому kind из envelope.
    IEntityDataStorageConverter GetRequiredConverter(string storageKind);
}
