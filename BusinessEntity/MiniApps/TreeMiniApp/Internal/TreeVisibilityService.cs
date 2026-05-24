using BusinessEntity.Core.Classes;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.Core.Services;
using BusinessEntity.MiniApps.TreeMiniApp.Contracts.Messages;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;
using BusinessEntity.WebLogger.Services;

namespace BusinessEntity.MiniApps.TreeMiniApp.Internal
{
    // Формирует видимую для пользователя часть дерева пространства.
    internal sealed class TreeVisibilityService
    {
        private const bool ShowFoldersWithoutVisibleContent = false;

        private readonly BusinessEntityHelper _businessEntityHelper;
        private readonly IWebLoggerService? _webLogger;

        public TreeVisibilityService(
            BusinessEntityHelper businessEntityHelper,
            IWebLoggerService? webLogger)
        {
            _businessEntityHelper = businessEntityHelper;
            _webLogger = webLogger;
        }

        public async Task<IReadOnlyList<TreeNodeSnapshot>> BuildVisibleChildrenAsync(
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
                var descendants = await BuildVisibleChildrenAsync(
                    child.Id,
                    currentUserId,
                    isAccessAdmin,
                    permissions,
                    cancellationToken);

                if (IsFolderEntity(child.EntityType))
                {
                    if (descendants.Count > 0 ||
                        (ShowFoldersWithoutVisibleContent && CanDisplayContainer(permissions, isAccessAdmin)))
                    {
                        snapshots.Add(new TreeNodeSnapshot(child, descendants));
                    }

                    continue;
                }

                if (await CanDisplayTreeEntityAsync(child, currentUserId, isAccessAdmin, permissions, cancellationToken))
                {
                    snapshots.Add(new TreeNodeSnapshot(child, descendants));
                }
            }

            return snapshots;
        }

        private async Task<bool> CanDisplayTreeEntityAsync(
            BusinessEntity.Core.Classes.BusinessEntity entity,
            Guid? currentUserId,
            bool isAccessAdmin,
            UserEffectivePermissions permissions,
            CancellationToken cancellationToken)
        {
            if (!CanDisplayContainer(permissions, isAccessAdmin))
            {
                return false;
            }

            if (!IsDocumentEntity(entity.EntityType))
            {
                return true;
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

        private static bool CanDisplayContainer(UserEffectivePermissions permissions, bool isAccessAdmin)
        {
            return isAccessAdmin || permissions.CanViewDraft || permissions.CanViewPublished;
        }

        private static bool IsOwner(BusinessEntity.Core.Classes.BusinessEntity entity, Guid? currentUserId)
        {
            return currentUserId.HasValue && entity.CreatedByUserId == currentUserId.Value;
        }

        private static bool IsFolderEntity(BusinessEntityTypeEnum entityType)
        {
            return entityType == BusinessEntityTypeEnum.Folder;
        }

        private static bool IsDocumentEntity(BusinessEntityTypeEnum entityType)
        {
            return entityType == BusinessEntityTypeEnum.Document ||
                   entityType == BusinessEntityTypeEnum.RichTextDocument;
        }
    }
}
