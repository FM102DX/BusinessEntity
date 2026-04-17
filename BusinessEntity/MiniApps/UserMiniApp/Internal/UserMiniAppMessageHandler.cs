using BusinessEntity.MiniApps.UserMiniApp.Contracts.Messages;
using ReactiveUI;

namespace BusinessEntity.MiniApps.UserMiniApp.Internal
{
    internal sealed class UserMiniAppMessageHandler : IDisposable
    {
        private readonly IMessageBus _messageBus;
        private readonly UserMiniAppService _userMiniAppService;
        private readonly ILogger<UserMiniAppMessageHandler> _logger;
        private IDisposable? _subscription;

        public UserMiniAppMessageHandler(
            IMessageBus messageBus,
            UserMiniAppService userMiniAppService,
            ILogger<UserMiniAppMessageHandler> logger)
        {
            _messageBus = messageBus;
            _userMiniAppService = userMiniAppService;
            _logger = logger;
        }

        public void EnsureSubscribed()
        {
            if (_subscription != null)
            {
                return;
            }

            _subscription = _messageBus
                .Listen<GetUserRequest>()
                .Subscribe(request => _ = HandleGetUserAsync(request));

            _logger.LogInformation("UserMiniApp subscribed to GetUserRequest messages.");
        }

        private async Task HandleGetUserAsync(GetUserRequest request)
        {
            try
            {
                var user = await _userMiniAppService.GetCurrentUserAsync();
                _messageBus.SendMessage(new GetUserResponse(request.RequestId, user));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve user for GetUserRequest {RequestId}.", request.RequestId);
                _messageBus.SendMessage(new GetUserResponse(request.RequestId, null));
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
