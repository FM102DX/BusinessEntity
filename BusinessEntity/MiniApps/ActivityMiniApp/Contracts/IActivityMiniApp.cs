namespace BusinessEntity.MiniApps.ActivityMiniApp.Contracts;

/// <summary>
/// Публичный контракт activity mini-app для работы с комментариями и будущими активностями.
/// </summary>
public interface IActivityMiniApp
{
    // Даёт явную точку startup-инициализации mini-app.
    void EnsureInitialized();

    // Возвращает комментарии, привязанные к указанной BusinessEntity.
    Task<IReadOnlyList<BusinessEntityCommentRecord>> GetCommentsAsync(
        Guid businessEntityId,
        CancellationToken cancellationToken = default);

    // Создает новый комментарий или ответ к указанной BusinessEntity.
    Task<BusinessEntityCommentRecord> CreateCommentAsync(
        BusinessEntityCommentCreateRequest request,
        CancellationToken cancellationToken = default);
}
