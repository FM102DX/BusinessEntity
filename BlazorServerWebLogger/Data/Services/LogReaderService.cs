using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.WebLogger.DataAccess;
using SampleOnlineMall.WebLogger.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorServerWebLogger.Data.Services
{
    public class LogReaderService
    {
        private readonly ThreadSafeDbContextFactory _dbContextFactory;
        private readonly object _lock = new(); // Блокировка для синхронизации
        private WebLoggerDbContext _dbContext; // Один общий экземпляр DbContext

        public LogReaderService(ThreadSafeDbContextFactory dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));

            // Создаём один экземпляр DbContext
            _dbContext = _dbContextFactory.GetDbContext();
        }

        // Метод для начальной загрузки первых n записей
        public async Task<ObservableCollection<LogEntryDbStorable>> ReadInitialAsync(int n = 50)
        {
            lock (_lock)
            {
                try
                {
                    var entries = _dbContext.LogEntries
                        .OrderByDescending(entry => entry.Timestamp)
                        .Take(n)
                        .ToList();

                    return new ObservableCollection<LogEntryDbStorable>(entries);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при чтении начальных записей: {ex.Message}");

                    // Пересоздаём DbContext в случае ошибки
                    _dbContext?.Dispose();
                    _dbContext = _dbContextFactory.GetDbContext();

                    throw;
                }
            }
        }

        // Метод для чтения новых записей, появившихся после указанного времени
        public async Task<List<LogEntryDbStorable>> ReadNewEntriesAsync(DateTime lastTimestamp)
        {
            lock (_lock)
            {
                try
                {
                    return _dbContext.LogEntries
                        .Where(entry => entry.Timestamp > lastTimestamp)
                        .OrderByDescending(entry => entry.Timestamp)
                        .ToList();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при чтении новых записей: {ex.Message}");

                    // Пересоздаём DbContext в случае ошибки
                    _dbContext?.Dispose();
                    _dbContext = _dbContextFactory.GetDbContext();

                    throw;
                }
            }
        }

        public async Task<int> GetTotalLogCount()
        {
            lock (_lock)
            {
                try
                {
                    return _dbContext.LogEntries.Count();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при подсчёте записей: {ex.Message}");

                    // Пересоздаём DbContext в случае ошибки
                    _dbContext?.Dispose();
                    _dbContext = _dbContextFactory.GetDbContext();

                    throw;
                }
            }
        }


        public void Dispose()
        {
            lock (_lock)
            {
                if (_dbContext != null && _dbContext.Database.GetDbConnection().State != System.Data.ConnectionState.Closed)
                {
                    _dbContext.Dispose();
                }
                _dbContext = null; // Явно обнуляем, чтобы избежать повторного использования
            }
        }
    }
}
