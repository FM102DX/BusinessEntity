using System.Collections.Concurrent;
using System.Linq.Expressions;
using BusinessEntity.Core.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.InMemory;

/// <summary>
/// Базовая in-memory реализация generic-репозитория mini-app хранения данных.
/// </summary>
public abstract class InMemoryAsyncRepositoryBase<T> : IAsyncRepository<T> where T : class, IBaseEntity
{
    private readonly ConcurrentDictionary<Guid, T> _storage = new();
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
        _storage.TryGetValue(id, out var entity);
        return Task.FromResult(entity);
    }

    // Проверяет наличие записи в in-memory хранилище по id.
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult(_storage.ContainsKey(id));
    }

    // Добавляет новую DTO-запись в in-memory хранилище.
    public Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        lock (_syncRoot)
        {
            entity.CreatedDate = entity.CreatedDate == default ? DateTime.UtcNow : entity.CreatedDate;
            entity.LastModifiedDate = DateTime.UtcNow;

            if (!_storage.TryAdd(entity.Id, entity))
            {
                throw new InvalidOperationException($"EntityData with id '{entity.Id}' already exists.");
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
            if (!_storage.ContainsKey(entity.Id))
            {
                throw new KeyNotFoundException($"EntityData with id '{entity.Id}' was not found.");
            }

            entity.LastModifiedDate = DateTime.UtcNow;
            _storage[entity.Id] = entity;
            return Task.CompletedTask;
        }
    }

    // Удаляет DTO-запись из in-memory хранилища по id.
    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        lock (_syncRoot)
        {
            _storage.TryRemove(id, out _);
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
}
