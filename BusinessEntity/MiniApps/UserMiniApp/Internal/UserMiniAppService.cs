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
        private readonly AuthentikManagementClient _authentikManagementClient;
        private readonly IUserMiniAppRepository<UserDto> _userRepository;
        private readonly IUserMiniAppRepository<UserPropertyDto> _userPropertyRepository;

        // Получает state mini-app и фабрику, которая умеет собирать пользователя из Authentik principal.
        public UserMiniAppService(
            UserMiniAppState state,
            BusinessEntityUserFactory userFactory,
            AuthentikManagementClient authentikManagementClient,
            IUserMiniAppRepository<UserDto> userRepository,
            IUserMiniAppRepository<UserPropertyDto> userPropertyRepository)
        {
            _state = state;
            _userFactory = userFactory;
            _authentikManagementClient = authentikManagementClient;
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

            var authentikLogin = string.IsNullOrWhiteSpace(currentUser.UserName)
                ? externalId
                : currentUser.UserName;

            if (existingUser != null)
            {
                var userData = ReadUserData(existingUser);
                var shouldUpdate = false;

                if (string.IsNullOrWhiteSpace(userData.ExtId))
                {
                    userData.ExtId = externalId;
                    shouldUpdate = true;
                }

                if (string.IsNullOrWhiteSpace(userData.AuthentikLogin))
                {
                    userData.AuthentikLogin = authentikLogin;
                    shouldUpdate = true;
                }

                if (string.IsNullOrWhiteSpace(userData.DisplayedName))
                {
                    userData.DisplayedName = authentikLogin;
                    shouldUpdate = true;
                }

                if (shouldUpdate)
                {
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
                    AuthentikLogin = authentikLogin,
                    DisplayedName = authentikLogin,
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

        // Возвращает пользователей приложения из Authentik и материализует их локальные DTO.
        public async Task<IReadOnlyList<UserAdministrationRecord>> GetAdministrationUsersAsync(CancellationToken cancellationToken = default)
        {
            var authentikUsers = await _authentikManagementClient.GetApplicationUsersAsync(cancellationToken);
            var localUsers = (await _userRepository.GetAllAsync(null, cancellationToken)).ToList();
            var records = new List<UserAdministrationRecord>();

            foreach (var authentikUser in authentikUsers)
            {
                var localUser = await UpsertLocalUserFromAuthentikAsync(authentikUser, localUsers, cancellationToken);
                records.Add(MapAdministrationRecord(localUser, authentikUser));
            }

            return records
                .OrderBy(user => string.IsNullOrWhiteSpace(user.DisplayedName) ? user.AuthentikLogin : user.DisplayedName)
                .ThenBy(user => user.AuthentikLogin)
                .ThenBy(user => user.ExternalId)
                .ToList();
        }

        // Создает пользователя в Authentik, перечитывает Authentik-список и материализует локальную DTO.
        public async Task<UserAdministrationRecord> CreateAdministrationUserAsync(CancellationToken cancellationToken = default)
        {
            var createdAuthentikUser = await _authentikManagementClient.CreateApplicationUserAsync(cancellationToken);
            var authentikUsers = await _authentikManagementClient.GetApplicationUsersAsync(cancellationToken);
            var reloadedAuthentikUser = authentikUsers.FirstOrDefault(user => user.Pk == createdAuthentikUser.Pk)
                                        ?? createdAuthentikUser;
            var localUsers = (await _userRepository.GetAllAsync(null, cancellationToken)).ToList();
            var localUser = await UpsertLocalUserFromAuthentikAsync(reloadedAuthentikUser, localUsers, cancellationToken);
            return MapAdministrationRecord(localUser, reloadedAuthentikUser);
        }

        // Обновляет Authentik username при необходимости, затем сохраняет локальное отображаемое имя.
        public async Task<UserAdministrationRecord> UpdateAdministrationUserAsync(
            Guid userId,
            UserAdministrationSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("Пользователь не выбран.", nameof(userId));
            }

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                throw new KeyNotFoundException("Пользователь не найден.");
            }

            var authentikLogin = NormalizeRequiredText(request.AuthentikLogin, "Логин в аутентик");
            var displayedName = NormalizeOptionalText(request.DisplayedName);
            if (string.IsNullOrWhiteSpace(displayedName))
            {
                displayedName = authentikLogin;
            }

            var userData = ReadUserData(user);
            var authentikUser = await ResolveAuthentikUserAsync(userData, user.ExternalId, cancellationToken);
            if (!string.Equals(authentikUser.Username, authentikLogin, StringComparison.Ordinal))
            {
                authentikUser = await _authentikManagementClient.UpdateUsernameAsync(
                    authentikUser.Pk,
                    authentikLogin,
                    cancellationToken);
            }

            user.ExternalId = authentikUser.Uid;
            user.Payload = SerializeUserData(new UserData
            {
                AuthentikUserPk = authentikUser.Pk,
                AuthentikUserUuid = authentikUser.Uuid,
                AuthentikLogin = authentikUser.Username,
                DisplayedName = displayedName,
                ExtId = authentikUser.Uid
            });
            user.DateLastModified = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);
            return MapAdministrationRecord(user, authentikUser);
        }

        // Удаляет Authentik-пользователя, затем локальную user-запись и все ее user properties.
        public async Task<bool> DeleteAdministrationUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                return false;
            }

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                return false;
            }

            var userData = ReadUserData(user);
            var authentikUser = await ResolveAuthentikUserAsync(userData, user.ExternalId, cancellationToken);
            await _authentikManagementClient.DeleteUserAsync(authentikUser.Pk, cancellationToken);

            var properties = await _userPropertyRepository.GetAllAsync(
                property => property.ParentEntityId == userId,
                cancellationToken);

            // User properties подчинены UserDto и не должны оставаться orphan-строками.
            foreach (var property in properties)
            {
                await _userPropertyRepository.DeleteAsync(property.Id, cancellationToken);
            }

            await _userRepository.DeleteAsync(userId, cancellationToken);
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
            UserData userData;
            if (string.IsNullOrWhiteSpace(user.Payload))
            {
                userData = new UserData { ExtId = user.ExternalId };
            }
            else
            {
                try
                {
                    userData = JsonSerializer.Deserialize<UserData>(user.Payload, UserMiniAppJsonOptions.Default)
                               ?? new UserData { ExtId = user.ExternalId };
                }
                catch (JsonException)
                {
                    userData = new UserData { ExtId = user.ExternalId };
                }
            }

            userData.ExtId = string.IsNullOrWhiteSpace(userData.ExtId) ? user.ExternalId : userData.ExtId;
            return userData;
        }

        private static string SerializeUserData(UserData userData)
        {
            return JsonSerializer.Serialize(userData, UserMiniAppJsonOptions.Default);
        }

        // Создает или обновляет локальную DTO по записи Authentik.
        private async Task<UserDto> UpsertLocalUserFromAuthentikAsync(
            AuthentikUserRecord authentikUser,
            List<UserDto> localUsers,
            CancellationToken cancellationToken)
        {
            var localUser = FindLocalUser(authentikUser, localUsers);
            if (localUser == null)
            {
                var now = DateTime.UtcNow;
                var created = new UserDto
                {
                    Id = Guid.NewGuid(),
                    ExternalId = authentikUser.Uid,
                    Payload = SerializeUserData(BuildUserData(authentikUser, authentikUser.Username)),
                    DateCreated = now,
                    DateLastModified = now
                };

                created = await _userRepository.AddAsync(created, cancellationToken);
                localUsers.Add(created);
                return created;
            }

            var currentData = ReadUserData(localUser);
            var currentDisplayedName = NormalizeOptionalText(currentData.DisplayedName);
            var previousLogin = NormalizeOptionalText(currentData.AuthentikLogin);
            var displayedName = string.IsNullOrWhiteSpace(currentDisplayedName) ||
                                string.Equals(currentDisplayedName, previousLogin, StringComparison.Ordinal)
                ? authentikUser.Username
                : currentDisplayedName;
            var nextData = BuildUserData(authentikUser, displayedName);
            var shouldUpdate =
                !string.Equals(localUser.ExternalId, authentikUser.Uid, StringComparison.Ordinal) ||
                !UserDataEquals(currentData, nextData);

            if (!shouldUpdate)
            {
                return localUser;
            }

            localUser.ExternalId = authentikUser.Uid;
            localUser.Payload = SerializeUserData(nextData);
            localUser.DateLastModified = DateTime.UtcNow;
            await _userRepository.UpdateAsync(localUser, cancellationToken);
            return localUser;
        }

        // Находит Authentik-пользователя для локальной DTO из актуального списка пользователей приложения.
        private async Task<AuthentikUserRecord> ResolveAuthentikUserAsync(
            UserData userData,
            string externalId,
            CancellationToken cancellationToken)
        {
            var authentikUsers = await _authentikManagementClient.GetApplicationUsersAsync(cancellationToken);
            var authentikUser = authentikUsers.FirstOrDefault(user =>
                userData.AuthentikUserPk > 0 && user.Pk == userData.AuthentikUserPk);

            authentikUser ??= authentikUsers.FirstOrDefault(user =>
                !string.IsNullOrWhiteSpace(userData.AuthentikUserUuid) &&
                string.Equals(user.Uuid, userData.AuthentikUserUuid, StringComparison.OrdinalIgnoreCase));
            authentikUser ??= authentikUsers.FirstOrDefault(user =>
                !string.IsNullOrWhiteSpace(userData.ExtId) &&
                string.Equals(user.Uid, userData.ExtId, StringComparison.OrdinalIgnoreCase));
            authentikUser ??= authentikUsers.FirstOrDefault(user =>
                !string.IsNullOrWhiteSpace(externalId) &&
                string.Equals(user.Uid, externalId, StringComparison.OrdinalIgnoreCase));
            authentikUser ??= authentikUsers.FirstOrDefault(user =>
                !string.IsNullOrWhiteSpace(userData.AuthentikLogin) &&
                string.Equals(user.Username, userData.AuthentikLogin, StringComparison.OrdinalIgnoreCase));

            if (authentikUser == null)
            {
                throw new InvalidOperationException("Пользователь не найден среди пользователей приложения в Authentik.");
            }

            return authentikUser;
        }

        // Формирует DTO административного UI из Authentik user и локального payload.
        private static UserAdministrationRecord MapAdministrationRecord(
            UserDto user,
            AuthentikUserRecord authentikUser)
        {
            var userData = ReadUserData(user);
            var displayedName = NormalizeOptionalText(userData.DisplayedName);

            return new UserAdministrationRecord
            {
                Id = user.Id,
                AuthentikUserPk = authentikUser.Pk,
                AuthentikUserUuid = authentikUser.Uuid,
                ExternalId = authentikUser.Uid,
                AuthentikLogin = authentikUser.Username,
                DisplayedName = string.IsNullOrWhiteSpace(displayedName) ? authentikUser.Username : displayedName,
                IsActive = authentikUser.IsActive,
                DateCreated = user.DateCreated,
                DateLastModified = user.DateLastModified
            };
        }

        private static UserDto? FindLocalUser(AuthentikUserRecord authentikUser, IEnumerable<UserDto> localUsers)
        {
            foreach (var localUser in localUsers)
            {
                var userData = ReadUserData(localUser);
                if (userData.AuthentikUserPk > 0 && userData.AuthentikUserPk == authentikUser.Pk)
                {
                    return localUser;
                }

                if (!string.IsNullOrWhiteSpace(userData.AuthentikUserUuid) &&
                    string.Equals(userData.AuthentikUserUuid, authentikUser.Uuid, StringComparison.OrdinalIgnoreCase))
                {
                    return localUser;
                }

                if (string.Equals(localUser.ExternalId, authentikUser.Uid, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(userData.ExtId, authentikUser.Uid, StringComparison.OrdinalIgnoreCase))
                {
                    return localUser;
                }
            }

            return null;
        }

        private static UserData BuildUserData(AuthentikUserRecord authentikUser, string displayedName)
        {
            return new UserData
            {
                AuthentikUserPk = authentikUser.Pk,
                AuthentikUserUuid = authentikUser.Uuid,
                AuthentikLogin = authentikUser.Username,
                DisplayedName = displayedName,
                ExtId = authentikUser.Uid
            };
        }

        private static bool UserDataEquals(UserData left, UserData right)
        {
            return left.AuthentikUserPk == right.AuthentikUserPk &&
                   string.Equals(left.AuthentikUserUuid, right.AuthentikUserUuid, StringComparison.Ordinal) &&
                   string.Equals(left.AuthentikLogin, right.AuthentikLogin, StringComparison.Ordinal) &&
                   string.Equals(left.DisplayedName, right.DisplayedName, StringComparison.Ordinal) &&
                   string.Equals(left.ExtId, right.ExtId, StringComparison.Ordinal);
        }

        // Нормализует обязательное короткое текстовое поле.
        private static string NormalizeRequiredText(string? value, string fieldName)
        {
            var normalized = NormalizeOptionalText(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException($"{fieldName} не может быть пустым.", fieldName);
            }

            return normalized;
        }

        // Нормализует пробелы в коротком текстовом поле.
        private static string NormalizeOptionalText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return string.Join(
                " ",
                value.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
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
