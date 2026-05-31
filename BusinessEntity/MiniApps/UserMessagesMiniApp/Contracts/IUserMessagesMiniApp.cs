namespace BusinessEntity.MiniApps.UserMessagesMiniApp.Contracts;

// Публичный контракт mini-app пользовательских сообщений для правой панели.
public interface IUserMessagesMiniApp
{
    // Сообщает UI-компонентам, что стек пользовательских сообщений изменился.
    event Action? MessagesChanged;

    // Возвращает стек сообщений конкретного пользователя, где новые сообщения идут первыми.
    IReadOnlyList<UserMessageRecord> GetMessages(Guid userId);

    // Даёт явную точку инициализации bus-подписок mini-app.
    void EnsureInitialized();

    // Добавляет сообщение напрямую в стек mini-app.
    void Post(PostUserMessage message);

    // Очищает стек сообщений конкретного пользователя.
    void Clear(Guid userId);
}
