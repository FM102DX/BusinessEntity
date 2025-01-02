using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BlazorServerWebLogger.Contracts;
using SampleOnlineMall.WebLogger.Models;

namespace BlazorServerWebLogger.Data.Services
{
    public class LogReaderService
    {
        private readonly IAsyncRepository<LogEntryDbStorable> _repo;

        public LogReaderService(IAsyncRepository<LogEntryDbStorable> repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        // Метод для начальной загрузки первых n записей
        public async Task<ObservableCollection<LogEntryDbStorable>> ReadInitialAsync(int n = 50)
        {
            var result = await _repo.GetAllAsync(null, n);

            // Сортируем по убыванию времени и берем только первые n записей
            var entries = result.Items
                .OrderByDescending(entry => entry.Timestamp)
                .ToList();

            return new ObservableCollection<LogEntryDbStorable>(entries);
        }

        // Метод для чтения новых записей, появившихся после указанного времени
        public async Task<List<LogEntryDbStorable>> ReadNewEntriesAsync(DateTime lastTimestamp)
        {
            var result = await _repo.GetAllAsync(
                filter: entry => entry.Timestamp > lastTimestamp, null // Фильтруем по времени
            );

            return result.Items
                .OrderByDescending(entry => entry.Timestamp)
                .ToList();
        }

        // Метод для получения общего количества логов
        public async Task<int> GetTotalLogCount()
        {
            return await _repo.GetCountAsync();
        }

        // Новый метод для удаления всех записей
        public async Task DeleteAllLogsAsync()
        {
            await _repo.DeleteAllAsync();
        }
    }
}