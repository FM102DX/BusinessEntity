using BusinessEntity.MiniApps.UserMiniApp.Contracts;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

namespace BusinessEntity.MiniApps.UserMiniApp.Internal
{
    // Хранит состояние user mini-app в рамках текущего DI scope.
    internal sealed class UserMiniAppState
    {
        // Показывает, загружался ли уже пользователь для текущего scope.
        public bool IsLoaded { get; set; }
        // Хранит собранного пользователя, чтобы не строить его повторно в одном scope.
        public BusinessEntityUser? CurrentUser { get; set; }
        // Показывает, что административный список пользователей уже загружался из Authentik.
        public bool AreAdministrationUsersLoaded { get; set; }
        // Хранит административные строки пользователей, материализованные из первого чтения Authentik.
        public IReadOnlyList<UserAdministrationRecord> AdministrationUsers { get; set; } =
            Array.Empty<UserAdministrationRecord>();
        // Хранит Authentik-записи пользователей приложения, полученные при первом чтении.
        public IReadOnlyList<AuthentikUserRecord> AuthentikApplicationUsers { get; set; } =
            Array.Empty<AuthentikUserRecord>();
    }
}
