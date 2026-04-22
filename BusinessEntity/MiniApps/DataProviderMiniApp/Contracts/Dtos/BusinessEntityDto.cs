using BusinessEntity.Core.Classes;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

public class BusinessEntityDto : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public BusinessEntityTypeEnum BusinessEntityType { get; set; } = BusinessEntityTypeEnum.Undefined;
    public BusinessEntityTypeEnum EntityType { get; set; } = BusinessEntityTypeEnum.Undefined;
}
