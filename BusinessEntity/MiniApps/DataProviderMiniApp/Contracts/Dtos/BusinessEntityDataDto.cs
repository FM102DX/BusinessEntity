using BusinessEntity.Core.Classes;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

/// <summary>
/// DTO сериализованного бизнес-объекта или крупного payload-блока.
/// </summary>
public class BusinessEntityDataDto : BaseEntity
{
    public Guid EntityId { get; set; }
    public string Data { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string SerializationFormat { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string ContentEncoding { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public int ChunkIndex { get; set; }
    public int ChunkCount { get; set; } = 1;
    public bool IsCompressed { get; set; }
}
