using BusinessEntity.Core.RichText;
using BusinessEntity.MiniApps.UserMiniApp.Contracts;
using BusinessEntity.MiniApps.UserMiniApp.Internal;

namespace BusinessEntity.MiniApps.UserMiniApp.Facade
{
    // Представляет фасад mini-app и гарантирует запуск bus-подписок при первом использовании.
    internal sealed class UserMiniApp : IUserMiniApp
    {
        private readonly UserMiniAppService _userMiniAppService;
        private readonly UserMiniAppMessageHandler _messageHandler;

        // Инициализирует фасад mini-app и активирует message handler.
        public UserMiniApp(
            UserMiniAppService userMiniAppService,
            UserMiniAppMessageHandler messageHandler)
        {
            _userMiniAppService = userMiniAppService;
            _messageHandler = messageHandler;
            _messageHandler.EnsureSubscribed();
        }

        // Даёт внешнему коду явную точку для ленивой или startup-инициализации mini-app.
        public void EnsureInitialized()
        {
            _messageHandler.EnsureSubscribed();
        }

        // Делегирует получение текущего пользователя во внутренний сервис mini-app.
        public Task<BusinessEntityUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetCurrentUserAsync(cancellationToken);
        }

        public Task<Contracts.Dtos.UserDto?> EnsureCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.EnsureCurrentUserAsync(cancellationToken);
        }

        // Делегирует удаление локальной учетной записи текущего пользователя во внутренний сервис mini-app.
        public Task<bool> DeleteCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.DeleteCurrentUserAsync(cancellationToken);
        }

        public Task<IReadOnlyList<RichTextDocumentBookmark>> GetRichDocBookmarksAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetRichDocBookmarksAsync(documentId, cancellationToken);
        }

        public Task<RichTextDocumentBookmark?> AddRichDocBookmarkAsync(
            Guid documentId,
            RichTextDocumentTextSelection? selection,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.AddRichDocBookmarkAsync(documentId, selection, cancellationToken);
        }

        public Task<bool> DeleteRichDocBookmarkAsync(Guid bookmarkId, CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.DeleteRichDocBookmarkAsync(bookmarkId, cancellationToken);
        }
    }
}
