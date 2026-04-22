using System.Threading;
using System.Threading.Tasks;

namespace BusinessEntity.MiniApps.SampleDataMiniApp.Contracts
{
    // Определяет внутренний контракт сервиса, который выполняет заливку тестовых данных.
    public interface ISampleDataService
    {
        // Запускает инициализацию тестовых данных.
        Task InitializeSampleDataAsync(CancellationToken ct = default);
        // Сбрасывает состояние сидера и принудительно запускает тестовую заливку снова.
        Task ForceReseedAsync(CancellationToken ct = default);
    }
}
