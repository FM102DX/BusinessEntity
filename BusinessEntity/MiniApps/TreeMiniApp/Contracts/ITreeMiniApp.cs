namespace BusinessEntity.MiniApps.TreeMiniApp.Contracts
{
    // Фасад mini-app дерева.
    public interface ITreeMiniApp
    {
        // Явно активирует bus-подписки mini-app.
        void EnsureInitialized();
    }
}
