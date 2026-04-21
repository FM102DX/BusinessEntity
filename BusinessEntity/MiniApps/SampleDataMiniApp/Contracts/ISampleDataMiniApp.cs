namespace BusinessEntity.MiniApps.SampleDataMiniApp.Contracts
{
    // Определяет публичный контракт mini-app, который запускает заливку тестовых данных.
    public interface ISampleDataMiniApp
    {
        // Инициализирует mini-app и запускает заливку тестовых данных.
        Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
    }
}
