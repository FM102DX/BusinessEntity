using BusinessEntity.MiniApps.ActivityMiniApp.Contracts;

namespace BusinessEntity.MiniApps.ActivityMiniApp.Contracts.Connectors;

/// <summary>
/// Connector для адресного доступа к ActivityMiniApp из UI и других mini-app.
/// </summary>
public interface IActivityConnector
{
    // Возвращает комментарии указанной BusinessEntity.
    Task<IReadOnlyList<BusinessEntityCommentRecord>> GetCommentsAsync(
        Guid businessEntityId,
        CancellationToken cancellationToken = default);

    // Создает комментарий или ответ к указанной BusinessEntity.
    Task<BusinessEntityCommentRecord> CreateCommentAsync(
        BusinessEntityCommentCreateRequest request,
        CancellationToken cancellationToken = default);
}
