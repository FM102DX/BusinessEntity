namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts
{
    /// <summary>
    /// Публичный фасад mini-app хранения данных.
    /// Отвечает только за инициализацию внутренних подписок и инфраструктуры.
    /// </summary>
    public interface IDataProviderMiniApp
    {
        void EnsureInitialized();
    }
}
