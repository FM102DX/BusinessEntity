using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.WebLogger.DataAccess;
using SampleOnlineMall.WebLogger.Models;
using System.Collections.ObjectModel;

namespace BlazorServerWebLogger.Data
{
    public class LogReaderService
    {
        private readonly ThreadSafeDbContextFactory _dbContextFactory;

        public LogReaderService(ThreadSafeDbContextFactory dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        }

        // Метод для начальной загрузки первых n записей
        public async Task<ObservableCollection<LogEntryDbStorable>> ReadInitialAsync(int n = 50)
        {
            try
            {
                using var context = _dbContextFactory.GetDbContext();

                var entries = await context.LogEntries
                    .OrderBy(entry => entry.Timestamp)
                    .Take(n)
                    .ToListAsync();

                return new ObservableCollection<LogEntryDbStorable>(entries);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при чтении начальных записей: {ex.Message}");

                // Пересоздаем контекст в случае ошибки
                _dbContextFactory.DisposeDbContext();
                throw; // Пробрасываем исключение дальше, чтобы вызвать повторную обработку, если требуется
            }
        }

        // Метод для чтения новых записей, появившихся после указанного времени
        public async Task<List<LogEntryDbStorable>> ReadNewEntriesAsync(DateTime lastTimestamp)
        {
            try
            {
                using var context = _dbContextFactory.GetDbContext();

                return await context.LogEntries
                    .Where(entry => entry.Timestamp > lastTimestamp)
                    .OrderBy(entry => entry.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при чтении новых записей: {ex.Message}");

                // Пересоздаем контекст в случае ошибки
                _dbContextFactory.DisposeDbContext();
                throw; // Пробрасываем исключение дальше, чтобы вызвать повторную обработку, если требуется
            }
        }
    }
}
