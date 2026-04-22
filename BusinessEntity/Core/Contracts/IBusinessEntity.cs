using System;
using BusinessEntity.Core.Classes;

// Контракт легковесной бизнес-сущности дерева
namespace BusinessEntity.Core.Contracts
{
    // Описывает поля базовой entity без тяжелого payload
    public interface IBusinessEntity
    {
        // Идентификатор сущности
        Guid Id { get; set; }
        // Дата создания сущности
        DateTime CreatedDate { get; set; }
        // Дата последнего изменения сущности
        DateTime LastModifiedDate { get; set; }
        // Отображаемое имя сущности
        string Name { get; set; }
        // Совместимое имя типа для старого кода
        BusinessEntityTypeEnum BusinessEntityType { get; set; }
        // Основной тип сущности
        BusinessEntityTypeEnum EntityType { get; set; }
    }
}
