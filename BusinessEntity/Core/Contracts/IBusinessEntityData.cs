using System;
using BusinessEntity.Core.Classes;

// Контракт тяжелого data-объекта сущности
namespace BusinessEntity.Core.Contracts
{
    // Описывает поля payload-объекта, связанного с entity по Id
    public interface IBusinessEntityData
    {
        // Идентификатор data-объекта
        Guid Id { get; set; }
        // Дата создания data-объекта
        DateTime CreatedDate { get; set; }
        // Дата последнего изменения data-объекта
        DateTime LastModifiedDate { get; set; }
        // Имя data-объекта
        string Name { get; set; }
        // Тип data-объекта
        BusinessEntityTypeEnum EntityType { get; set; }
        // Дополнительная строковая метка
        string Tag { get; set; }
        // Номер версии payload-объекта в storage.
        int Version { get; set; }
        // Показывает, должен ли payload сохраняться append-only версиями.
        bool HasVersions { get; }
    }
}
