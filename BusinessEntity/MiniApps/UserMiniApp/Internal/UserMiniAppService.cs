using BusinessEntity.MiniApps.UserMiniApp.Contracts;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Repositories;
using BusinessEntity.Core.RichText;
using System.Text.Json;

namespace BusinessEntity.MiniApps.UserMiniApp.Internal
{
    // Инкапсулирует основную логику получения и кэширования текущего пользователя в пределах scope.
    internal sealed class UserMiniAppService
    {
        private const int MaxBookmarkTextLength = 500;
        private const int MaxBookmarkLabelLength = 80;

        private readonly UserMiniAppState _state;
        private readonly BusinessEntityUserFactory _userFactory;
        private readonly IUserMiniAppRepository<UserDto> _userRepository;
        private readonly IUserMiniAppRepository<UserPropertyDto> _userPropertyRepository;

        // Получает state mini-app и фабрику, которая умеет собирать пользователя из Authentik principal.
        public UserMiniAppService(
            UserMiniAppState state,
            BusinessEntityUserFactory userFactory,
            IUserMiniAppRepository<UserDto> userRepository,
            IUserMiniAppRepository<UserPropertyDto> userPropertyRepository)
        {
            _state = state;
            _userFactory = userFactory;
            _userRepository = userRepository;
            _userPropertyRepository = userPropertyRepository;
        }

        // Возвращает пользователя из state или один раз строит его через factory.
        public async Task<BusinessEntityUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            if (_state.IsLoaded)
            {
                return _state.CurrentUser;
            }

            _state.CurrentUser = await _userFactory.CreateAsync(cancellationToken);
            _state.IsLoaded = true;
            return _state.CurrentUser;
        }

        public async Task<UserDto?> EnsureCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            var currentUser = await GetCurrentUserAsync(cancellationToken);
            if (currentUser?.IsAuthenticated != true)
            {
                return null;
            }

            var externalId = currentUser.GetNameIdentifier();
            if (string.IsNullOrWhiteSpace(externalId))
            {
                externalId = currentUser.UserId;
            }

            if (string.IsNullOrWhiteSpace(externalId))
            {
                return null;
            }

            var existingUser = (await _userRepository.GetAllAsync(
                    user => user.ExternalId == externalId,
                    cancellationToken))
                .OrderBy(user => user.DateCreated)
                .FirstOrDefault();

            var displayedName = string.IsNullOrWhiteSpace(currentUser.UserName)
                ? externalId
                : currentUser.UserName;

            if (existingUser != null)
            {
                var userData = ReadUserData(existingUser);
                if (string.IsNullOrWhiteSpace(userData.DisplayedName))
                {
                    userData.DisplayedName = displayedName;
                    userData.ExtId = externalId;
                    existingUser.Payload = SerializeUserData(userData);
                    existingUser.DateLastModified = DateTime.UtcNow;
                    await _userRepository.UpdateAsync(existingUser, cancellationToken);
                }

                return existingUser;
            }

            var now = DateTime.UtcNow;
            var created = new UserDto
            {
                Id = Guid.NewGuid(),
                ExternalId = externalId,
                Payload = SerializeUserData(new UserData
                {
                    DisplayedName = displayedName,
                    ExtId = externalId
                }),
                DateCreated = now,
                DateLastModified = now
            };

            return await _userRepository.AddAsync(created, cancellationToken);
        }

        // Удаляет локальную запись текущего Authentik-пользователя и все привязанные user properties.
        public async Task<bool> DeleteCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            var currentUser = await GetCurrentUserAsync(cancellationToken);
            if (currentUser?.IsAuthenticated != true)
            {
                return false;
            }

            var externalId = ResolveExternalId(currentUser);
            if (string.IsNullOrWhiteSpace(externalId))
            {
                return false;
            }

            var users = (await _userRepository.GetAllAsync(
                    user => user.ExternalId == externalId,
                    cancellationToken))
                .OrderBy(user => user.DateCreated)
                .ToList();

            if (users.Count == 0)
            {
                return false;
            }

            foreach (var user in users)
            {
                var properties = await _userPropertyRepository.GetAllAsync(
                    property => property.ParentEntityId == user.Id,
                    cancellationToken);

                foreach (var property in properties)
                {
                    await _userPropertyRepository.DeleteAsync(property.Id, cancellationToken);
                }

                await _userRepository.DeleteAsync(user.Id, cancellationToken);
            }

            return true;
        }

        public async Task<IReadOnlyList<RichTextDocumentBookmark>> GetRichDocBookmarksAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            var user = await EnsureCurrentUserAsync(cancellationToken);
            if (user == null)
            {
                return Array.Empty<RichTextDocumentBookmark>();
            }

            var payload = await ReadBookmarksPayloadAsync(user.Id, cancellationToken);
            return payload.Bookmarks
                .Where(x => x.DocumentId == documentId)
                .OrderBy(x => x.ChunkSortOrder)
                .ThenBy(x => x.BlockIndex)
                .ThenBy(x => x.CreatedDate)
                .ToList();
        }

        public async Task<RichTextDocumentBookmark?> AddRichDocBookmarkAsync(
            Guid documentId,
            RichTextDocumentTextSelection? selection,
            CancellationToken cancellationToken = default)
        {
            if (documentId == Guid.Empty || selection?.Position == null)
            {
                return null;
            }

            var selectedText = NormalizeBookmarkText(selection.Text);
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                return null;
            }

            var user = await EnsureCurrentUserAsync(cancellationToken);
            if (user == null)
            {
                return null;
            }

            var payload = await ReadBookmarksPayloadAsync(user.Id, cancellationToken);
            var bookmark = new RichTextDocumentBookmark
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                ChunkSortOrder = selection.Position.ChunkSortOrder,
                BlockIndex = selection.Position.BlockIndex,
                SelectedText = selectedText,
                Label = BuildBookmarkLabel(selectedText),
                CreatedDate = DateTime.UtcNow
            };

            payload.Bookmarks.Add(bookmark);
            await UpsertBookmarksPayloadAsync(user.Id, payload, cancellationToken);
            return bookmark;
        }

        public async Task<bool> DeleteRichDocBookmarkAsync(Guid bookmarkId, CancellationToken cancellationToken = default)
        {
            if (bookmarkId == Guid.Empty)
            {
                return false;
            }

            var user = await EnsureCurrentUserAsync(cancellationToken);
            if (user == null)
            {
                return false;
            }

            var payload = await ReadBookmarksPayloadAsync(user.Id, cancellationToken);
            var removedCount = payload.Bookmarks.RemoveAll(x => x.Id == bookmarkId);
            if (removedCount == 0)
            {
                return false;
            }

            await UpsertBookmarksPayloadAsync(user.Id, payload, cancellationToken);
            return true;
        }

        public async Task<int> GetRichDocDisplayedLevelAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            if (documentId == Guid.Empty)
            {
                return 1;
            }

            var user = await EnsureCurrentUserAsync(cancellationToken);
            if (user == null)
            {
                return 1;
            }

            var property = await ReadDisplayedLevelPropertyAsync(user.Id, documentId, cancellationToken);
            return NormalizeDisplayedLevel(property?.DisplayLevelCount ?? 1);
        }

        public async Task SaveRichDocDisplayedLevelAsync(
            Guid documentId,
            int displayLevelCount,
            CancellationToken cancellationToken = default)
        {
            if (documentId == Guid.Empty)
            {
                return;
            }

            var user = await EnsureCurrentUserAsync(cancellationToken);
            if (user == null)
            {
                return;
            }

            await UpsertDisplayedLevelPropertyAsync(
                user.Id,
                new RichDocDisplayedLevelProperty
                {
                    DocumentId = documentId,
                    DisplayLevelCount = NormalizeDisplayedLevel(displayLevelCount)
                },
                cancellationToken);
        }

        private static UserData ReadUserData(UserDto user)
        {
            if (string.IsNullOrWhiteSpace(user.Payload))
            {
                return new UserData { ExtId = user.ExternalId };
            }

            try
            {
                return JsonSerializer.Deserialize<UserData>(user.Payload, UserMiniAppJsonOptions.Default)
                       ?? new UserData { ExtId = user.ExternalId };
            }
            catch (JsonException)
            {
                return new UserData { ExtId = user.ExternalId };
            }
        }

        private static string SerializeUserData(UserData userData)
        {
            return JsonSerializer.Serialize(userData, UserMiniAppJsonOptions.Default);
        }

        // Определяет стабильный внешний идентификатор пользователя из нормализованной user-модели.
        private static string ResolveExternalId(BusinessEntityUser currentUser)
        {
            var externalId = currentUser.GetNameIdentifier();
            if (string.IsNullOrWhiteSpace(externalId))
            {
                externalId = currentUser.UserId;
            }

            return externalId;
        }

        private async Task<RichTextDocumentBookmarksPayload> ReadBookmarksPayloadAsync(Guid userId, CancellationToken cancellationToken)
        {
            var property = (await _userPropertyRepository.GetAllAsync(
                    x => x.ParentEntityId == userId &&
                         x.PropertyType == (int)UserPropertyTypeEnum.RichDocBookmarks,
                    cancellationToken))
                .OrderByDescending(x => x.DateLastModified)
                .FirstOrDefault();

            if (property == null || string.IsNullOrWhiteSpace(property.Data))
            {
                return new RichTextDocumentBookmarksPayload();
            }

            try
            {
                var payload = JsonSerializer.Deserialize<RichTextDocumentBookmarksPayload>(
                    property.Data,
                    UserMiniAppJsonOptions.Default);

                if (payload?.SchemaVersion == 1 &&
                    string.Equals(payload.Kind, "RichDocBookmarks", StringComparison.Ordinal))
                {
                    payload.Bookmarks ??= new List<RichTextDocumentBookmark>();
                    return payload;
                }
            }
            catch (JsonException)
            {
                // Invalid user property payload is treated as empty.
            }

            return new RichTextDocumentBookmarksPayload();
        }

        private async Task<RichDocDisplayedLevelProperty?> ReadDisplayedLevelPropertyAsync(
            Guid userId,
            Guid documentId,
            CancellationToken cancellationToken)
        {
            var properties = (await _userPropertyRepository.GetAllAsync(
                    x => x.ParentEntityId == userId &&
                         x.PropertyType == (int)UserPropertyTypeEnum.RichDocDisplayedLevelProperty,
                    cancellationToken))
                .OrderByDescending(x => x.DateLastModified)
                .ToList();

            foreach (var property in properties)
            {
                var payload = TryReadDisplayedLevelProperty(property.Data);
                if (payload?.DocumentId == documentId)
                {
                    return payload;
                }
            }

            return null;
        }

        private async Task UpsertBookmarksPayloadAsync(
            Guid userId,
            RichTextDocumentBookmarksPayload payload,
            CancellationToken cancellationToken)
        {
            var properties = (await _userPropertyRepository.GetAllAsync(
                    x => x.ParentEntityId == userId &&
                         x.PropertyType == (int)UserPropertyTypeEnum.RichDocBookmarks,
                    cancellationToken))
                .OrderByDescending(x => x.DateLastModified)
                .ToList();

            var now = DateTime.UtcNow;
            var data = JsonSerializer.Serialize(payload, UserMiniAppJsonOptions.Default);
            var metadata = JsonSerializer.Serialize(
                new
                {
                    schemaVersion = 1,
                    kind = "RichDocBookmarksMetadata",
                    bookmarkCount = payload.Bookmarks.Count
                },
                UserMiniAppJsonOptions.Default);

            var property = properties.FirstOrDefault();
            if (property == null)
            {
                await _userPropertyRepository.AddAsync(
                    new UserPropertyDto
                    {
                        Id = Guid.NewGuid(),
                        DateCreated = now,
                        DateLastModified = now,
                        ParentEntityId = userId,
                        PropertyType = (int)UserPropertyTypeEnum.RichDocBookmarks,
                        Data = data,
                        Metadata = metadata
                    },
                    cancellationToken);
                return;
            }

            property.DateLastModified = now;
            property.Data = data;
            property.Metadata = metadata;
            await _userPropertyRepository.UpdateAsync(property, cancellationToken);

            foreach (var duplicate in properties.Skip(1))
            {
                await _userPropertyRepository.DeleteAsync(duplicate.Id, cancellationToken);
            }
        }

        private async Task UpsertDisplayedLevelPropertyAsync(
            Guid userId,
            RichDocDisplayedLevelProperty payload,
            CancellationToken cancellationToken)
        {
            var properties = (await _userPropertyRepository.GetAllAsync(
                    x => x.ParentEntityId == userId &&
                         x.PropertyType == (int)UserPropertyTypeEnum.RichDocDisplayedLevelProperty,
                    cancellationToken))
                .OrderByDescending(x => x.DateLastModified)
                .ToList();

            payload.DisplayLevelCount = NormalizeDisplayedLevel(payload.DisplayLevelCount);

            var matchingProperties = properties
                .Where(property => TryReadDisplayedLevelProperty(property.Data)?.DocumentId == payload.DocumentId)
                .ToList();

            var now = DateTime.UtcNow;
            var data = JsonSerializer.Serialize(payload, UserMiniAppJsonOptions.Default);
            var metadata = JsonSerializer.Serialize(
                new
                {
                    schemaVersion = 1,
                    kind = "RichDocDisplayedLevelPropertyMetadata",
                    documentId = payload.DocumentId,
                    displayLevelCount = payload.DisplayLevelCount
                },
                UserMiniAppJsonOptions.Default);

            var property = matchingProperties.FirstOrDefault();
            if (property == null)
            {
                await _userPropertyRepository.AddAsync(
                    new UserPropertyDto
                    {
                        Id = Guid.NewGuid(),
                        DateCreated = now,
                        DateLastModified = now,
                        ParentEntityId = userId,
                        PropertyType = (int)UserPropertyTypeEnum.RichDocDisplayedLevelProperty,
                        Data = data,
                        Metadata = metadata
                    },
                    cancellationToken);
                return;
            }

            property.DateLastModified = now;
            property.Data = data;
            property.Metadata = metadata;
            await _userPropertyRepository.UpdateAsync(property, cancellationToken);

            foreach (var duplicate in matchingProperties.Skip(1))
            {
                await _userPropertyRepository.DeleteAsync(duplicate.Id, cancellationToken);
            }
        }

        private static RichDocDisplayedLevelProperty? TryReadDisplayedLevelProperty(string? data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return null;
            }

            try
            {
                var payload = JsonSerializer.Deserialize<RichDocDisplayedLevelProperty>(
                    data,
                    UserMiniAppJsonOptions.Default);

                if (payload?.SchemaVersion == 1 &&
                    string.Equals(payload.Kind, "RichDocDisplayedLevelProperty", StringComparison.Ordinal))
                {
                    payload.DisplayLevelCount = NormalizeDisplayedLevel(payload.DisplayLevelCount);
                    return payload;
                }
            }
            catch (JsonException)
            {
                // Invalid user property payload is ignored.
            }

            return null;
        }

        private static int NormalizeDisplayedLevel(int value)
        {
            return Math.Clamp(value, 1, 3);
        }

        private static string NormalizeBookmarkText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var normalized = string.Join(
                " ",
                text.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));

            return normalized.Length <= MaxBookmarkTextLength
                ? normalized
                : normalized[..MaxBookmarkTextLength];
        }

        private static string BuildBookmarkLabel(string selectedText)
        {
            if (selectedText.Length <= MaxBookmarkLabelLength)
            {
                return selectedText;
            }

            return selectedText[..MaxBookmarkLabelLength].TrimEnd() + "...";
        }
    }
}
