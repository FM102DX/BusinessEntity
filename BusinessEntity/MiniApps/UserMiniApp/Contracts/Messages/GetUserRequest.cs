namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Messages
{
    // Представляет bus-запрос на получение текущего пользователя.
    public sealed record GetUserRequest(Guid RequestId);
}
