using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlazorServerWebLogger.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SampleOnlineMall.WebLogger.DataAccess;
using SampleOnlineMall.WebLogger.Models;
namespace BlazorServerWebLogger.Data.Services.HostedServices
{
    public class SampleLogGeneratorService : IHostedService, IDisposable
    {
        private readonly ThreadSafeDbContextFactory _dbContextFactory;
        private readonly int _sampleLogCreatePeriod;
        private readonly object _lock = new(); // Блокировка для синхронизации
        private CancellationTokenSource _cancellationTokenSource;

        public SampleLogGeneratorService(
            ThreadSafeDbContextFactory dbContextFactory,
            IOptions<SampleLogSettings> settings)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _sampleLogCreatePeriod = settings.Value.SampleLogCreatePeriod;
            Console.WriteLine($"Период создания логов (SampleLogCreatePeriod): {_sampleLogCreatePeriod} мс");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Task.Run(() => GenerateLogsAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

            return Task.CompletedTask;
        }

        private async Task GenerateLogsAsync(CancellationToken cancellationToken)
        {
            var random = new Random();

            while (!cancellationToken.IsCancellationRequested)
            {
                using (var contextWrp = _dbContextFactory.GetDbContext())
                {
                    var dbContext = contextWrp.Context;
                    var logEntry = new LogEntryDbStorable
                    {
                        Id = Guid.NewGuid(),
                        Timestamp = DateTime.UtcNow,
                        ServiceCode = "self",
                        MessageType = "Info",
                        Message = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 100)
                            .Select(s => s[random.Next(s.Length)]).ToArray())
                    };
                    dbContext.LogEntries.Add(logEntry);
                    dbContext.SaveChanges();

                    // Вывод данных о записи в консоль
                    Console.WriteLine($"Сгенерирована запись: Id={logEntry.Id}, Timestamp={logEntry.Timestamp}, " +
                                      $"ServiceCode={logEntry.ServiceCode}, MessageType={logEntry.MessageType}, Message={logEntry.Message}");

                    await Task.Delay(_sampleLogCreatePeriod, cancellationToken);
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource?.Cancel();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
        }
    }

}
