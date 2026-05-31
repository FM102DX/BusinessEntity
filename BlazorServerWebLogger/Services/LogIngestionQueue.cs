using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AutoMapper;
using BlazorServerWebLogger.Contracts;
using BusinessEntity.Service.WebLogging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BusinessEntity.WebLogger.Models;

namespace BlazorServerWebLogger.Services
{
    // Серверная очередь принимает всплески логов и отделяет HTTP-прием от записи в БД.
    public interface ILogIngestionQueue
    {
        // Ставит лог-запись в очередь без потерь при наплыве сообщений.
        ValueTask EnqueueAsync(LogEntryTransferDto logEntryDto, CancellationToken cancellationToken = default);
    }

    // Фоновый worker последовательно вычитывает очередь и надежно сохраняет записи в БД.
    public sealed class LogIngestionQueue : IHostedService, ILogIngestionQueue, IDisposable
    {
        // Используем неограниченную очередь, чтобы не терять сообщения на входе.
        private static readonly UnboundedChannelOptions ChannelOptions = new()
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };

        // Даем сервису время спокойно дренировать очередь и переживать кратковременные сбои БД.
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan ShutdownDrainTimeout = TimeSpan.FromSeconds(90);

        private readonly Channel<LogEntryTransferDto> _channel = Channel.CreateUnbounded<LogEntryTransferDto>(ChannelOptions);
        private readonly IRepositoryFactory<LogEntryDbStorable> _repositoryFactory;
        private readonly IMapper _mapper;
        private readonly ILogger<LogIngestionQueue> _logger;
        private readonly CancellationTokenSource _workerCts = new();
        private Task? _processingTask;

        // Получает зависимости для фоновой записи queued-логов.
        public LogIngestionQueue(
            IRepositoryFactory<LogEntryDbStorable> repositoryFactory,
            IMapper mapper,
            ILogger<LogIngestionQueue> logger)
        {
            _repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Принимает лог-запись в память сервера и сразу освобождает HTTP-запрос.
        public ValueTask EnqueueAsync(LogEntryTransferDto logEntryDto, CancellationToken cancellationToken = default)
        {
            if (logEntryDto == null)
            {
                throw new ArgumentNullException(nameof(logEntryDto));
            }

            var copy = Clone(logEntryDto);
            return _channel.Writer.TryWrite(copy)
                ? ValueTask.CompletedTask
                : _channel.Writer.WriteAsync(copy, cancellationToken);
        }

        // Запускает фоновую обработку очереди после старта приложения.
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _processingTask = Task.Run(() => ProcessQueueAsync(_workerCts.Token), CancellationToken.None);
            return Task.CompletedTask;
        }

        // На остановке закрывает вход, дожидается записи очереди и только потом завершает worker.
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _channel.Writer.TryComplete();
            if (_processingTask == null)
            {
                return;
            }

            using var shutdownTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            shutdownTimeoutCts.CancelAfter(ShutdownDrainTimeout);

            try
            {
                await _processingTask.WaitAsync(shutdownTimeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                _workerCts.Cancel();
                await Task.WhenAny(_processingTask, Task.Delay(Timeout.Infinite, cancellationToken));
            }
        }

        // Освобождает ресурсы worker-а при выгрузке приложения.
        public void Dispose()
        {
            _workerCts.Dispose();
        }

        // Бесконечно читает очередь и гарантирует retry, пока запись не попадет в БД или сервис не остановят.
        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            await foreach (var logEntryDto in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                await PersistWithRetryAsync(logEntryDto, cancellationToken);
            }
        }

        // Повторяет запись в БД до успеха, чтобы временные проблемы с БД не приводили к потере логов.
        private async Task PersistWithRetryAsync(LogEntryTransferDto logEntryDto, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var repository = _repositoryFactory.GetRepository();
                    var logEntryDb = _mapper.Map<LogEntryDbStorable>(logEntryDto);
                    logEntryDb.Id = Guid.NewGuid();
                    await repository.InsertAsync(logEntryDb);
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Queued log persistence failed. ServiceCode={ServiceCode}, MessageType={MessageType}",
                        logEntryDto.ServiceCode,
                        logEntryDto.MessageType);

                    await Task.Delay(RetryDelay, cancellationToken);
                }
            }
        }

        // Копирует DTO, чтобы внешние вызывающие не могли поменять запись после enqueue.
        private static LogEntryTransferDto Clone(LogEntryTransferDto source)
        {
            return new LogEntryTransferDto
            {
                Timestamp = source.Timestamp,
                ServiceCode = source.ServiceCode,
                MessageType = source.MessageType,
                Message = source.Message
            };
        }
    }
}
