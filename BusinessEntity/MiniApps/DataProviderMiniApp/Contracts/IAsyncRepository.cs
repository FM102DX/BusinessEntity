using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;

public interface IAsyncRepository<T> where T : IBaseEntity
{
    Task<IReadOnlyList<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null, int? take = null, CancellationToken ct = default);

    Task<IReadOnlyList<T>> GetPageAsync<TKey>(
        Expression<Func<T, bool>>? filter,
        Expression<Func<T, TKey>> orderBy,
        bool descending = false,
        int skip = 0,
        int? take = null,
        CancellationToken ct = default);

    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<int> GetCountAsync(CancellationToken ct = default);
    Task<int> GetCountAsync(Expression<Func<T, bool>>? filter, CancellationToken ct = default);
    Task DeleteAllAsync(CancellationToken ct = default);
} 
