namespace BusinessEntity.Services.BackupRestore;

public sealed class BusinessEntityRestoreWriteTracker
{
    public HashSet<Guid> EntityIds { get; } = new();

    public HashSet<Guid> EntityPropertyIds { get; } = new();

    public HashSet<Guid> DataIds { get; } = new();

    public HashSet<Guid> DataPropertyIds { get; } = new();

    public HashSet<Guid> ChunkIds { get; } = new();

    public HashSet<Guid> ChunkPropertyIds { get; } = new();

    public HashSet<Guid> RelationIds { get; } = new();

    public List<string> StorageFolders { get; } = new();
}
