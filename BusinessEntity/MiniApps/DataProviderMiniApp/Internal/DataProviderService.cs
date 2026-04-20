using System.Linq.Expressions;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal
{
    /// <summary>
    /// Внутренний сервис mini-app, который выполняет реальные CRUD-операции над поддерживаемыми хранилищами.
    /// </summary>
    internal sealed class DataProviderService
    {
        private readonly DataProviderState _state;

        /// <summary>
        /// Получает доступ к внутреннему состоянию mini-app.
        /// </summary>
        public DataProviderService(DataProviderState state)
        {
            _state = state;
        }

        /// <summary>
        /// Возвращает список записей указанного типа.
        /// </summary>
        public Task<IReadOnlyList<T>> GetAllAsync<T>(Expression<Func<T, bool>>? filter = null, int? take = null, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            return _state.GetRepository<T>().GetAllAsync(filter, take, cancellationToken);
        }

        /// <summary>
        /// Возвращает запись указанного типа по идентификатору.
        /// </summary>
        public Task<T?> GetByIdAsync<T>(Guid id, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            return _state.GetRepository<T>().GetByIdAsync(id, cancellationToken);
        }

        /// <summary>
        /// Проверяет существование записи указанного типа.
        /// </summary>
        public Task<bool> ExistsAsync<T>(Guid id, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            return _state.GetRepository<T>().ExistsAsync(id, cancellationToken);
        }

        /// <summary>
        /// Добавляет запись указанного типа.
        /// </summary>
        public Task<T> AddAsync<T>(T entity, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            return _state.GetRepository<T>().AddAsync(entity, cancellationToken);
        }

        /// <summary>
        /// Обновляет запись указанного типа.
        /// </summary>
        public Task UpdateAsync<T>(T entity, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            return _state.GetRepository<T>().UpdateAsync(entity, cancellationToken);
        }

        /// <summary>
        /// Удаляет запись указанного типа по идентификатору.
        /// </summary>
        public Task DeleteAsync<T>(Guid id, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            return _state.GetRepository<T>().DeleteAsync(id, cancellationToken);
        }

        /// <summary>
        /// Возвращает количество записей указанного типа.
        /// </summary>
        public Task<int> GetCountAsync<T>(CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            return _state.GetRepository<T>().GetCountAsync(cancellationToken);
        }

        /// <summary>
        /// Полностью очищает хранилище указанного типа.
        /// </summary>
        public Task DeleteAllAsync<T>(CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            return _state.GetRepository<T>().DeleteAllAsync(cancellationToken);
        }
    }
}
