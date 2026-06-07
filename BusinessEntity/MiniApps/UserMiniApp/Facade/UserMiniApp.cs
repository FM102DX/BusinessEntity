using BusinessEntity.Core.RichText;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.MiniApps.UserMiniApp.Contracts;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;
using BusinessEntity.MiniApps.UserMiniApp.Internal;

namespace BusinessEntity.MiniApps.UserMiniApp.Facade
{
    // Представляет фасад user mini-app и делегирует публичные операции внутреннему сервису.
    internal sealed class UserMiniApp : IUserMiniApp
    {
        private readonly UserMiniAppService _userMiniAppService;
        private readonly UserMiniAppMessageHandler _messageHandler;

        // Инициализирует фасад mini-app без автоматической bus-подписки на каждый DI scope.
        public UserMiniApp(
            UserMiniAppService userMiniAppService,
            UserMiniAppMessageHandler messageHandler)
        {
            _userMiniAppService = userMiniAppService;
            _messageHandler = messageHandler;
        }

        // Даёт внешнему коду явную точку startup-инициализации системных данных и bus handler.
        public void EnsureInitialized()
        {
            _messageHandler.EnsureSubscribed();
            _userMiniAppService.EnsureSystemDefaultsAsync().GetAwaiter().GetResult();
        }

        // Делегирует получение текущего пользователя во внутренний сервис mini-app.
        public Task<BusinessEntityUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetCurrentUserAsync(cancellationToken);
        }

        public Task<Contracts.Dtos.UserDto?> EnsureCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.EnsureCurrentUserAsync(cancellationToken);
        }

        // Делегирует удаление локальной учетной записи текущего пользователя во внутренний сервис mini-app.
        public Task<bool> DeleteCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.DeleteCurrentUserAsync(cancellationToken);
        }

        // Делегирует чтение локальных пользователей во внутренний сервис mini-app.
        public Task<IReadOnlyList<UserAdministrationRecord>> GetAdministrationUsersAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetAdministrationUsersAsync(cancellationToken);
        }

        // Делегирует явное чтение Authentik-пользователей во внутренний сервис mini-app.
        public Task<IReadOnlyList<UserAdministrationRecord>> ReadAdministrationUsersFromAuthentikAsync(
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.ReadAdministrationUsersFromAuthentikAsync(cancellationToken);
        }

        // Делегирует создание пользователя Authentik во внутренний сервис mini-app.
        public Task<UserAdministrationRecord> CreateAdministrationUserAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.CreateAdministrationUserAsync(cancellationToken);
        }

        // Делегирует обновление локального пользователя во внутренний сервис mini-app.
        public Task<UserAdministrationRecord> UpdateAdministrationUserAsync(
            Guid userId,
            UserAdministrationSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.UpdateAdministrationUserAsync(userId, request, cancellationToken);
        }

        // Делегирует удаление локального пользователя во внутренний сервис mini-app.
        public Task<bool> DeleteAdministrationUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.DeleteAdministrationUserAsync(userId, cancellationToken);
        }

        // Делегирует проверку системных ролей во внутренний сервис mini-app.
        public Task EnsureSystemRolesAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.EnsureSystemRolesAsync(cancellationToken);
        }

        // Делегирует чтение ролей во внутренний сервис mini-app.
        public Task<IReadOnlyList<UserRoleRecord>> GetRolesAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetRolesAsync(cancellationToken);
        }

        // Делегирует создание роли во внутренний сервис mini-app.
        public Task<UserRoleRecord> CreateRoleAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.CreateRoleAsync(cancellationToken);
        }

        // Делегирует обновление роли во внутренний сервис mini-app.
        public Task<UserRoleRecord> UpdateRoleAsync(
            Guid roleId,
            UserRoleSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.UpdateRoleAsync(roleId, request, cancellationToken);
        }

        // Делегирует удаление роли во внутренний сервис mini-app.
        public Task<bool> DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.DeleteRoleAsync(roleId, cancellationToken);
        }

        // Делегирует чтение групп во внутренний сервис mini-app.
        public Task<IReadOnlyList<UserGroupRecord>> GetUserGroupsAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetUserGroupsAsync(cancellationToken);
        }

        // Делегирует создание группы во внутренний сервис mini-app.
        public Task<UserGroupRecord> CreateUserGroupAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.CreateUserGroupAsync(cancellationToken);
        }

        // Делегирует обновление группы во внутренний сервис mini-app.
        public Task<UserGroupRecord> UpdateUserGroupAsync(
            Guid groupId,
            UserGroupSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.UpdateUserGroupAsync(groupId, request, cancellationToken);
        }

        // Делегирует удаление группы во внутренний сервис mini-app.
        public Task<bool> DeleteUserGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.DeleteUserGroupAsync(groupId, cancellationToken);
        }

        // Делегирует чтение назначений группы во внутренний сервис mini-app.
        public Task<IReadOnlyList<UserGroupMembershipRecord>> GetUserGroupMembershipsAsync(
            Guid groupId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetUserGroupMembershipsAsync(groupId, cancellationToken);
        }

        // Делегирует сохранение назначений группы во внутренний сервис mini-app.
        public Task<IReadOnlyList<UserGroupMembershipRecord>> UpdateUserGroupMembershipsAsync(
            Guid groupId,
            UserGroupMembershipSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.UpdateUserGroupMembershipsAsync(groupId, request, cancellationToken);
        }

        // Делегирует чтение пространств для назначения ролей во внутренний сервис mini-app.
        public Task<IReadOnlyList<UserSpaceRecord>> GetRoleAssignmentSpacesAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetRoleAssignmentSpacesAsync(cancellationToken);
        }

        // Делегирует чтение назначений ролей во внутренний сервис mini-app.
        public Task<IReadOnlyList<UserRoleAssignmentRecord>> GetRoleAssignmentsAsync(
            Guid spaceId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetRoleAssignmentsAsync(spaceId, cancellationToken);
        }

        // Делегирует создание назначения роли во внутренний сервис mini-app.
        public Task<UserRoleAssignmentRecord> CreateRoleAssignmentAsync(
            Guid spaceId,
            UserRoleAssignmentSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.CreateRoleAssignmentAsync(spaceId, request, cancellationToken);
        }

        // Делегирует удаление назначения роли во внутренний сервис mini-app.
        public Task<bool> DeleteRoleAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.DeleteRoleAssignmentAsync(assignmentId, cancellationToken);
        }

        // Делегирует чтение групп текущего пользователя во внутренний сервис mini-app.
        public Task<IReadOnlyList<string>> GetCurrentUserGroupNamesAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetCurrentUserGroupNamesAsync(cancellationToken);
        }

        // Делегирует чтение профиля текущего пользователя во внутренний сервис mini-app.
        public Task<UserProfileDto?> GetProfileAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetProfileAsync(cancellationToken);
        }

        // Делегирует обновление профиля текущего пользователя во внутренний сервис mini-app.
        public Task<UserProfileDto> UpdateProfileAsync(
            UserProfileUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.UpdateProfileAsync(request, cancellationToken);
        }

        public Task<IReadOnlyList<RichTextDocumentBookmark>> GetRichDocBookmarksAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetRichDocBookmarksAsync(documentId, cancellationToken);
        }

        public Task<RichTextDocumentBookmark?> AddRichDocBookmarkAsync(
            Guid documentId,
            RichTextDocumentTextSelection? selection,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.AddRichDocBookmarkAsync(documentId, selection, cancellationToken);
        }

        public Task<bool> DeleteRichDocBookmarkAsync(Guid bookmarkId, CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.DeleteRichDocBookmarkAsync(bookmarkId, cancellationToken);
        }

        public Task<int> GetRichDocDisplayedLevelAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetRichDocDisplayedLevelAsync(documentId, cancellationToken);
        }

        public Task SaveRichDocDisplayedLevelAsync(
            Guid documentId,
            int displayLevelCount,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.SaveRichDocDisplayedLevelAsync(documentId, displayLevelCount, cancellationToken);
        }

        public Task<IReadOnlyList<UserPropertyDto>> GetCurrentUserPropertiesAsync(
            int propertyType,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetCurrentUserPropertiesAsync(propertyType, cancellationToken);
        }

        public Task<UserPropertyDto?> UpsertCurrentUserPropertyAsync(
            Guid? propertyId,
            int propertyType,
            string data,
            string metadata,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.UpsertCurrentUserPropertyAsync(
                propertyId,
                propertyType,
                data,
                metadata,
                cancellationToken);
        }

        public Task<bool> DeleteCurrentUserPropertyAsync(
            Guid propertyId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.DeleteCurrentUserPropertyAsync(propertyId, cancellationToken);
        }

        // Делегирует чтение пресетов печати во внутренний сервис mini-app.
        public Task<DocPrintSettingsPresetCollection> GetDocPrintPresetsAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetDocPrintPresetsAsync(cancellationToken);
        }

        // Делегирует сохранение пресета печати во внутренний сервис mini-app.
        public Task<DocPrintSettingsPreset> SaveDocPrintPresetAsync(
            DocPrintSettingsPreset preset,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.SaveDocPrintPresetAsync(preset, cancellationToken);
        }

        // Делегирует удаление пресета печати во внутренний сервис mini-app.
        public Task<bool> DeleteDocPrintPresetAsync(
            string presetName,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.DeleteDocPrintPresetAsync(presetName, cancellationToken);
        }

        // Делегирует расчет прав текущего или anonymous пользователя в пространстве.
        public Task<UserEffectivePermissions> GetCurrentUserPermissionsForSpaceAsync(
            Guid spaceId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetCurrentUserPermissionsForSpaceAsync(spaceId, cancellationToken);
        }

        // Делегирует расчет прав текущего или anonymous пользователя для пространства сущности.
        public Task<UserEffectivePermissions> GetCurrentUserPermissionsForEntityAsync(
            Guid entityId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetCurrentUserPermissionsForEntityAsync(entityId, cancellationToken);
        }

        // Делегирует расчет content-access решения текущего или anonymous пользователя.
        public Task<UserContentAccessDecision> GetCurrentUserContentAccessForEntityAsync(
            UserContentAccessRequest request,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetCurrentUserContentAccessForEntityAsync(request, cancellationToken);
        }

        // Делегирует расчет прав системного anonymous-пользователя в пространстве.
        public Task<UserEffectivePermissions> GetAnonymousPermissionsForSpaceAsync(
            Guid spaceId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetAnonymousPermissionsForSpaceAsync(spaceId, cancellationToken);
        }

        // Делегирует поиск anonymous-доступных пространств во внутренний сервис mini-app.
        public Task<IReadOnlyList<UserSpaceRecord>> GetAnonymousAccessibleSpacesAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetAnonymousAccessibleSpacesAsync(cancellationToken);
        }

        // Делегирует поиск anonymous-доступных документов во внутренний сервис mini-app.
        public Task<IReadOnlyList<UserAccessibleDocumentRecord>> GetAnonymousAccessibleDocumentsAsync(
            Guid spaceId,
            CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetAnonymousAccessibleDocumentsAsync(spaceId, cancellationToken);
        }
    }
}
