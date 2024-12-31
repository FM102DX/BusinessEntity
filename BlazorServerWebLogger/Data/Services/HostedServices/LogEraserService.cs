using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SampleOnlineMall.WebLogger.DataAccess;
using BlazorServerWebLogger.Data;

namespace BlazorServerWebLogger.Data.Services.HostedServices
{
    public class LogEraserService : IHostedService, IDisposable
    {
        private readonly ThreadSafeDbContextFactory _dbContextFactory;
        private readonly int _erasePeriod;
        private readonly int _logsTargetCount;
        private readonly object _lock = new(); // Блокировка для синхронизации доступа к DbContext
        private CancellationTokenSource _cancellationTokenSource;

        public LogEraserService(
            ThreadSafeDbContextFactory dbContextFactory,
            IOptions<LogEraserSettings> settings)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _erasePeriod = settings.Value.ErasePeriod;
            _logsTargetCount = settings.Value.LogsTargetCount;
            Console.WriteLine($"Период удаления логов (ErasePeriod): {_erasePeriod} мс");
            Console.WriteLine($"Целевое количество логов (LogsTargetCount): {_logsTargetCount}");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Task.Run(() => EraseOldLogsAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

            return Task.CompletedTask;
        }

        private async Task EraseOldLogsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using (var contextWrp = _dbContextFactory.GetDbContext())
                {
                    var dbContext = contextWrp.Context;
                    var totalLogs = dbContext.LogEntries.Count();
                    if (totalLogs > _logsTargetCount)
                    {
                        var logsToRemove = totalLogs - _logsTargetCount;
                        var oldestLogs = dbContext.LogEntries
                            .OrderBy(log => log.Timestamp)
                            .Take(logsToRemove)
                            .ToList();
                        dbContext.LogEntries.RemoveRange(oldestLogs);
                        dbContext.SaveChanges();
                        Console.WriteLine($"Удалено {logsToRemove} старых логов.");
                    }
                }
                await Task.Delay(_erasePeriod, cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource?.Cancel();
            return Task.CompletedTask;
        }

        public void Dispose()
        {

        }
    }
}
