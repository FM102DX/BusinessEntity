using BusinessEntity.Core.RichText;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Connectors
{
    // Определяет короткий connector для адресного доступа к user mini-app из других модулей.
    public interface IUserConnector
    {
        // Возвращает текущего пользователя приложения через публичный контракт mini-app.
        Task<BusinessEntityUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
        // Гарантирует локальную учетную запись текущего пользователя и возвращает ее DTO.
        Task<UserDto?> EnsureCurrentUserAsync(CancellationToken cancellationToken = default);
        // Удаляет локальную учетную запись текущего пользователя вместе с ее техническими свойствами.
        Task<bool> DeleteCurrentUserAsync(CancellationToken cancellationToken = default);
        // Возвращает локальных пользователей для страницы администрирования.
        Task<IReadOnlyList<UserAdministrationRecord>> GetAdministrationUsersAsync(CancellationToken cancellationToken = default);
        // Создает пользователя приложения в Authentik через user mini-app.
        Task<UserAdministrationRecord> CreateAdministrationUserAsync(CancellationToken cancellationToken = default);
        // Обновляет локального пользователя через user mini-app.
        Task<UserAdministrationRecord> UpdateAdministrationUserAsync(
            Guid userId,
            UserAdministrationSaveRequest request,
            CancellationToken cancellationToken = default);
        // Удаляет локального пользователя через user mini-app.
        Task<bool> DeleteAdministrationUserAsync(Guid userId, CancellationToken cancellationToken = default);
        // Возвращает все группы текущего пользователя.
        Task<IReadOnlyList<string>> GetGroupsAsync(CancellationToken cancellationToken = default);
        // Проверяет membership текущего пользователя в конкретной группе.
        Task<bool> IsInGroupAsync(string groupName, CancellationToken cancellationToken = default);

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
