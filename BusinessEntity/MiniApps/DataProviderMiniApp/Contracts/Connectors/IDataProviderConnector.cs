using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Connectors
{
    /// <summary>
    /// Публичный connector mini-app хранения данных.
    /// Нужен сервисам как компактная точка доступа вместо прямых репозиториев.
    /// </summary>
    public interface IDataProviderConnector
    {
        Task<IReadOnlyList<BusinessEntity.Core.Classes.BusinessEntity>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<BusinessEntity.Core.Classes.BusinessEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<TData?> GetDataAsync<TData>(Guid id, CancellationToken cancellationToken = default)
            where TData : class, IBusinessEntityData;
        Task UpdateDataAsync<TData>(Guid id, TData data, CancellationToken cancellationToken = default)
            where TData : class, IBusinessEntityData;
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
}
