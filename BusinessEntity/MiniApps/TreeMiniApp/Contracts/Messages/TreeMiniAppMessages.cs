using BusinessEntity.Core.Classes;

namespace BusinessEntity.MiniApps.TreeMiniApp.Contracts.Messages
{
    // Снимок узла дерева для передачи через bus.
    public sealed record TreeNodeSnapshot(
        BusinessEntity.Core.Classes.BusinessEntity Entity,
        IReadOnlyList<TreeNodeSnapshot> Children);

    // Снимок дерева выбранного пространства.
    public sealed record TreeSpaceSnapshot(
        BusinessEntity.Core.Classes.BusinessEntity Space,
        IReadOnlyList<TreeNodeSnapshot> Children);

    // Запрос на полную загрузку дерева пространства.
    public sealed record GetTreeForSpaceRequest(Guid RequestId, Guid SpaceId);

    // Ответ со снимком дерева пространства.
    public sealed record GetTreeForSpaceResponse(Guid RequestId, TreeSpaceSnapshot? Snapshot, string? ErrorMessage = null);
}
