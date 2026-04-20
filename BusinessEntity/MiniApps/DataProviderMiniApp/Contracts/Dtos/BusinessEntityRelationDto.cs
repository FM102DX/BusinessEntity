using BusinessEntity.Core.Classes;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

/// <summary>
/// DTO ребра графа для хранения типизированной связи между двумя объектами.
/// </summary>
public class BusinessEntityRelationDto : BaseEntity
{
    public Guid ObjectAId { get; set; }
    public Guid ObjectBId { get; set; }
    public string RelationType { get; set; } = string.Empty;
    public string RelationParams { get; set; } = string.Empty;
}
