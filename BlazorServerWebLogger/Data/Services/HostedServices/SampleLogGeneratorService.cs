using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using BlazorServerWebLogger.Contracts;
using SampleOnlineMall.WebLogger.Models;

namespace BlazorServerWebLogger.Data.Services.HostedServices
{
    public class SampleLogGeneratorService : IHostedService, IDisposable
    {
        private readonly IRepositoryFactory<LogEntryDbStorable> _repositoryFactory;
        private readonly int _sampleLogCreatePeriod;
        private CancellationTokenSource _cancellationTokenSource;

        public SampleLogGeneratorService(
            IRepositoryFactory<LogEntryDbStorable> repositoryFactory,
            IOptions<SampleLogSettings> settings)
        {
            _repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
            _sampleLogCreatePeriod = settings.Value.SampleLogCreatePeriod;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task.Run(() => GenerateLogsAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            return Task.CompletedTask;
        }

        private async Task GenerateLogsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var random = new Random();
                var logRepository = _repositoryFactory.GetRepository();

                while (!cancellationToken.IsCancellationRequested)
                {
                    var message = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 100)
                        .Select(s => s[random.Next(s.Length)]).ToArray());

                    var logEntry = new LogEntryDbStorable
                    {
                        Id = Guid.NewGuid(),
                        ServiceCode = "self",
                        MessageType = "Info",
                        Message = message,
                        Timestamp = DateTime.UtcNow
                    };

                    await logRepository.InsertAsync(logEntry);
                    await Task.Delay(_sampleLogCreatePeriod, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в GenerateLogsAsync: {ex.Message}");
                throw;
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
