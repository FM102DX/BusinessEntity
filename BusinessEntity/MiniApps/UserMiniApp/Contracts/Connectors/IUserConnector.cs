namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Connectors
{
    public interface IUserConnector
    {
        Task<BusinessEntityUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> GetGroupsAsync(CancellationToken cancellationToken = default);
        Task<bool> IsInGroupAsync(string groupName, CancellationToken cancellationToken = default);
    }
}
