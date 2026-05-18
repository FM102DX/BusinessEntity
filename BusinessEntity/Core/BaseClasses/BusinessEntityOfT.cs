using System;
using BusinessEntity.Core.Contracts;

// Typed-обертка для entity с привязанным data-объектом
namespace BusinessEntity.Core.Classes;

// Объединяет легковесную entity и тяжелый payload одного Id
public class BusinessEntity<T> : BusinessEntity where T : class, IBusinessEntityData
{
    // Внутреннее хранилище data-объекта
    private T _data = default!;

    // Typed payload сущности
    public T Data
    {
        get => _data;
        set
        {
            _data = value ?? throw new ArgumentNullException(nameof(value));
            SynchronizeDataWithEntity();
        }
    }

    // Подключает готовый data-объект к сущности
    public void AttachData(T data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    // Синхронизирует общие поля entity и data
    public void SynchronizeDataWithEntity()
    {
        // Если payload еще не задан, синхронизация не нужна
        if (_data == null)
        {
            return;
        }

        // Выравниваем identity и время изменения
        _data.Id = Id;
        _data.LastModifiedDate = LastModifiedDate;

        if (_data is BusinessEntityData businessEntityData)
        {
            businessEntityData.CreatedByUserId = CreatedByUserId;
            businessEntityData.LastModifiedByUserId = LastModifiedByUserId;
        }

        // Переносим дату создания только один раз
        if (_data.CreatedDate == default)
        {
            _data.CreatedDate = CreatedDate;
        }

        // Если имя в payload пустое, берем имя entity
        if (string.IsNullOrWhiteSpace(_data.Name))
        {
            _data.Name = Name;
        }

        // Если тип payload не задан, берем тип entity
        if (_data.EntityType == BusinessEntityTypeEnum.Undefined)
        {
            _data.EntityType = EntityType;
        }
    }
}
