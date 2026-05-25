using BusinessEntity.MiniApps.UserMiniApp.Contracts;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Connectors;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;
using BusinessEntity.Core.RichText;

namespace BusinessEntity.MiniApps.UserMiniApp.Connectors
{
    // Предоставляет другим модулям короткий доступ к данным user mini-app.
    public sealed class UserConnector : IUserConnector
    {
        private readonly IUserMiniApp _userMiniApp;

        // Инициализирует connector прямой ссылкой на публичный контракт user mini-app.
        public UserConnector(IUserMiniApp userMiniApp)
        {
            _userMiniApp = userMiniApp;
        }

        // Возвращает текущего пользователя без bus-roundtrip, чтобы не плодить межscope подписки.
        public Task<BusinessEntityUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetCurrentUserAsync(cancellationToken);
        }

        public Task<UserDto?> EnsureCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniApp.EnsureCurrentUserAsync(cancellationToken);
        }

        // Удаляет локальную учетную запись текущего пользователя через публичный контракт mini-app.
        public Task<bool> DeleteCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniApp.DeleteCurrentUserAsync(cancellationToken);
        }

        // Возвращает локальных пользователей через публичный контракт user mini-app.
        public Task<IReadOnlyList<UserAdministrationRecord>> GetAdministrationUsersAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetAdministrationUsersAsync(cancellationToken);
        }

        // Читает пользователей Authentik по явной команде администратора.
        public Task<IReadOnlyList<UserAdministrationRecord>> ReadAdministrationUsersFromAuthentikAsync(
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.ReadAdministrationUsersFromAuthentikAsync(cancellationToken);
        }

        // Создает пользователя приложения в Authentik через публичный контракт user mini-app.
        public Task<UserAdministrationRecord> CreateAdministrationUserAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniApp.CreateAdministrationUserAsync(cancellationToken);
        }

        // Обновляет локального пользователя через публичный контракт user mini-app.
        public Task<UserAdministrationRecord> UpdateAdministrationUserAsync(
            Guid userId,
            UserAdministrationSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.UpdateAdministrationUserAsync(userId, request, cancellationToken);
        }

        // Удаляет локального пользователя через публичный контракт user mini-app.
        public Task<bool> DeleteAdministrationUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return _userMiniApp.DeleteAdministrationUserAsync(userId, cancellationToken);
        }

        // Возвращает роли через публичный контракт user mini-app.
        public Task<IReadOnlyList<UserRoleRecord>> GetRolesAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetRolesAsync(cancellationToken);
        }

        // Создает роль через публичный контракт user mini-app.
        public Task<UserRoleRecord> CreateRoleAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniApp.CreateRoleAsync(cancellationToken);
        }

        // Обновляет роль через публичный контракт user mini-app.
        public Task<UserRoleRecord> UpdateRoleAsync(
            Guid roleId,
            UserRoleSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.UpdateRoleAsync(roleId, request, cancellationToken);
        }

        // Удаляет роль через публичный контракт user mini-app.
        public Task<bool> DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
        {
            return _userMiniApp.DeleteRoleAsync(roleId, cancellationToken);
        }

        // Возвращает группы пользователей через публичный контракт user mini-app.
        public Task<IReadOnlyList<UserGroupRecord>> GetUserGroupsAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetUserGroupsAsync(cancellationToken);
        }

        // Создает группу пользователей через публичный контракт user mini-app.
        public Task<UserGroupRecord> CreateUserGroupAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniApp.CreateUserGroupAsync(cancellationToken);
        }

        // Обновляет группу пользователей через публичный контракт user mini-app.
        public Task<UserGroupRecord> UpdateUserGroupAsync(
            Guid groupId,
            UserGroupSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.UpdateUserGroupAsync(groupId, request, cancellationToken);
        }

        // Удаляет группу пользователей через публичный контракт user mini-app.
        public Task<bool> DeleteUserGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            return _userMiniApp.DeleteUserGroupAsync(groupId, cancellationToken);
        }

        // Возвращает назначение пользователей в группу через публичный контракт user mini-app.
        public Task<IReadOnlyList<UserGroupMembershipRecord>> GetUserGroupMembershipsAsync(
            Guid groupId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetUserGroupMembershipsAsync(groupId, cancellationToken);
        }

        // Сохраняет назначение пользователей в группу через публичный контракт user mini-app.
        public Task<IReadOnlyList<UserGroupMembershipRecord>> UpdateUserGroupMembershipsAsync(
            Guid groupId,
            UserGroupMembershipSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.UpdateUserGroupMembershipsAsync(groupId, request, cancellationToken);
        }

        // Возвращает пространства для вкладки назначения ролей через публичный контракт user mini-app.
        public Task<IReadOnlyList<UserSpaceRecord>> GetRoleAssignmentSpacesAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetRoleAssignmentSpacesAsync(cancellationToken);
        }

        // Возвращает назначения ролей для выбранного пространства через публичный контракт user mini-app.
        public Task<IReadOnlyList<UserRoleAssignmentRecord>> GetRoleAssignmentsAsync(
            Guid spaceId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetRoleAssignmentsAsync(spaceId, cancellationToken);
        }

        // Создает назначение роли через публичный контракт user mini-app.
        public Task<UserRoleAssignmentRecord> CreateRoleAssignmentAsync(
            Guid spaceId,
            UserRoleAssignmentSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.CreateRoleAssignmentAsync(spaceId, request, cancellationToken);
        }

        // Удаляет назначение роли через публичный контракт user mini-app.
        public Task<bool> DeleteRoleAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default)
        {
            return _userMiniApp.DeleteRoleAssignmentAsync(assignmentId, cancellationToken);
        }

        // Возвращает профиль текущего пользователя через публичный контракт user mini-app.
        public Task<UserProfileDto?> GetProfileAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetProfileAsync(cancellationToken);
        }

        // Обновляет профиль текущего пользователя через публичный контракт user mini-app.
        public Task<UserProfileDto> UpdateProfileAsync(
            UserProfileUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.UpdateProfileAsync(request, cancellationToken);
        }

        // Возвращает только список групп поверх общего объекта пользователя.
        public async Task<IReadOnlyList<string>> GetGroupsAsync(CancellationToken cancellationToken = default)
        {
            return await _userMiniApp.GetCurrentUserGroupNamesAsync(cancellationToken);
        }

        // Проверяет membership в группе поверх общего объекта пользователя.
        public async Task<bool> IsInGroupAsync(string groupName, CancellationToken cancellationToken = default)
        {
            var groups = await _userMiniApp.GetCurrentUserGroupNamesAsync(cancellationToken);
            return groups.Any(group => string.Equals(group, groupName, StringComparison.OrdinalIgnoreCase));
        }

        public Task<IReadOnlyList<RichTextDocumentBookmark>> GetRichDocBookmarksAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetRichDocBookmarksAsync(documentId, cancellationToken);
        }

        public Task<RichTextDocumentBookmark?> AddRichDocBookmarkAsync(
            Guid documentId,
            RichTextDocumentTextSelection? selection,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.AddRichDocBookmarkAsync(documentId, selection, cancellationToken);
        }

        public Task<bool> DeleteRichDocBookmarkAsync(Guid bookmarkId, CancellationToken cancellationToken = default)
        {
            return _userMiniApp.DeleteRichDocBookmarkAsync(bookmarkId, cancellationToken);
        }

        public Task<int> GetRichDocDisplayedLevelAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetRichDocDisplayedLevelAsync(documentId, cancellationToken);
        }

        public Task SaveRichDocDisplayedLevelAsync(
            Guid documentId,
            int displayLevelCount,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.SaveRichDocDisplayedLevelAsync(documentId, displayLevelCount, cancellationToken);
        }

        // Возвращает effective permissions текущего или anonymous пользователя в пространстве.
        public Task<UserEffectivePermissions> GetCurrentUserPermissionsForSpaceAsync(
            Guid spaceId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetCurrentUserPermissionsForSpaceAsync(spaceId, cancellationToken);
        }

        // Возвращает effective permissions текущего или anonymous пользователя для пространства сущности.
        public Task<UserEffectivePermissions> GetCurrentUserPermissionsForEntityAsync(
            Guid entityId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetCurrentUserPermissionsForEntityAsync(entityId, cancellationToken);
        }

        // Возвращает готовое решение по доступу текущего или anonymous пользователя к контентной сущности.
        public Task<UserContentAccessDecision> GetCurrentUserContentAccessForEntityAsync(
            UserContentAccessRequest request,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetCurrentUserContentAccessForEntityAsync(request, cancellationToken);
        }

        // Возвращает effective permissions anonymous-пользователя в пространстве.
        public Task<UserEffectivePermissions> GetAnonymousPermissionsForSpaceAsync(
            Guid spaceId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetAnonymousPermissionsForSpaceAsync(spaceId, cancellationToken);
        }

        // Возвращает пространства, где anonymous имеет права и есть доступные объекты.
        public Task<IReadOnlyList<UserSpaceRecord>> GetAnonymousAccessibleSpacesAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetAnonymousAccessibleSpacesAsync(cancellationToken);
        }

        // Возвращает anonymous-доступные документы в пространстве через user mini-app.
        public Task<IReadOnlyList<UserAccessibleDocumentRecord>> GetAnonymousAccessibleDocumentsAsync(
            Guid spaceId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniApp.GetAnonymousAccessibleDocumentsAsync(spaceId, cancellationToken);
        }
    }
}
