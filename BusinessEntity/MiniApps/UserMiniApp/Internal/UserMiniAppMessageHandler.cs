using BusinessEntity.MiniApps.UserMiniApp.Contracts.Messages;
using ReactiveUI;

namespace BusinessEntity.MiniApps.UserMiniApp.Internal
{
    // Подписывает mini-app на bus-запросы и публикует ответы с текущим пользователем.
    internal sealed class UserMiniAppMessageHandler : IDisposable
    {
        private readonly IMessageBus _messageBus;
        private readonly UserMiniAppService _userMiniAppService;
        private readonly ILogger<UserMiniAppMessageHandler> _logger;
        private IDisposable? _subscription;

        // Получает bus, сервис mini-app и логгер для централизованной обработки запросов.
        public UserMiniAppMessageHandler(
            IMessageBus messageBus,
            UserMiniAppService userMiniAppService,
            ILogger<UserMiniAppMessageHandler> logger)
        {
            _messageBus = messageBus;
            _userMiniAppService = userMiniAppService;
            _logger = logger;
        }

        // Инициализирует единственную подписку на GetUserRequest для текущего scope mini-app.
        public void EnsureSubscribed()
        {
            if (_subscription != null)
            {
                return;
            }

            _subscription = _messageBus
                .Listen<GetUserRequest>()
                // Передаём запрос в async-обработчик, который соберёт пользователя и отправит ответ.
                .Subscribe(request => _ = HandleGetUserAsync(request));

            _logger.LogInformation("UserMiniApp subscribed to GetUserRequest messages.");
        }

        // Обрабатывает bus-запрос пользователя и публикует типизированный ответ в bus.
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

        // Освобождает подписку mini-app на bus при завершении scope.
        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
