using BusinessEntity.Core.Classes;

namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Описывает контентную сущность, для которой UserMiniApp должен рассчитать права доступа.
public sealed class UserContentAccessRequest
{
    public Guid EntityId { get; set; }
    public BusinessEntityTypeEnum EntityType { get; set; }
    public bool IsCommon { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public int PublishedVersion { get; set; }
}
