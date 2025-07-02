using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace BusinessEntity.Core.Contracts
{
    public interface IAsyncRepository<T> where T : IBaseEntity
    {
        Task<IReadOnlyList<T>> GetAllAsync(
            Expression<Func<T, bool>>? filter = null,
            int? take = null,
            CancellationToken ct = default);

        Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

        Task<T> AddAsync(T entity, CancellationToken ct = default);
        Task UpdateAsync(T entity, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
        
        // Utility methods
        Task<int> GetCountAsync(CancellationToken ct = default);
        Task DeleteAllAsync(CancellationToken ct = default);
    }
} 