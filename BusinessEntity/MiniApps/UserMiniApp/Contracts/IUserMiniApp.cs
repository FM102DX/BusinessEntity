using BusinessEntity.Core.RichText;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

namespace BusinessEntity.MiniApps.UserMiniApp.Contracts
{
    // Определяет публичный контракт mini-app, который умеет отдавать текущего пользователя.
    public interface IUserMiniApp
    {
        // Даёт явную точку инициализации mini-app при старте приложения.
        void EnsureInitialized();

        // Возвращает текущего пользователя приложения, собранного из Authentik claims.
        Task<BusinessEntityUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default);

        // Гарантирует локальную учетную запись текущего Authentik-пользователя в user mini-app storage.
        Task<UserDto?> EnsureCurrentUserAsync(CancellationToken cancellationToken = default);

        // Удаляет локальную учетную запись текущего Authentik-пользователя и ее технические свойства.
        Task<bool> DeleteCurrentUserAsync(CancellationToken cancellationToken = default);

        // Возвращает локальных пользователей для административного CRUD UI.
        Task<IReadOnlyList<UserAdministrationRecord>> GetAdministrationUsersAsync(CancellationToken cancellationToken = default);

        // Читает пользователей приложения из Authentik и материализует их в локальную таблицу Users.
        Task<IReadOnlyList<UserAdministrationRecord>> ReadAdministrationUsersFromAuthentikAsync(
            CancellationToken cancellationToken = default);

        // Создает пользователя приложения в Authentik и материализует его локальную DTO.
        Task<UserAdministrationRecord> CreateAdministrationUserAsync(CancellationToken cancellationToken = default);

        // Обновляет локального пользователя приложения для административного CRUD UI.
        Task<UserAdministrationRecord> UpdateAdministrationUserAsync(
            Guid userId,
            UserAdministrationSaveRequest request,
            CancellationToken cancellationToken = default);

        // Удаляет локального пользователя приложения и его технические свойства.
        Task<bool> DeleteAdministrationUserAsync(Guid userId, CancellationToken cancellationToken = default);

        // Гарантирует системные роли UserMiniApp.
        Task EnsureSystemRolesAsync(CancellationToken cancellationToken = default);

        // Возвращает роли UserMiniApp для административного редактора.
        Task<IReadOnlyList<UserRoleRecord>> GetRolesAsync(CancellationToken cancellationToken = default);

        // Создает новую роль UserMiniApp.
        Task<UserRoleRecord> CreateRoleAsync(CancellationToken cancellationToken = default);

        // Обновляет роль UserMiniApp.
        Task<UserRoleRecord> UpdateRoleAsync(
            Guid roleId,
            UserRoleSaveRequest request,
            CancellationToken cancellationToken = default);

        // Удаляет роль UserMiniApp.
        Task<bool> DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

        // Возвращает группы пользователей UserMiniApp для административного редактора.
        Task<IReadOnlyList<UserGroupRecord>> GetUserGroupsAsync(CancellationToken cancellationToken = default);

        // Создает новую группу пользователей UserMiniApp.
        Task<UserGroupRecord> CreateUserGroupAsync(CancellationToken cancellationToken = default);

        // Обновляет группу пользователей UserMiniApp.
        Task<UserGroupRecord> UpdateUserGroupAsync(
            Guid groupId,
            UserGroupSaveRequest request,
            CancellationToken cancellationToken = default);

        // Удаляет группу пользователей UserMiniApp.
        Task<bool> DeleteUserGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

        // Возвращает назначение пользователей в выбранную группу.
        Task<IReadOnlyList<UserGroupMembershipRecord>> GetUserGroupMembershipsAsync(
            Guid groupId,
            CancellationToken cancellationToken = default);

        // Сохраняет полный список пользователей выбранной группы.
        Task<IReadOnlyList<UserGroupMembershipRecord>> UpdateUserGroupMembershipsAsync(
            Guid groupId,
            UserGroupMembershipSaveRequest request,
            CancellationToken cancellationToken = default);

        // Возвращает пространства для вкладки назначения ролей.
        Task<IReadOnlyList<UserSpaceRecord>> GetRoleAssignmentSpacesAsync(CancellationToken cancellationToken = default);

        // Возвращает назначения ролей для выбранного пространства.
        Task<IReadOnlyList<UserRoleAssignmentRecord>> GetRoleAssignmentsAsync(
            Guid spaceId,
            CancellationToken cancellationToken = default);

        // Создает назначение роли для группы или пользователя в выбранном пространстве.
        Task<UserRoleAssignmentRecord> CreateRoleAssignmentAsync(
            Guid spaceId,
            UserRoleAssignmentSaveRequest request,
            CancellationToken cancellationToken = default);

        // Удаляет назначение роли.
        Task<bool> DeleteRoleAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default);

        // Возвращает имена локальных групп текущего пользователя.
        Task<IReadOnlyList<string>> GetCurrentUserGroupNamesAsync(CancellationToken cancellationToken = default);

        // Возвращает effective permissions текущего или anonymous пользователя в пространстве.
        Task<UserEffectivePermissions> GetCurrentUserPermissionsForSpaceAsync(
            Guid spaceId,
            CancellationToken cancellationToken = default);

        // Возвращает effective permissions текущего или anonymous пользователя для пространства сущности.
        Task<UserEffectivePermissions> GetCurrentUserPermissionsForEntityAsync(
            Guid entityId,
            CancellationToken cancellationToken = default);

        // Возвращает effective permissions системного anonymous-пользователя в пространстве.
        Task<UserEffectivePermissions> GetAnonymousPermissionsForSpaceAsync(
            Guid spaceId,
            CancellationToken cancellationToken = default);

        // Возвращает пространства, где anonymous имеет права и есть доступные объекты.
        Task<IReadOnlyList<UserSpaceRecord>> GetAnonymousAccessibleSpacesAsync(CancellationToken cancellationToken = default);

        // Возвращает документы в пространстве, которые anonymous может открыть для просмотра.
        Task<IReadOnlyList<UserAccessibleDocumentRecord>> GetAnonymousAccessibleDocumentsAsync(
            Guid spaceId,
            CancellationToken cancellationToken = default);

        // Возвращает профиль текущего пользователя для страницы "Профиль".
        Task<UserProfileDto?> GetProfileAsync(CancellationToken cancellationToken = default);

        // Обновляет отображаемое имя и, при необходимости, пароль текущего пользователя.
        Task<UserProfileDto> UpdateProfileAsync(
            UserProfileUpdateRequest request,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<RichTextDocumentBookmark>> GetRichDocBookmarksAsync(
            Guid documentId,
            CancellationToken cancellationToken = default);

        Task<RichTextDocumentBookmark?> AddRichDocBookmarkAsync(
            Guid documentId,
            RichTextDocumentTextSelection? selection,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteRichDocBookmarkAsync(Guid bookmarkId, CancellationToken cancellationToken = default);

        Task<int> GetRichDocDisplayedLevelAsync(
            Guid documentId,
            CancellationToken cancellationToken = default);

        Task SaveRichDocDisplayedLevelAsync(
            Guid documentId,
            int displayLevelCount,
            CancellationToken cancellationToken = default);
    }
}
