using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.WebLogger.DataAccess;
using SampleOnlineMall.WebLogger.Models;
using System.Collections.ObjectModel;

namespace BlazorServerWebLogger.Data
{
    public class LogReaderService
    {
        private readonly WebLoggerDbContext _context;

        public LogReaderService(WebLoggerDbContext context)
        {
            _context = context;
        }

        // Метод для начальной загрузки первых n записей
        public async Task<ObservableCollection<LogEntryDbStorable>> ReadInitialAsync(int n = 50)
        {
            var entries = await _context.LogEntries
                .OrderBy(entry => entry.Timestamp)
                .Take(n)
                .ToListAsync();

            return new ObservableCollection<LogEntryDbStorable>(entries);
        }

        // Метод для чтения новых записей, появившихся после указанного времени
        public async Task<List<LogEntryDbStorable>> ReadNewEntriesAsync(DateTime lastTimestamp)
        {
            return await _context.LogEntries
                .Where(entry => entry.Timestamp > lastTimestamp)
                .OrderBy(entry => entry.Timestamp)
                .ToListAsync();
        }
    }


}
