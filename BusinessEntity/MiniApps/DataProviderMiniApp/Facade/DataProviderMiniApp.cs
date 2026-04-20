using System.Linq.Expressions;
using BusinessEntity.Core.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Internal;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Facade
{
    /// <summary>
    /// Фасад mini-app хранения данных.
    /// Гарантирует инициализацию bus-подписок и делегирует операции во внутренний сервис.
    /// </summary>
    internal sealed class DataProviderMiniApp : IDataProviderMiniApp
    {
        private readonly DataProviderService _dataProviderService;

        /// <summary>
        /// Инициализирует фасад mini-app и активирует message handler.
        /// </summary>
        public DataProviderMiniApp(
            DataProviderService dataProviderService,
            DataProviderMessageHandler messageHandler)
        {
            _dataProviderService = dataProviderService;
            messageHandler.EnsureSubscribed();
        }

        /// <summary>
        /// Возвращает список записей указанного типа.
        /// </summary>
        public Task<IReadOnlyList<T>> GetAllAsync<T>(Expression<Func<T, bool>>? filter = null, int? take = null, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            return _dataProviderService.GetAllAsync(filter, take, cancellationToken);
        }

        /// <summary>
        /// Возвращает запись указанного типа по идентификатору.
        /// </summary>
        public Task<T?> GetByIdAsync<T>(Guid id, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            return _dataProviderService.GetByIdAsync<T>(id, cancellationToken);
        }

        /// <summary>
        /// Проверяет существование записи указанного типа.
        /// </summary>
        public Task<bool> ExistsAsync<T>(Guid id, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            return _dataProviderService.ExistsAsync<T>(id, cancellationToken);
        }

        /// <summary>
        /// Добавляет новую запись указанного типа.
        /// </summary>
        public Task<T> AddAsync<T>(T entity, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            return _dataProviderService.AddAsync(entity, cancellationToken);
        }

        /// <summary>
        /// Обновляет существующую запись указанного типа.
        /// </summary>
        public Task UpdateAsync<T>(T entity, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            return _dataProviderService.UpdateAsync(entity, cancellationToken);
        }

        /// <summary>
        /// Удаляет запись указанного типа по идентификатору.
        /// </summary>
        public Task DeleteAsync<T>(Guid id, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            return _dataProviderService.DeleteAsync<T>(id, cancellationToken);
        }

        /// <summary>
        /// Возвращает количество записей указанного типа.
        /// </summary>
        public Task<int> GetCountAsync<T>(CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            return _dataProviderService.GetCountAsync<T>(cancellationToken);
        }

        /// <summary>
        /// Полностью очищает хранилище указанного типа.
        /// </summary>
        public Task DeleteAllAsync<T>(CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            return _dataProviderService.DeleteAllAsync<T>(cancellationToken);
        }
    }
}
