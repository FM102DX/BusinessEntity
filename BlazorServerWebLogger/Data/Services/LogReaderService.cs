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
            try
            {
                var result = await _repo.GetAllAsync(
                    filter: entry => entry.Timestamp >= lastTimestamp, null // Берем >= и режем дубли уже по Id на уровне UI
                );

                return result.Items
                    .OrderByDescending(entry => entry.Timestamp)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGREADER-ERROR] Ошибка чтения новых записей: {ex.Message}");
                if (ex.Message.Contains("Failed to connect") || ex.Message.Contains("timeout") || ex.Message.Contains("172.22.0."))
                {
                    Console.WriteLine($"[LOGREADER-ERROR] 🔥 СЕТЕВАЯ ОШИБКА при чтении логов!");
                    Console.WriteLine($"[LOGREADER-ERROR] LastTimestamp: {lastTimestamp:yyyy-MM-dd HH:mm:ss}");
                }
                throw; // пробрасываем для обработки выше
            }
        }

        // Метод для получения общего количества логов
        public async Task<int> GetTotalLogCount()
        {
            try
            {
                return await _repo.GetCountAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGREADER-ERROR] Ошибка получения количества логов: {ex.Message}");
                if (ex.Message.Contains("Failed to connect") || ex.Message.Contains("timeout") || ex.Message.Contains("172.22.0."))
                {
                    Console.WriteLine($"[LOGREADER-ERROR] 🔥 СЕТЕВАЯ ОШИБКА при подсчете логов!");
                }
                throw;
            }
        }

        // Новый метод для удаления всех записей
        public async Task DeleteAllLogsAsync()
        {
            await _repo.DeleteAllAsync();
        }
    }
}
