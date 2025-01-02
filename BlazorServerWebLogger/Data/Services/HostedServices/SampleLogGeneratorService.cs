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
        private readonly Random _random;
        private IAsyncRepository<LogEntryDbStorable> _logRepository;

        public SampleLogGeneratorService(
            IRepositoryFactory<LogEntryDbStorable> repositoryFactory,
            IOptions<SampleLogSettings> settings)
        {
            _repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
            _sampleLogCreatePeriod = settings.Value.SampleLogCreatePeriod;
            _random = new Random();
            _logRepository = _repositoryFactory.GetRepository();
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
                    var logRepository = _logRepository;
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var message = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 100)
                            .Select(s => s[_random.Next(s.Length)]).ToArray());

                        var messageTypes = new string[] { "Info", "Error", "Warning" };
                        var serviceCodeIndex = _random.Next(1, 5); // Генерируем число от 1 до 4
                        var messageTypeIndex = _random.Next(1, 4); // Генерируем число от 1 до 3
                        var serviceCode = $"SELF_{serviceCodeIndex}";
                        var messageType = messageTypes[messageTypeIndex-1];

                        var logEntry = new LogEntryDbStorable
                        {
                            Id = Guid.NewGuid(),
                            ServiceCode = serviceCode,
                            MessageType = messageType ?? "default",
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
