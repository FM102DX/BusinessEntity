using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

/// <summary>
/// Общий контракт storage-property, привязанной к строке родительской DTO-таблицы.
/// </summary>
public interface IPropertyDto : IBaseEntity
{
    Guid ParentEntityId { get; set; }
    int PropertyType { get; set; }
    string Data { get; set; }
    string Metadata { get; set; }
}
