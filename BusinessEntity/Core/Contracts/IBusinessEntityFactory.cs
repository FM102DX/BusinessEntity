using System;
using BusinessEntity.Core.Classes;

// Контракт фабрики для создания entity и typed entity
namespace BusinessEntity.Core.Contracts
{
    // Инкапсулирует правила создания базовой сущности и payload
    public interface IBusinessEntityFactory
    {
        // Создает простую entity без data-объекта
        global::BusinessEntity.Core.Classes.BusinessEntity Create(BusinessEntityTypeEnum type, string? name = null);
        // Создает typed entity по типу payload
        global::BusinessEntity.Core.Classes.BusinessEntity<TData> Create<TData>(string? name = null) where TData : class, IBusinessEntityData, new();
        // Создает typed entity с явно заданным типом
        global::BusinessEntity.Core.Classes.BusinessEntity<TData> Create<TData>(BusinessEntityTypeEnum type, string? name = null) where TData : class, IBusinessEntityData, new();
        // Создает typed entity с готовым экземпляром payload
        global::BusinessEntity.Core.Classes.BusinessEntity<TData> Create<TData>(BusinessEntityTypeEnum type, TData data, string? name = null) where TData : class, IBusinessEntityData;
        // Создает typed entity по runtime-типу payload
        IBusinessEntity CreateWithData(Type dataType, string? name = null);
    }
}
