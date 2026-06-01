using BusinessEntity.MiniApps.ActivityMiniApp.Contracts;
using BusinessEntity.MiniApps.ActivityMiniApp.Contracts.Connectors;

namespace BusinessEntity.MiniApps.ActivityMiniApp.Connectors;

/// <summary>
/// Connector ActivityMiniApp для компактного доступа к операциям комментариев.
/// </summary>
public sealed class ActivityConnector : IActivityConnector
{
    private readonly IActivityMiniApp _activityMiniApp;

    // Сохраняет публичный контракт ActivityMiniApp и гарантирует его инициализацию.
    public ActivityConnector(IActivityMiniApp activityMiniApp)
    {
        _activityMiniApp = activityMiniApp;
        _activityMiniApp.EnsureInitialized();
    }

    // Возвращает комментарии указанной BusinessEntity.
    public Task<IReadOnlyList<BusinessEntityCommentRecord>> GetCommentsAsync(
        Guid businessEntityId,
        CancellationToken cancellationToken = default)
    {
        return _activityMiniApp.GetCommentsAsync(businessEntityId, cancellationToken);
    }

    // Создает комментарий или ответ к указанной BusinessEntity.
    public Task<BusinessEntityCommentRecord> CreateCommentAsync(
        BusinessEntityCommentCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        return _activityMiniApp.CreateCommentAsync(request, cancellationToken);
    }
}
