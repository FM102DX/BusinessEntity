using BusinessEntity.MiniApps.UserMiniApp.Contracts;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Connectors;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Messages;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;
using BusinessEntity.Core.RichText;
using ReactiveUI;

namespace BusinessEntity.MiniApps.UserMiniApp.Connectors
{
    // Предоставляет другим модулям короткий доступ к пользователю через bus-roundtrip.
    public sealed class UserConnector : IUserConnector
    {
        private readonly IMessageBus _messageBus;
        private readonly IUserMiniApp _userMiniApp;

        // Инициализирует connector и гарантирует материализацию mini-app перед первым запросом.
        public UserConnector(IMessageBus messageBus, IUserMiniApp userMiniApp)
        {
            _messageBus = messageBus;
            _userMiniApp = userMiniApp;
            userMiniApp.EnsureInitialized();
        }

        // Отправляет GetUserRequest в bus и ждёт типизированный ответ от user mini-app.
        public async Task<BusinessEntityUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var completion = new TaskCompletionSource<BusinessEntityUser?>(TaskCreationOptions.RunContinuationsAsynchronously);
            IDisposable? subscription = null;

            // Слушаем только ответ для нашего requestId, чтобы не пересекаться с параллельными запросами.
            subscription = _messageBus
                .Listen<GetUserResponse>()
                .Subscribe(response =>
                {
                    if (response.RequestId != requestId)
                    {
                        return;
                    }

                    subscription?.Dispose();
                    completion.TrySetResult(response.User);
                });

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                subscription?.Dispose();
                completion.TrySetCanceled(cancellationToken);
            });

            _messageBus.SendMessage(new GetUserRequest(requestId));
            return await completion.Task;
        }

        public Task<UserDto?> EnsureCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniApp.EnsureCurrentUserAsync(cancellationToken);
        }

        // Удаляет локальную учетную запись текущего пользователя через публичный контракт mini-app.
        public Task<bool> DeleteCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniApp.DeleteCurrentUserAsync(cancellationToken);
        }

        // Возвращает локальных пользователей через публичный контракт user mini-app.
        public Task<IReadOnlyList<UserAdministrationRecord>> GetAdministrationUsersAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetAdministrationUsersAsync(cancellationToken);
        }

        // Создает пользователя приложения в Authentik через публичный контракт user mini-app.
        public Task<UserAdministrationRecord> CreateAdministrationUserAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniApp.CreateAdministrationUserAsync(cancellationToken);
        }

        // Обновляет локального пользователя через публичный контракт user mini-app.
        public Task<UserAdministrationRecord> UpdateAdministrationUserAsync(
            Guid userId,
            UserAdministrationSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.UpdateAdministrationUserAsync(userId, request, cancellationToken);
        }

        // Удаляет локального пользователя через публичный контракт user mini-app.
        public Task<bool> DeleteAdministrationUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return _userMiniApp.DeleteAdministrationUserAsync(userId, cancellationToken);
        }

        // Возвращает только список групп поверх общего объекта пользователя.
        public async Task<IReadOnlyList<string>> GetGroupsAsync(CancellationToken cancellationToken = default)
        {
            var user = await GetCurrentUserAsync(cancellationToken);
            return user?.Groups ?? Array.Empty<string>();
        }

        // Проверяет membership в группе поверх общего объекта пользователя.
        public async Task<bool> IsInGroupAsync(string groupName, CancellationToken cancellationToken = default)
        {
            var user = await GetCurrentUserAsync(cancellationToken);
            return user?.HasGroup(groupName) == true;
        }

        public Task<IReadOnlyList<RichTextDocumentBookmark>> GetRichDocBookmarksAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetRichDocBookmarksAsync(documentId, cancellationToken);
        }

        public Task<RichTextDocumentBookmark?> AddRichDocBookmarkAsync(
            Guid documentId,
            RichTextDocumentTextSelection? selection,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.AddRichDocBookmarkAsync(documentId, selection, cancellationToken);
        }

        public Task<bool> DeleteRichDocBookmarkAsync(Guid bookmarkId, CancellationToken cancellationToken = default)
        {
            return _userMiniApp.DeleteRichDocBookmarkAsync(bookmarkId, cancellationToken);
        }

        public Task<int> GetRichDocDisplayedLevelAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetRichDocDisplayedLevelAsync(documentId, cancellationToken);
        }

        public Task SaveRichDocDisplayedLevelAsync(
            Guid documentId,
            int displayLevelCount,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.SaveRichDocDisplayedLevelAsync(documentId, displayLevelCount, cancellationToken);
        }
    }
}
