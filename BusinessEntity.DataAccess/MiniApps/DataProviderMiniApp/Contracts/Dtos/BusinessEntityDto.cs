using BusinessEntity.Core.Classes;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

/// <summary>
/// DTO узла графа для хранения базовых атрибутов бизнес-объекта.
/// </summary>
public class BusinessEntityDto : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public BusinessEntityTypeEnum EntityType { get; set; } = BusinessEntityTypeEnum.Undefined;
    public BusinessEntityTypeEnum BusinessEntityType { get; set; } = BusinessEntityTypeEnum.Undefined;
}
