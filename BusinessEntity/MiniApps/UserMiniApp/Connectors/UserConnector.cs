using BusinessEntity.MiniApps.UserMiniApp.Contracts;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Connectors;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Messages;
using ReactiveUI;

namespace BusinessEntity.MiniApps.UserMiniApp.Connectors
{
    // Предоставляет другим модулям короткий доступ к пользователю через bus-roundtrip.
    public sealed class UserConnector : IUserConnector
    {
        private readonly IMessageBus _messageBus;

        // Инициализирует connector и гарантирует материализацию mini-app перед первым запросом.
        public UserConnector(IMessageBus messageBus, IUserMiniApp userMiniApp)
        {
            _messageBus = messageBus;

            // MiniApp materialization ensures the message subscriptions are initialized
            // before the first connector roundtrip.
            _ = userMiniApp;
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
    }
}
