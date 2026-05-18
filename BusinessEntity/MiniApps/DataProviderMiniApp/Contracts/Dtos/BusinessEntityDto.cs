using BusinessEntity.Core.Classes;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

public class BusinessEntityDto : BaseEntity
{
    public Guid? CreatedByUserId { get; set; }
    public Guid? LastModifiedByUserId { get; set; }
    public bool IsPublic { get; set; }
    public string Name { get; set; } = string.Empty;
    public BusinessEntityTypeEnum BusinessEntityType { get; set; } = BusinessEntityTypeEnum.Undefined;
    public BusinessEntityTypeEnum EntityType { get; set; } = BusinessEntityTypeEnum.Undefined;
}
