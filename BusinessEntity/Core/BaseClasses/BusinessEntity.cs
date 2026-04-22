using BusinessEntity.Core.Contracts;

// Базовая узловая сущность дерева
namespace BusinessEntity.Core.Classes;

// Хранит легковесные поля бизнес-сущности без тяжелого payload
public class BusinessEntity: IBusinessEntity
{
    // Идентификатор сущности
    public Guid Id { get; set; } = Guid.NewGuid();
    // Дата создания сущности
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    // Дата последнего изменения сущности
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
    // Отображаемое имя сущности
    public string Name { get; set; } = string.Empty;
    // Совместимое имя типа для старого кода
    public virtual BusinessEntityTypeEnum BusinessEntityType { get; set; } = BusinessEntityTypeEnum.Undefined;
    // Основной тип сущности
    public BusinessEntityTypeEnum EntityType { get; set; } = BusinessEntityTypeEnum.Undefined;
}
