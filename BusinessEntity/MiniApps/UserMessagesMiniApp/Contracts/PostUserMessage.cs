namespace BusinessEntity.MiniApps.UserMessagesMiniApp.Contracts;

// Bus-команда на публикацию пользовательского сообщения в правой панели.
public sealed record PostUserMessage(
    Guid UserId,
    string Text,
    UserMessageLevel Level = UserMessageLevel.Info,
    string? Title = null);
