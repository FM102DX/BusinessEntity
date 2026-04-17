namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Messages
{
    using BusinessEntity.MiniApps.UserMiniApp.Contracts;

    // Представляет bus-ответ mini-app с текущим пользователем.
    public sealed record GetUserResponse(Guid RequestId, BusinessEntityUser? User);
}
