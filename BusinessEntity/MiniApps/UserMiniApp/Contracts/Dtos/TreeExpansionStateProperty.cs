namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

/// <summary>
/// Пользовательское состояние раскрытия папок дерева внутри одного пространства.
/// </summary>
public sealed class TreeExpansionStateProperty
{
    public string Kind { get; set; } = nameof(TreeExpansionStateProperty);
    public int SchemaVersion { get; set; } = 1;
    public Guid SpaceId { get; set; }
    public List<Guid> CollapsedFolderIds { get; set; } = new();
}
