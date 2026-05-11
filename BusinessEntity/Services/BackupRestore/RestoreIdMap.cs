namespace BusinessEntity.Services.BackupRestore;

public sealed class RestoreIdMap
{
    public Guid RestoreSessionId { get; set; }

    public string SourceBackupRoot { get; set; } = string.Empty;

    public Guid SourceSpaceId { get; set; }

    public Guid TargetSpaceId { get; set; }

    public Dictionary<Guid, Guid> Entities { get; } = new();

    public Dictionary<Guid, Guid> DataItems { get; } = new();

    public Dictionary<Guid, Guid> Chunks { get; } = new();

    public Dictionary<Guid, Guid> Properties { get; } = new();

    public Dictionary<Guid, Guid> Relations { get; } = new();

    public Guid GetRequiredEntityId(Guid sourceId)
    {
        return Entities.TryGetValue(sourceId, out var targetId)
            ? targetId
            : throw new InvalidOperationException($"Restore map does not contain entity id '{sourceId}'.");
    }

    public Guid GetOrCreateDataId(Guid sourceId)
    {
        return GetOrCreate(DataItems, sourceId);
    }

    public Guid GetOrCreateChunkId(Guid sourceId)
    {
        return GetOrCreate(Chunks, sourceId);
    }

    public Guid GetOrCreatePropertyId(Guid sourceId)
    {
        return GetOrCreate(Properties, sourceId);
    }

    public Guid GetOrCreateRelationId(Guid sourceId)
    {
        return GetOrCreate(Relations, sourceId);
    }

    private static Guid GetOrCreate(IDictionary<Guid, Guid> map, Guid sourceId)
    {
        if (map.TryGetValue(sourceId, out var targetId))
        {
            return targetId;
        }

        targetId = Guid.NewGuid();
        map[sourceId] = targetId;
        return targetId;
    }
}
