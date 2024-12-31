using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.WebLogger.DataAccess;
using SampleOnlineMall.WebLogger.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlazorServerWebLogger.Contracts;

namespace BlazorServerWebLogger.Data.Services
{
    public class LogReaderService
    {
        private readonly ThreadSafeDbContextFactory _dbContextFactory;
        private readonly object _lock = new(); // Блокировка для синхронизации
        private readonly IAsyncRepository<LogEntryDbStorable> _repo;

        public LogReaderService(IAsyncRepository<LogEntryDbStorable> repo)
        {
            _repo = repo;
        }

        // Метод для начальной загрузки первых n записей
        public async Task<ObservableCollection<LogEntryDbStorable>> ReadInitialAsync(int n = 50)
        {
            using (var contextWrp = _dbContextFactory.GetDbContext())
            {
                var dbContext = contextWrp.Context;
                var entries = dbContext.LogEntries
                    .OrderByDescending(entry => entry.Timestamp)
                    .Take(n)
                    .ToList();
                return new ObservableCollection<LogEntryDbStorable>(entries);
            }
        }

        // Метод для чтения новых записей, появившихся после указанного времени
        public async Task<List<LogEntryDbStorable>> ReadNewEntriesAsync(DateTime lastTimestamp)
        {
            using (var contextWrp = _dbContextFactory.GetDbContext())
            {
                var dbContext = contextWrp.Context;
                return dbContext.LogEntries
                    .Where(entry => entry.Timestamp > lastTimestamp)
                    .OrderByDescending(entry => entry.Timestamp)
                    .ToList();
            }
        }

        public async Task<int> GetTotalLogCount()
        {
            using (var contextWrp = _dbContextFactory.GetDbContext())
            {
                var dbContext = contextWrp.Context;
                return dbContext.LogEntries.Count();
            }
        }


        public void Dispose()
        {

        }
    }
}
