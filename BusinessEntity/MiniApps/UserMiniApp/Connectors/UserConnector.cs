using BusinessEntity.MiniApps.UserMiniApp.Contracts;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Connectors;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Messages;
using ReactiveUI;

namespace BusinessEntity.MiniApps.UserMiniApp.Connectors
{
    public sealed class UserConnector : IUserConnector
    {
        private readonly IMessageBus _messageBus;

        public UserConnector(IMessageBus messageBus, IUserMiniApp userMiniApp)
        {
            _messageBus = messageBus;

            // MiniApp materialization ensures the message subscriptions are initialized
            // before the first connector roundtrip.
            _ = userMiniApp;
        }

        public async Task<BusinessEntityUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var completion = new TaskCompletionSource<BusinessEntityUser?>(TaskCreationOptions.RunContinuationsAsynchronously);
            IDisposable? subscription = null;

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

        public async Task<IReadOnlyList<string>> GetGroupsAsync(CancellationToken cancellationToken = default)
        {
            var user = await GetCurrentUserAsync(cancellationToken);
            return user?.Groups ?? Array.Empty<string>();
        }

        public async Task<bool> IsInGroupAsync(string groupName, CancellationToken cancellationToken = default)
        {
            var user = await GetCurrentUserAsync(cancellationToken);
            return user?.HasGroup(groupName) == true;
        }
    }
}
