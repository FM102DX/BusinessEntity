namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Connectors
{
    // Определяет короткий connector для адресного доступа к user mini-app из других модулей.
    public interface IUserConnector
    {
        // Возвращает текущего пользователя приложения через публичный контракт mini-app.
        Task<BusinessEntityUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
        // Возвращает все группы текущего пользователя.
        Task<IReadOnlyList<string>> GetGroupsAsync(CancellationToken cancellationToken = default);
        // Проверяет membership текущего пользователя в конкретной группе.
        Task<bool> IsInGroupAsync(string groupName, CancellationToken cancellationToken = default);
    }
}
