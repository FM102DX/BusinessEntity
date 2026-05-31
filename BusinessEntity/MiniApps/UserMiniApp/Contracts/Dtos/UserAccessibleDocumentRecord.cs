using BusinessEntity.Core.Classes;

namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Описывает документ, который пользователь может открыть в выбранном пространстве.
public sealed class UserAccessibleDocumentRecord
{
    public Guid Id { get; set; }
    public Guid SpaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public BusinessEntityTypeEnum EntityType { get; set; } = BusinessEntityTypeEnum.Undefined;
}
