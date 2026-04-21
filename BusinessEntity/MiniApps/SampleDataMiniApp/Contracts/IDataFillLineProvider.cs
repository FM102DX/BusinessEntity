using System.Threading;
using System.Threading.Tasks;

namespace BusinessEntity.MiniApps.SampleDataMiniApp.Contracts
{
    // Определяет источник строк для наполнения тестовых документов.
    public interface IDataFillLineProvider
    {
        // Возвращает следующую строку для заполнения тестовых данных.
        Task<string> GetNextLineAsync(CancellationToken ct = default);
    }
}
