namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

/// <summary>
/// DTO комментария, привязанного к любому BusinessEntity.
/// </summary>
public sealed class BusinessEntityCommentDto : BaseEntity
{
    public Guid BusinessEntityId { get; set; }
    public Guid? ParentId { get; set; }
    public string Data { get; set; } = string.Empty;
}
