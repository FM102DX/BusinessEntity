namespace BusinessEntity.MiniApps.UserMiniApp.Contracts
{
    public interface IUserMiniApp
    {
        Task<BusinessEntityUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    }
}
