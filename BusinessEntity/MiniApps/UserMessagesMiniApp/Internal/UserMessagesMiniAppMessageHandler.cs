using BusinessEntity.MiniApps.UserMessagesMiniApp.Contracts;
using ReactiveUI;

namespace BusinessEntity.MiniApps.UserMessagesMiniApp.Internal;

// Подписывает mini-app пользовательских сообщений на bus-команды.
internal sealed class UserMessagesMiniAppMessageHandler : IDisposable
{
    private readonly IMessageBus _messageBus;
    private readonly UserMessagesMiniAppState _state;
    private readonly ILogger<UserMessagesMiniAppMessageHandler> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    // Создает handler, который переносит bus-сообщения в state mini-app.
    public UserMessagesMiniAppMessageHandler(
        IMessageBus messageBus,
        UserMessagesMiniAppState state,
        ILogger<UserMessagesMiniAppMessageHandler> logger)
    {
        _messageBus = messageBus;
        _state = state;
        _logger = logger;
    }

    // Инициализирует подписки на публикацию и очистку пользовательских сообщений.
    public void EnsureSubscribed()
    {
        if (_subscriptions.Count > 0)
        {
            return;
        }

        _subscriptions.Add(_messageBus.Listen<PostUserMessage>().Subscribe(_state.Add));
        _subscriptions.Add(_messageBus.Listen<ClearUserMessages>().Subscribe(_state.Clear));

        _logger.LogInformation("UserMessagesMiniApp subscribed to user messages.");
    }

    // Освобождает bus-подписки mini-app.
    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
    }
}
