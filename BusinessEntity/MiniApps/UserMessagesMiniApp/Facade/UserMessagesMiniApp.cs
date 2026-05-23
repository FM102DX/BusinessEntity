using BusinessEntity.MiniApps.UserMessagesMiniApp.Contracts;
using BusinessEntity.MiniApps.UserMessagesMiniApp.Internal;

namespace BusinessEntity.MiniApps.UserMessagesMiniApp.Facade;

// Фасад mini-app пользовательских сообщений.
internal sealed class UserMessagesMiniApp : IUserMessagesMiniApp
{
    private readonly UserMessagesMiniAppState _state;
    private readonly UserMessagesMiniAppMessageHandler _messageHandler;

    // Создает фасад и активирует bus-подписки mini-app.
    public UserMessagesMiniApp(
        UserMessagesMiniAppState state,
        UserMessagesMiniAppMessageHandler messageHandler)
    {
        _state = state;
        _messageHandler = messageHandler;
        _messageHandler.EnsureSubscribed();
    }

    // Пробрасывает событие изменения сообщений из state.
    public event Action? MessagesChanged
    {
        add => _state.Changed += value;
        remove => _state.Changed -= value;
    }

    // Возвращает стек пользовательских сообщений указанного пользователя.
    public IReadOnlyList<UserMessageRecord> GetMessages(Guid userId)
    {
        return _state.GetMessages(userId);
    }

    // Даёт внешнему коду явную точку для startup-инициализации mini-app.
    public void EnsureInitialized()
    {
        _messageHandler.EnsureSubscribed();
    }

    // Добавляет сообщение напрямую в state mini-app.
    public void Post(PostUserMessage message)
    {
        _state.Add(message);
    }

    // Очищает стек сообщений указанного пользователя.
    public void Clear(Guid userId)
    {
        _state.Clear(new ClearUserMessages(userId));
    }
}
