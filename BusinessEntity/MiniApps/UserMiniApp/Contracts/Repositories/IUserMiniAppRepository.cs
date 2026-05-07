using System.Linq.Expressions;

namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Repositories;

// Минимальный repository-контракт user mini-app без зависимости от data-provider mini-app.
public interface IUserMiniAppRepository<T> where T : class
{
    Task<IReadOnlyList<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null, CancellationToken ct = default);
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
