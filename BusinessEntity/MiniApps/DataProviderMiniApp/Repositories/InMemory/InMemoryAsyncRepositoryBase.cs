using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using BusinessEntity.Core.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.InMemory;

/// <summary>
/// Базовая in-memory реализация generic-репозитория mini-app хранения данных.
/// </summary>
public abstract class InMemoryAsyncRepositoryBase<T> : IAsyncRepository<T> where T : class, IBaseEntity
{
    private static readonly PropertyInfo? VersionProperty = typeof(T).GetProperty("Version", typeof(int));
    private readonly ConcurrentDictionary<string, T> _storage = new();
    private readonly object _syncRoot = new();

    // Читает список записей из in-memory хранилища текущего DTO-типа.
    public Task<IReadOnlyList<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null, int? take = null, CancellationToken ct = default)
    {
        lock (_syncRoot)
        {
            IEnumerable<T> query = _storage.Values;

            if (filter != null)
            {
                query = query.Where(filter.Compile());
            }

            query = query.OrderByDescending(x => x.CreatedDate);

            if (take.HasValue)
            {
                query = query.Take(take.Value);
            }

            return Task.FromResult<IReadOnlyList<T>>(query.ToList());
        }
    }

    // Читает страницу записей с явным order/skip/take без доменной специфики.
    public Task<IReadOnlyList<T>> GetPageAsync<TKey>(
        Expression<Func<T, bool>>? filter,
        Expression<Func<T, TKey>> orderBy,
        bool descending = false,
        int skip = 0,
        int? take = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(orderBy);

        lock (_syncRoot)
        {
            IEnumerable<T> query = _storage.Values;
            if (filter != null)
            {
                query = query.Where(filter.Compile());
            }

            var keySelector = orderBy.Compile();
            query = descending
                ? query.OrderByDescending(keySelector)
                : query.OrderBy(keySelector);

            if (skip > 0)
            {
                query = query.Skip(skip);
            }

            if (take.HasValue)
            {
                query = query.Take(take.Value);
            }

            return Task.FromResult<IReadOnlyList<T>>(query.ToList());
        }
    }

    // Читает одну запись текущего DTO-типа по id.
    public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        lock (_syncRoot)
        {
            var entity = _storage.Values
                .Where(x => x.Id == id)
                .OrderByDescending(GetVersion)
                .ThenByDescending(x => x.LastModifiedDate)
                .FirstOrDefault();
            return Task.FromResult(entity);
        }
    }

    // Проверяет наличие записи в in-memory хранилище по id.
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult(_storage.Values.Any(x => x.Id == id));
    }

    // Добавляет новую DTO-запись в in-memory хранилище.
    public Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        lock (_syncRoot)
        {
            entity.CreatedDate = entity.CreatedDate == default ? DateTime.UtcNow : entity.CreatedDate;
            entity.LastModifiedDate = DateTime.UtcNow;

            if (!_storage.TryAdd(BuildStorageKey(entity), entity))
            {
                throw new InvalidOperationException($"EntityData with id '{entity.Id}' and version '{GetVersion(entity)}' already exists.");
            }

            return Task.FromResult(entity);
        }
    }

    // Обновляет существующую DTO-запись в in-memory хранилище.
    public Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        lock (_syncRoot)
        {
            var key = BuildStorageKey(entity);
            if (!_storage.ContainsKey(key))
            {
                throw new KeyNotFoundException($"EntityData with id '{entity.Id}' and version '{GetVersion(entity)}' was not found.");
            }

            entity.LastModifiedDate = DateTime.UtcNow;
            _storage[key] = entity;
            return Task.CompletedTask;
        }
    }

    // Удаляет все DTO-записи из in-memory хранилища по id.
    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        lock (_syncRoot)
        {
            foreach (var key in _storage.Where(x => x.Value.Id == id).Select(x => x.Key).ToList())
            {
                _storage.TryRemove(key, out _);
            }

            return Task.CompletedTask;
        }
    }

    // Возвращает количество записей текущего DTO-типа.
    public Task<int> GetCountAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_storage.Count);
    }

    // Возвращает количество записей текущего DTO-типа по optional-фильтру.
    public Task<int> GetCountAsync(Expression<Func<T, bool>>? filter, CancellationToken ct = default)
    {
        lock (_syncRoot)
        {
            if (filter == null)
            {
                return Task.FromResult(_storage.Count);
            }

            return Task.FromResult(_storage.Values.Count(filter.Compile()));
        }
    }

    // Полностью очищает in-memory хранилище текущего DTO-типа.
    public Task DeleteAllAsync(CancellationToken ct = default)
    {
        lock (_syncRoot)
        {
            _storage.Clear();
            return Task.CompletedTask;
        }
    }

    private static string BuildStorageKey(T entity)
    {
        return VersionProperty == null
            ? entity.Id.ToString("D")
            : $"{entity.Id:D}:{GetVersion(entity):D10}";
    }

    private static int GetVersion(T entity)
    {
        return VersionProperty?.GetValue(entity) is int version && version > 0
            ? version
            : 1;
    }
}
