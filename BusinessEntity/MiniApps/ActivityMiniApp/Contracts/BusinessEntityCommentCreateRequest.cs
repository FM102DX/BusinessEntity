namespace BusinessEntity.MiniApps.ActivityMiniApp.Contracts;

/// <summary>
/// Запрос на создание корневого комментария или ответа к BusinessEntity.
/// </summary>
public sealed class BusinessEntityCommentCreateRequest
{
    public Guid BusinessEntityId { get; set; }
    public Guid? ParentId { get; set; }
    public string Text { get; set; } = string.Empty;
}
