using BusinessEntity.MiniApps.ActivityMiniApp.Contracts;
using BusinessEntity.MiniApps.ActivityMiniApp.Internal;

namespace BusinessEntity.MiniApps.ActivityMiniApp.Facade;

/// <summary>
/// Фасад ActivityMiniApp для внешнего кода приложения.
/// </summary>
internal sealed class ActivityMiniApp : IActivityMiniApp
{
    private readonly ActivityCommentService _commentService;

    // Получает внутренний сервис комментариев mini-app.
    public ActivityMiniApp(ActivityCommentService commentService)
    {
        _commentService = commentService;
    }

    // Даёт явную точку startup-инициализации mini-app.
    public void EnsureInitialized()
    {
    }

    // Возвращает комментарии указанной BusinessEntity.
    public Task<IReadOnlyList<BusinessEntityCommentRecord>> GetCommentsAsync(
        Guid businessEntityId,
        CancellationToken cancellationToken = default)
    {
        return _commentService.GetCommentsAsync(businessEntityId, cancellationToken);
    }

    // Создает комментарий или ответ к указанной BusinessEntity.
    public Task<BusinessEntityCommentRecord> CreateCommentAsync(
        BusinessEntityCommentCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        return _commentService.CreateCommentAsync(request, cancellationToken);
    }
}
