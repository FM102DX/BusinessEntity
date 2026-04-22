using BusinessEntity.Core.Classes;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;

/// <summary>
/// Реальный CRUD-контракт mini-app поверх DTO-хранилища.
/// Этот сервис можно внедрять напрямую в классы, которым нужен обход без фасада/инициализатора.
/// </summary>
public interface IDataProviderCrudService
{
    Task<IReadOnlyList<BusinessEntity.Core.Classes.BusinessEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<BusinessEntity.Core.Classes.BusinessEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<T?> GetDataAsync<T>(Guid id, CancellationToken cancellationToken = default);
    Task UpdateDataAsync<T>(Guid id, T data, CancellationToken cancellationToken = default);
    Task<byte[]?> GetDataPayloadAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateDataPayloadAsync(Guid id, byte[] payload, CancellationToken cancellationToken = default);
    Task<BusinessEntity.Core.Classes.BusinessEntity> AddAsync(BusinessEntity.Core.Classes.BusinessEntity entityData, CancellationToken cancellationToken = default);
    Task UpdateAsync(BusinessEntity.Core.Classes.BusinessEntity entityData, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task ClearAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessEntityRelation>> GetAllRelationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessEntityRelation>> GetRelationsAsync(Guid objectAId, Guid objectBId, CancellationToken cancellationToken = default);
    Task<BusinessEntityRelation?> GetRelationByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BusinessEntityRelation> CreateRelationAsync(BusinessEntityRelation relation, CancellationToken cancellationToken = default);
    Task UpdateRelationAsync(BusinessEntityRelation relation, CancellationToken cancellationToken = default);
    Task DeleteRelationAsync(Guid id, CancellationToken cancellationToken = default);
}
