using System.Linq.Expressions;
using BusinessEntity.Core.Contracts;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Connectors
{
    /// <summary>
    /// Публичный connector mini-app хранения данных.
    /// Нужен сервисам как компактная точка доступа вместо прямых репозиториев.
    /// </summary>
    public interface IDataProviderConnector
    {
        /// <summary>
        /// Возвращает записи указанного типа с optional filter и take.
        /// </summary>
        Task<IReadOnlyList<T>> GetAllAsync<T>(Expression<Func<T, bool>>? filter = null, int? take = null, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity;

        /// <summary>
        /// Возвращает запись указанного типа по идентификатору.
        /// </summary>
        Task<T?> GetByIdAsync<T>(Guid id, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity;

        /// <summary>
        /// Проверяет существование записи указанного типа.
        /// </summary>
        Task<bool> ExistsAsync<T>(Guid id, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity;

        /// <summary>
        /// Добавляет новую запись указанного типа.
        /// </summary>
        Task<T> AddAsync<T>(T entity, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity;

        /// <summary>
        /// Обновляет существующую запись указанного типа.
        /// </summary>
        Task UpdateAsync<T>(T entity, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity;

        /// <summary>
        /// Удаляет запись указанного типа по идентификатору.
        /// </summary>
        Task DeleteAsync<T>(Guid id, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity;

        /// <summary>
        /// Возвращает количество записей указанного типа.
        /// </summary>
        Task<int> GetCountAsync<T>(CancellationToken cancellationToken = default)
            where T : class, IBaseEntity;

        /// <summary>
        /// Полностью очищает хранилище указанного типа.
        /// </summary>
        Task DeleteAllAsync<T>(CancellationToken cancellationToken = default)
            where T : class, IBaseEntity;
    }
}
