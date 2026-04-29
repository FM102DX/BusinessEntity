namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

/// <summary>
/// DTO технического свойства, привязанного к BusinessEntityDataChunkDto.
/// </summary>
public class BusinessEntityDataChunkPropertyDto : BaseEntity, IPropertyDto
{
    public Guid ParentEntityId { get; set; }
    public int PropertyType { get; set; }
    public string Data { get; set; } = string.Empty;
    public string Metadata { get; set; } = string.Empty;
}
