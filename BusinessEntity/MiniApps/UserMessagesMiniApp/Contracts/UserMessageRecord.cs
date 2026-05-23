namespace BusinessEntity.MiniApps.UserMessagesMiniApp.Contracts;

// Запись пользовательского сообщения, подготовленная для отображения в UI.
public sealed record UserMessageRecord(
    Guid Id,
    Guid UserId,
    string Text,
    UserMessageLevel Level,
    string Title,
    DateTime CreatedAt);
