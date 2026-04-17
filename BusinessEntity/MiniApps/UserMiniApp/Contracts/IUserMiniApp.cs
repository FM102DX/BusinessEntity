namespace BusinessEntity.MiniApps.UserMiniApp.Contracts
{
    // Определяет публичный контракт mini-app, который умеет отдавать текущего пользователя.
    public interface IUserMiniApp
    {
        // Возвращает текущего пользователя приложения, собранного из Authentik claims.
        Task<BusinessEntityUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    }
}
