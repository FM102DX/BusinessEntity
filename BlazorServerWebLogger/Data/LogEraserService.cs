using BlazorServerWebLogger.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SampleOnlineMall.WebLogger.DataAccess;

public class LogEraserService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly int _erasePeriod;
    private readonly int _logsTargetCount;
    private CancellationTokenSource _cancellationTokenSource;

    public LogEraserService(IServiceProvider serviceProvider, IOptions<LogEraserSettings> settings)
    {
        _serviceProvider = serviceProvider;
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
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<WebLoggerDbContext>();

                    var totalLogs = await context.LogEntries.CountAsync(cancellationToken);

                    if (totalLogs > _logsTargetCount)
                    {
                        var logsToRemove = totalLogs - _logsTargetCount;

                        var oldestLogs = await context.LogEntries
                            .OrderBy(log => log.Timestamp)
                            .Take(logsToRemove)
                            .ToListAsync(cancellationToken);

                        context.LogEntries.RemoveRange(oldestLogs);
                        await context.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during log erasure: {ex.Message}");
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
