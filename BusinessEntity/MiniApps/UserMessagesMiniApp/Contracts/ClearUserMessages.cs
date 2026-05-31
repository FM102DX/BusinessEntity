namespace BusinessEntity.MiniApps.UserMessagesMiniApp.Contracts;

// Bus-команда на очистку стека пользовательских сообщений для конкретного пользователя.
public sealed record ClearUserMessages(Guid UserId);
