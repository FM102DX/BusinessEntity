namespace BusinessEntity.MiniApps.ActivityMiniApp.Contracts;

/// <summary>
/// Комментарий, подготовленный для отображения в UI.
/// </summary>
public sealed record BusinessEntityCommentRecord
{
    public Guid Id { get; init; }
    public Guid BusinessEntityId { get; init; }
    public Guid? ParentId { get; init; }
    public string Text { get; init; } = string.Empty;
    public Guid? AuthorUserId { get; init; }
    public string AuthorDisplayName { get; init; } = string.Empty;
    public DateTime CreatedDate { get; init; }
    public DateTime LastModifiedDate { get; init; }
    public int DisplayDepth { get; init; }

    // Возвращает копию комментария с нормализованным родителем и уровнем отображения.
    public BusinessEntityCommentRecord WithDisplay(Guid? parentId, int displayDepth)
    {
        return this with
        {
            ParentId = parentId,
            DisplayDepth = Math.Clamp(displayDepth, 0, 3)
        };
    }
}
