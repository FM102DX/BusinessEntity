using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using BusinessEntity.Contracts;
using SampleOnlineMall.WebLogger.Models;

namespace BusinessEntity.Data.Services.HostedServices
{
    public class LogEraserService : IHostedService, IDisposable
    {
        private readonly IRepositoryFactory<LogEntryDbStorable> _repositoryFactory;
        private readonly int _erasePeriod;
        private readonly int _logsTargetCount;
        private CancellationTokenSource _cancellationTokenSource;

        public LogEraserService(
            IRepositoryFactory<LogEntryDbStorable> repositoryFactory,
            IOptions<LogEraserSettings> settings)
        {
            _repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
            _erasePeriod = settings.Value.ErasePeriod;
            _logsTargetCount = settings.Value.LogsTargetCount;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task.Run(() => EraseOldLogsAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            return Task.CompletedTask;
        }

        private async Task EraseOldLogsAsync(CancellationToken cancellationToken)
        {
            var logRepository = _repositoryFactory.GetRepository();

            while (!cancellationToken.IsCancellationRequested)
            {
                var totalLogs = await logRepository.GetCountAsync();
                if (totalLogs > _logsTargetCount)
                {
                    var logsToRemove = totalLogs - _logsTargetCount;
                    await logRepository.DeleteNOldestRecordsAsync(logsToRemove);
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
            _cancellationTokenSource?.Dispose();
        }
    }
}
