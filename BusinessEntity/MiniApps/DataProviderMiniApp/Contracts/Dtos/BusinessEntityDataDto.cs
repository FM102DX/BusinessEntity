namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

/// <summary>
/// DTO сериализованного бизнес-объекта или крупного payload-блока.
/// </summary>
public class BusinessEntityDataDto : BaseEntity
{
    public Guid BusinessEntityId { get; set; }
    public int Version { get; set; } = 1;
    public string Data { get; set; } = string.Empty;
}
