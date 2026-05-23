using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Services;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.MiniApps.TreeMiniApp.Contracts.Messages;
using BusinessEntity.MiniApps.UserMiniApp.Contracts;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Connectors;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;
using BusinessEntity.Services;
using BusinessEntity.WebLogger.Services;

namespace BusinessEntity.MiniApps.TreeMiniApp.Internal
{
    // Инкапсулирует серверные операции дерева поверх BusinessEntityHelper.
    internal sealed class TreeMiniAppService
    {
        private readonly BusinessEntityHelper _businessEntityHelper;
        private readonly RichTextDocumentHelper _richTextDocumentHelper;
        private readonly IUserConnector _userConnector;
        private readonly IWebLoggerService? _webLogger;

        public TreeMiniAppService(
            BusinessEntityHelper businessEntityHelper,
            RichTextDocumentHelper richTextDocumentHelper,
            IUserConnector userConnector,
            IWebLoggerService? webLogger)
        {
            _businessEntityHelper = businessEntityHelper;
            _richTextDocumentHelper = richTextDocumentHelper;
            _userConnector = userConnector;
            _webLogger = webLogger;
        }

        // Возвращает полный снимок дерева для пространства.
        public async Task<TreeSpaceSnapshot?> GetTreeForSpaceAsync(Guid spaceId, CancellationToken cancellationToken = default)
        {
            var space = await _businessEntityHelper.GetBusinessEntityById(spaceId);
            if (space == null || space.EntityType != BusinessEntityTypeEnum.Space)
            {
                return null;
            }

            var currentUser = await _userConnector.EnsureCurrentUserAsync(cancellationToken);
            var currentBusinessUser = await _userConnector.GetCurrentUserAsync(cancellationToken);
            var permissions = await _userConnector.GetCurrentUserPermissionsForSpaceAsync(space.Id, cancellationToken);
            if (!IsAccessAdmin(currentBusinessUser) &&
                !permissions.CanViewPublished &&
                !permissions.CanViewDraft)
            {
                return null;
            }

            var children = await BuildChildrenAsync(
                space.Id,
                currentUser?.Id,
                IsAccessAdmin(currentBusinessUser),
                permissions,
                cancellationToken);
            return new TreeSpaceSnapshot(space, children);
        }

        // Создает дочернюю сущность под выбранным родителем.
        public async Task<BusinessEntity.Core.Classes.BusinessEntity> CreateEntityAsync(Guid parentId, BusinessEntityTypeEnum entityType, CancellationToken cancellationToken = default)
        {
            await EnsureAuthenticatedMutationAsync(cancellationToken);
            var parent = await _businessEntityHelper.GetBusinessEntityById(parentId)
                ?? throw new InvalidOperationException($"Parent entity '{parentId}' was not found.");

            return entityType switch
            {
                BusinessEntityTypeEnum.Folder => await _businessEntityHelper.CreateSubFolderAsync(parent, cancellationToken),
                BusinessEntityTypeEnum.Document => await _businessEntityHelper.CreateDocumentAsync(parent, cancellationToken),
                BusinessEntityTypeEnum.RichTextDocument => await _richTextDocumentHelper.CreateRichTextDocumentAsync(parent, cancellationToken),
                _ => throw new InvalidOperationException($"Tree create is not supported for entity type '{entityType}'.")
            };
        }

        // Переименовывает сущность дерева.
        public async Task<BusinessEntity.Core.Classes.BusinessEntity?> RenameEntityAsync(Guid entityId, string newName, CancellationToken cancellationToken = default)
        {
            await EnsureAuthenticatedMutationAsync(cancellationToken);
            return await _businessEntityHelper.RenameEntity(entityId, newName, cancellationToken);
        }

        // Удаляет набор сущностей дерева.
        public async Task DeleteEntitiesAsync(IReadOnlyList<Guid> entityIds, CancellationToken cancellationToken = default)
        {
            await EnsureAuthenticatedMutationAsync(cancellationToken);
            foreach (var entityId in entityIds.Distinct())
            {
                await _businessEntityHelper.RemoveBusinessEntity(entityId);
            }
        }

        // Перемещает набор сущностей к новому родителю.
        public async Task MoveEntitiesAsync(IReadOnlyList<Guid> entityIds, Guid targetParentId, CancellationToken cancellationToken = default)
        {
            await EnsureAuthenticatedMutationAsync(cancellationToken);
            var targetParent = await _businessEntityHelper.GetBusinessEntityById(targetParentId)
                ?? throw new InvalidOperationException($"Target parent '{targetParentId}' was not found.");

            foreach (var entityId in entityIds.Distinct())
            {
                var entity = await _businessEntityHelper.GetBusinessEntityById(entityId)
                    ?? throw new InvalidOperationException($"Entity '{entityId}' was not found.");

                await _businessEntityHelper.ChangeVisualFolderParentForItem(entity, targetParent);
            }
        }

        // Рекурсивно строит дочерние узлы дерева.
        private async Task<IReadOnlyList<TreeNodeSnapshot>> BuildChildrenAsync(
            Guid parentId,
            Guid? currentUserId,
            bool isAccessAdmin,
            UserEffectivePermissions permissions,
            CancellationToken cancellationToken)
        {
            var children = await _businessEntityHelper.GetContainedEntitiesAsync(parentId, cancellationToken);
            var snapshots = new List<TreeNodeSnapshot>();

            foreach (var child in children)
            {
                var descendants = await BuildChildrenAsync(child.Id, currentUserId, isAccessAdmin, permissions, cancellationToken);
                if (await CanDisplayTreeEntityAsync(child, currentUserId, isAccessAdmin, permissions, cancellationToken) || descendants.Count > 0)
                {
                    snapshots.Add(new TreeNodeSnapshot(child, descendants));
                }
            }

            return snapshots;
        }

        // Проверяет видимость документа в дереве с учетом владельца, общего режима и публикации.
        private async Task<bool> CanDisplayTreeEntityAsync(
            BusinessEntity.Core.Classes.BusinessEntity entity,
            Guid? currentUserId,
            bool isAccessAdmin,
            UserEffectivePermissions permissions,
            CancellationToken cancellationToken)
        {
            if (!permissions.CanViewPublished && !permissions.CanViewDraft && !isAccessAdmin)
            {
                return false;
            }

            if (!IsDocumentEntity(entity.EntityType))
            {
                return isAccessAdmin || permissions.CanViewDraft || permissions.CanViewPublished;
            }

            if (isAccessAdmin || IsOwner(entity, currentUserId) || permissions.CanViewDraft)
            {
                return true;
            }

            if (!permissions.CanViewPublished)
            {
                return false;
            }

            if (entity.IsPublic)
            {
                return true;
            }

            var publishedVersion = await GetPublishedVersionAsync(entity, cancellationToken);
            return publishedVersion > 0;
        }

        // Запрещает anonymous-режиму выполнять изменения дерева.
        private async Task EnsureAuthenticatedMutationAsync(CancellationToken cancellationToken)
        {
            var currentUser = await _userConnector.GetCurrentUserAsync(cancellationToken);
            if (currentUser?.IsAuthenticated == true)
            {
                return;
            }

            throw new UnauthorizedAccessException("Для изменения дерева требуется вход.");
        }

        private async Task<int> GetPublishedVersionAsync(
            BusinessEntity.Core.Classes.BusinessEntity entity,
            CancellationToken cancellationToken)
        {
            try
            {
                return entity.EntityType switch
                {
                    BusinessEntityTypeEnum.Document => (await _businessEntityHelper.GetEntityWithDataAsync<Document>(entity.Id, cancellationToken))?.Data.PublishedVersion ?? 0,
                    BusinessEntityTypeEnum.RichTextDocument => (await _businessEntityHelper.GetEntityWithDataAsync<RichTextDocument>(entity.Id, cancellationToken))?.Data.PublishedVersion ?? 0,
                    _ => 0
                };
            }
            catch (Exception ex)
            {
                if (_webLogger != null)
                {
                    await _webLogger.Error(ex);
                }
                return 0;
            }
        }

        private static bool IsOwner(BusinessEntity.Core.Classes.BusinessEntity entity, Guid? currentUserId)
        {
            return currentUserId.HasValue && entity.CreatedByUserId == currentUserId.Value;
        }

        private static bool IsDocumentEntity(BusinessEntityTypeEnum entityType)
        {
            return entityType == BusinessEntityTypeEnum.Document ||
                   entityType == BusinessEntityTypeEnum.RichTextDocument;
        }

        private static bool IsAccessAdmin(BusinessEntityUser? user)
        {
            return user?.IsAkadmin == true ||
                   user?.IsGeneralAdmin == true ||
                   string.Equals(user?.UserName, "admin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
