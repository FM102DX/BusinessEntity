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

        // Создает пользователя приложения в Authentik и материализует его локальную DTO.
        Task<UserAdministrationRecord> CreateAdministrationUserAsync(CancellationToken cancellationToken = default);

        // Обновляет локального пользователя приложения для административного CRUD UI.
        Task<UserAdministrationRecord> UpdateAdministrationUserAsync(
            Guid userId,
            UserAdministrationSaveRequest request,
            CancellationToken cancellationToken = default);

        // Удаляет локального пользователя приложения и его технические свойства.
        Task<bool> DeleteAdministrationUserAsync(Guid userId, CancellationToken cancellationToken = default);

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
