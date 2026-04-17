namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Messages
{
    using BusinessEntity.MiniApps.UserMiniApp.Contracts;

    public sealed record GetUserResponse(Guid RequestId, BusinessEntityUser? User);
}
