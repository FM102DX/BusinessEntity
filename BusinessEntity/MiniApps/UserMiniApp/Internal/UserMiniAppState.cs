using BusinessEntity.MiniApps.UserMiniApp.Contracts;

namespace BusinessEntity.MiniApps.UserMiniApp.Internal
{
    // Хранит состояние user mini-app в рамках текущего DI scope.
    internal sealed class UserMiniAppState
    {
        // Показывает, загружался ли уже пользователь для текущего scope.
        public bool IsLoaded { get; set; }
        // Хранит собранного пользователя, чтобы не строить его повторно в одном scope.
        public BusinessEntityUser? CurrentUser { get; set; }
    }
}
