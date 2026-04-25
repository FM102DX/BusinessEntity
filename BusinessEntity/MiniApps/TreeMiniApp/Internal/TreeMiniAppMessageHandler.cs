using BusinessEntity.MiniApps.TreeMiniApp.Contracts.Messages;
using ReactiveUI;

namespace BusinessEntity.MiniApps.TreeMiniApp.Internal
{
    // Подписывает mini-app дерева на bus-запросы и публикует ответы.
    internal sealed class TreeMiniAppMessageHandler : IDisposable
    {
        private readonly IMessageBus _messageBus;
        private readonly TreeMiniAppService _treeMiniAppService;
        private readonly ILogger<TreeMiniAppMessageHandler> _logger;
        private readonly List<IDisposable> _subscriptions = new();

        public TreeMiniAppMessageHandler(
            IMessageBus messageBus,
            TreeMiniAppService treeMiniAppService,
            ILogger<TreeMiniAppMessageHandler> logger)
        {
            _messageBus = messageBus;
            _treeMiniAppService = treeMiniAppService;
            _logger = logger;
        }

        // Инициализирует все bus-подписки mini-app.
        public void EnsureSubscribed()
        {
            if (_subscriptions.Count > 0)
            {
                return;
            }

            _subscriptions.Add(_messageBus.Listen<GetTreeForSpaceRequest>().Subscribe(request => _ = HandleGetTreeForSpaceAsync(request)));

            _logger.LogInformation("TreeMiniApp subscribed to tree messages.");
        }

        private async Task HandleGetTreeForSpaceAsync(GetTreeForSpaceRequest request)
        {
            try
            {
                var snapshot = await _treeMiniAppService.GetTreeForSpaceAsync(request.SpaceId);
                _messageBus.SendMessage(new GetTreeForSpaceResponse(request.RequestId, snapshot));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load tree for space {SpaceId}.", request.SpaceId);
                _messageBus.SendMessage(new GetTreeForSpaceResponse(request.RequestId, null, ex.Message));
            }
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
        }
    }
}
