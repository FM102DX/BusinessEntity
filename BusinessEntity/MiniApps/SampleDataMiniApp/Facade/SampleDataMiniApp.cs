using BusinessEntity.MiniApps.SampleDataMiniApp.Contracts;

namespace BusinessEntity.MiniApps.SampleDataMiniApp.Facade
{
    // Представляет фасад mini-app тестовой заливки и делегирует запуск существующему сидеру.
    internal sealed class SampleDataMiniApp : ISampleDataMiniApp
    {
        private readonly ISampleDataService _sampleDataService;

        // Сохраняет существующий сервис заливки, вокруг которого строится mini-app.
        public SampleDataMiniApp(ISampleDataService sampleDataService)
        {
            _sampleDataService = sampleDataService;
        }

        // Запускает существующую логику инициализации тестовых данных через mini-app-обёртку.
        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
        {
            return _sampleDataService.InitializeSampleDataAsync(cancellationToken);
        }

        // Принудительно запускает повторную заливку данных через mini-app-обертку.
        public Task ForceReseedAsync(CancellationToken cancellationToken = default)
        {
            return _sampleDataService.ForceReseedAsync(cancellationToken);
        }
    }
}
