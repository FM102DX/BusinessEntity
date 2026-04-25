using BusinessEntity.Core.Classes;
using BusinessEntity.MiniApps.TreeMiniApp.Contracts;
using BusinessEntity.MiniApps.TreeMiniApp.Contracts.Connectors;
using BusinessEntity.MiniApps.TreeMiniApp.Contracts.Messages;
using ReactiveUI;

namespace BusinessEntity.MiniApps.TreeMiniApp.Connectors
{
    // Bus-roundtrip connector дерева.
    public sealed class TreeConnector : ITreeConnector
    {
        private readonly IMessageBus _messageBus;

        public TreeConnector(IMessageBus messageBus, ITreeMiniApp treeMiniApp)
        {
            _messageBus = messageBus;
            treeMiniApp.EnsureInitialized();
        }

        public async Task<TreeSpaceSnapshot?> GetTreeForSpaceAsync(Guid spaceId, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<GetTreeForSpaceRequest, GetTreeForSpaceResponse>(
                new GetTreeForSpaceRequest(requestId, spaceId),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            return response.Snapshot;
        }

        private async Task<TResponse> SendAndReceiveAsync<TRequest, TResponse>(
            TRequest request,
            Func<TResponse, Guid> requestIdSelector,
            Guid expectedRequestId,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            IDisposable? subscription = null;

            subscription = _messageBus
                .Listen<TResponse>()
                .Subscribe(response =>
                {
                    if (requestIdSelector(response) != expectedRequestId)
                    {
                        return;
                    }

                    subscription?.Dispose();
                    completion.TrySetResult(response);
                });

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                subscription?.Dispose();
                completion.TrySetCanceled(cancellationToken);
            });

            _messageBus.SendMessage(request);
            return await completion.Task;
        }

        private static void EnsureNoError(string? errorMessage)
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new InvalidOperationException(errorMessage);
            }
        }
    }
}
