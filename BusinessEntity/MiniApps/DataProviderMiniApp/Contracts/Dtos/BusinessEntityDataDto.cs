using BusinessEntity.Core.Classes;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

/// <summary>
/// DTO сериализованного бизнес-объекта или крупного payload-блока.
/// </summary>
public class BusinessEntityDataDto : BaseEntity
{
    public Guid BusinessEntityId { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
