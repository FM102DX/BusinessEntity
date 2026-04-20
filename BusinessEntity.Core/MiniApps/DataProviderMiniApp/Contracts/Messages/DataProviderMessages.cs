using System.Linq.Expressions;
using BusinessEntity.Core.Contracts;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Messages
{
    /// <summary>
    /// Запрашивает список записей указанного типа.
    /// </summary>
    public sealed record GetRecordsRequest<T>(Guid RequestId, Expression<Func<T, bool>>? Filter = null, int? Take = null)
        where T : class, IBaseEntity;

    /// <summary>
    /// Возвращает список записей указанного типа.
    /// </summary>
    public sealed record GetRecordsResponse<T>(Guid RequestId, IReadOnlyList<T> Records, string? ErrorMessage = null)
        where T : class, IBaseEntity;

    /// <summary>
    /// Запрашивает запись указанного типа по идентификатору.
    /// </summary>
    public sealed record GetRecordByIdRequest<T>(Guid RequestId, Guid Id)
        where T : class, IBaseEntity;

    /// <summary>
    /// Возвращает запись указанного типа по идентификатору.
    /// </summary>
    public sealed record GetRecordByIdResponse<T>(Guid RequestId, T? Record, string? ErrorMessage = null)
        where T : class, IBaseEntity;

    /// <summary>
    /// Запрашивает проверку существования записи.
    /// </summary>
    public sealed record RecordExistsRequest<T>(Guid RequestId, Guid Id)
        where T : class, IBaseEntity;

    /// <summary>
    /// Возвращает признак существования записи.
    /// </summary>
    public sealed record RecordExistsResponse<T>(Guid RequestId, bool Exists, string? ErrorMessage = null)
        where T : class, IBaseEntity;

    /// <summary>
    /// Команда на добавление новой записи.
    /// </summary>
    public sealed record AddRecordRequest<T>(Guid RequestId, T Record)
        where T : class, IBaseEntity;

    /// <summary>
    /// Возвращает добавленную запись.
    /// </summary>
    public sealed record AddRecordResponse<T>(Guid RequestId, T? Record, string? ErrorMessage = null)
        where T : class, IBaseEntity;

    /// <summary>
    /// Команда на обновление записи.
    /// </summary>
    public sealed record UpdateRecordRequest<T>(Guid RequestId, T Record)
        where T : class, IBaseEntity;

    /// <summary>
    /// Возвращает результат обновления записи.
    /// </summary>
    public sealed record UpdateRecordResponse<T>(Guid RequestId, bool Success, string? ErrorMessage = null)
        where T : class, IBaseEntity;

    /// <summary>
    /// Команда на удаление записи по идентификатору.
    /// </summary>
    public sealed record DeleteRecordRequest<T>(Guid RequestId, Guid Id)
        where T : class, IBaseEntity;

    /// <summary>
    /// Возвращает результат удаления записи.
    /// </summary>
    public sealed record DeleteRecordResponse<T>(Guid RequestId, bool Success, string? ErrorMessage = null)
        where T : class, IBaseEntity;

    /// <summary>
    /// Запрашивает количество записей указанного типа.
    /// </summary>
    public sealed record GetRecordCountRequest<T>(Guid RequestId)
        where T : class, IBaseEntity;

    /// <summary>
    /// Возвращает количество записей указанного типа.
    /// </summary>
    public sealed record GetRecordCountResponse<T>(Guid RequestId, int Count, string? ErrorMessage = null)
        where T : class, IBaseEntity;

    /// <summary>
    /// Команда на полную очистку хранилища указанного типа.
    /// </summary>
    public sealed record DeleteAllRecordsRequest<T>(Guid RequestId)
        where T : class, IBaseEntity;

    /// <summary>
    /// Возвращает результат полной очистки хранилища указанного типа.
    /// </summary>
    public sealed record DeleteAllRecordsResponse<T>(Guid RequestId, bool Success, string? ErrorMessage = null)
        where T : class, IBaseEntity;
}
