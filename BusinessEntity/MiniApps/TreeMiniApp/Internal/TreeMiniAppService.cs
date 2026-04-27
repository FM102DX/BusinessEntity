using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Services;
using BusinessEntity.MiniApps.TreeMiniApp.Contracts.Messages;
using BusinessEntity.Services;
using BusinessEntity.WebLogger.Services;

namespace BusinessEntity.MiniApps.TreeMiniApp.Internal
{
    // Инкапсулирует серверные операции дерева поверх BusinessEntityHelper.
    internal sealed class TreeMiniAppService
    {
        private readonly BusinessEntityHelper _businessEntityHelper;
        private readonly RichTextDocumentHelper _richTextDocumentHelper;
        private readonly IWebLoggerService? _webLogger;

        public TreeMiniAppService(
            BusinessEntityHelper businessEntityHelper,
            RichTextDocumentHelper richTextDocumentHelper,
            IWebLoggerService? webLogger)
        {
            _businessEntityHelper = businessEntityHelper;
            _richTextDocumentHelper = richTextDocumentHelper;
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

            var children = await BuildChildrenAsync(space.Id, cancellationToken);
            return new TreeSpaceSnapshot(space, children);
        }

        // Создает дочернюю сущность под выбранным родителем.
        public async Task<BusinessEntity.Core.Classes.BusinessEntity> CreateEntityAsync(Guid parentId, BusinessEntityTypeEnum entityType, CancellationToken cancellationToken = default)
        {
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
            return await _businessEntityHelper.RenameEntity(entityId, newName, cancellationToken);
        }

        // Удаляет набор сущностей дерева.
        public async Task DeleteEntitiesAsync(IReadOnlyList<Guid> entityIds, CancellationToken cancellationToken = default)
        {
            foreach (var entityId in entityIds.Distinct())
            {
                await _businessEntityHelper.RemoveBusinessEntity(entityId);
            }
        }

        // Перемещает набор сущностей к новому родителю.
        public async Task MoveEntitiesAsync(IReadOnlyList<Guid> entityIds, Guid targetParentId, CancellationToken cancellationToken = default)
        {
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
        private async Task<IReadOnlyList<TreeNodeSnapshot>> BuildChildrenAsync(Guid parentId, CancellationToken cancellationToken)
        {
            var children = await _businessEntityHelper.GetContainedEntitiesAsync(parentId, cancellationToken);
            var snapshots = new List<TreeNodeSnapshot>();

            foreach (var child in children)
            {
                var descendants = await BuildChildrenAsync(child.Id, cancellationToken);
                snapshots.Add(new TreeNodeSnapshot(child, descendants));
            }

            return snapshots;
        }
    }
}
