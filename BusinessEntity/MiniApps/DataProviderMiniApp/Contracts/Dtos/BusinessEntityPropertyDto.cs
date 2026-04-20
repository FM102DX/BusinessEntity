using BusinessEntity.Core.Classes;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

/// <summary>
/// DTO атомарного свойства бизнес-объекта для адресного чтения и фильтрации.
/// </summary>
public class BusinessEntityPropertyDto : BaseEntity
{
    public Guid EntityId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string PropertyCode { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public string PropertyValue { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    public string StringValue { get; set; } = string.Empty;
    public decimal? NumberValue { get; set; }
    public DateTime? DateValue { get; set; }
    public bool? BoolValue { get; set; }
    public string JsonValue { get; set; } = string.Empty;
    public string ValueUnit { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsIndexed { get; set; }
}
