using BlazorServerWebLogger.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SampleOnlineMall.WebLogger.DataAccess;
using SampleOnlineMall.WebLogger.Models;

public class SampleLogGeneratorService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly int _sampleLogCreatePeriod;
    private CancellationTokenSource _cancellationTokenSource;

    public SampleLogGeneratorService(IServiceProvider serviceProvider, IOptions<SampleLogSettings> settings)
    {
        _serviceProvider = serviceProvider;
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
        var random = new Random();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<WebLoggerDbContext>();

                    var logEntry = new LogEntryDbStorable
                    {
                        Id = Guid.NewGuid(),
                        Timestamp = DateTime.UtcNow,
                        ServiceCode = "self",
                        MessageType = "Info",
                        Message = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 100)
                            .Select(s => s[random.Next(s.Length)]).ToArray())
                    };

                    await context.LogEntries.AddAsync(logEntry, cancellationToken);
                    await context.SaveChangesAsync(cancellationToken);

                    // Вывод данных о записи в консоль
                    Console.WriteLine($"Сгенерирована запись: Id={logEntry.Id}, Timestamp={logEntry.Timestamp}, " +
                                      $"ServiceCode={logEntry.ServiceCode}, MessageType={logEntry.MessageType}, Message={logEntry.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during sample log generation: {ex.Message}");
            }

            await Task.Delay(_sampleLogCreatePeriod, cancellationToken);
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
