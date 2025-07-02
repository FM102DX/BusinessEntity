using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Threading;
using BusinessEntity.Core.Contracts;

namespace BusinessEntity.Core.Repositories
{
    public class InMemoryRepository<T> : IAsyncRepository<T> where T : class, IBaseEntity
    {
        private readonly ConcurrentDictionary<Guid, T> _storage = new();
        private readonly object _lock = new object();

        public Task<IReadOnlyList<T>> GetAllAsync(
            Expression<Func<T, bool>>? filter = null,
            int? take = null,
            CancellationToken ct = default)
        {
            lock (_lock)
            {
                var query = _storage.Values.AsQueryable();
                
                if (filter != null)
                {
                    query = query.Where(filter);
                }
                
                if (take.HasValue)
                {
                    query = query.OrderByDescending(x => x.CreatedDate).Take(take.Value);
                }
                
                var result = query.ToList();
                return Task.FromResult<IReadOnlyList<T>>(result);
            }
        }

        public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            _storage.TryGetValue(id, out var entity);
            return Task.FromResult(entity);
        }

        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(_storage.ContainsKey(id));
        }

        public Task<T> AddAsync(T entity, CancellationToken ct = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            lock (_lock)
            {
                entity.CreatedDate = DateTime.UtcNow;
                entity.LastModifiedDate = DateTime.UtcNow;
                
                if (!_storage.TryAdd(entity.Id, entity))
                {
                    throw new InvalidOperationException($"Entity with ID {entity.Id} already exists");
                }
                
                return Task.FromResult(entity);
            }
        }

        public Task UpdateAsync(T entity, CancellationToken ct = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            lock (_lock)
            {
                if (!_storage.ContainsKey(entity.Id))
                {
                    throw new KeyNotFoundException($"Entity with ID {entity.Id} not found");
                }
                
                entity.LastModifiedDate = DateTime.UtcNow;
                _storage[entity.Id] = entity;
                return Task.CompletedTask;
            }
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            lock (_lock)
            {
                if (!_storage.TryRemove(id, out _))
                {
                    throw new KeyNotFoundException($"Entity with ID {id} not found");
                }
                
                return Task.CompletedTask;
            }
        }

        public Task<int> GetCountAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_storage.Count);
        }

        public Task DeleteAllAsync(CancellationToken ct = default)
        {
            lock (_lock)
            {
                _storage.Clear();
                return Task.CompletedTask;
            }
        }
    }
} 