using BusinessEntity.Core.Classes;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

/// <summary>
/// DTO узла графа для хранения базовых атрибутов бизнес-объекта.
/// </summary>
public class BusinessEntityDto : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public BusinessEntityTypeEnum BusinessEntityType { get; set; } = BusinessEntityTypeEnum.Undefined;
    public BusinessEntityTypeEnum EntityType { get; set; } = BusinessEntityTypeEnum.Undefined;
}
