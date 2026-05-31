using System;
using System.Collections;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BusinessEntity.Service;
using BusinessEntity.Service.WebLogging;
using Microsoft.Extensions.Hosting;

namespace BusinessEntity.WebLogger.Services
{
    public interface IWebLoggerService
    {
        Task Information(string text);
        Task Warning(string text);
        Task Error(string text);
        Task Error(Exception ex);
        Task Debug(string text);
        void SetActiveStatus(bool newStatus);
        Task SendObject(object data);
    }

    public class WebLoggerService : BackgroundService, IWebLoggerService
    {
        // Один sender-поток последовательно вычитывает очередь и не создает шторма HTTP-запросов.
        private static readonly BoundedChannelOptions ChannelOptions = new(512)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        };

        // Таймаут и backoff делаем мягче, чем раньше, чтобы не спамить логгер при старте приложения.
        private static readonly TimeSpan LogRequestTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan RetryDelayAfterFailure = TimeSpan.FromSeconds(2);
        private const int FailureThresholdBeforeSuspension = 5;

        private readonly WebLoggerLocalSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly Channel<LogEntryTransferDto> _channel;
        private readonly string _loggerUrl;
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        private int _isActive = 1;
        private int _consecutiveFailures;

        public WebLoggerService(
            WebLoggerLocalSettings settings,
            GenericAppSettings genAppSettings,
            IHttpClientFactory httpClientFactory)
        {
            _settings = settings;

            var host = genAppSettings.IsDocker
                ? _settings.HostAliasWhenDocker
                : _settings.HostAliasWhenIISExpress;

            _loggerUrl = $"http://{host}/api/WebLogger/CreateLogRecord";
            _httpClient = httpClientFactory.CreateClient("WebLogger");
            _httpClient.Timeout = LogRequestTimeout;
            _channel = Channel.CreateBounded<LogEntryTransferDto>(ChannelOptions);
        }

        public void SetActiveStatus(bool newStatus)
        {
            Interlocked.Exchange(ref _isActive, newStatus ? 1 : 0);
        }

        public async Task Debug(string text)
        {
            await EnqueueAsync("Debug", text);
        }

        public async Task Information(string text)
        {
            await EnqueueAsync("Info", text);
        }

        public async Task Warning(string text)
        {
            await EnqueueAsync("Warning", text);
        }

        public async Task Error(string text)
        {
            await EnqueueAsync("Error", text);
        }

        public async Task Error(Exception ex)
        {
            await EnqueueAsync("Error", FormatException(ex));
        }

        public async Task SendObject(object data)
        {
            var serializedData = JsonSerializer.Serialize(data, _jsonOptions);
            await EnqueueAsync("OBJECT", serializedData);
        }

        // Фоновая отправка стабилизирует поведение и не заставляет прикладной код ждать сеть.
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var logEntry = await _channel.Reader.ReadAsync(stoppingToken);
                    await SendLogUntilSuccessAsync(logEntry, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WebLoggerService] Sender loop failure: {ex.Message}");
                    await Task.Delay(RetryDelayAfterFailure, stoppingToken);
                }
            }
        }

        // Помещает запись в bounded-очередь, чтобы всплески логов не рвали сеть и не плодили HttpClient.PostAsync.
        private async ValueTask EnqueueAsync(string messageType, string message)
        {
            if (Interlocked.CompareExchange(ref _isActive, 1, 1) == 0)
            {
                return;
            }

            var logEntry = new LogEntryTransferDto
            {
                Timestamp = DateTime.UtcNow,
                ServiceCode = _settings.ServiceCode,
                MessageType = messageType,
                Message = message
            };

            await _channel.Writer.WriteAsync(logEntry);
        }

        // Держит текущую запись в голове очереди и ретраит именно ее до успешной отправки.
        private async Task SendLogUntilSuccessAsync(LogEntryTransferDto logEntry, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var sendResult = await TrySendLogOnceAsync(logEntry, cancellationToken);
                if (sendResult.IsSuccess)
                {
                    Interlocked.Exchange(ref _consecutiveFailures, 0);
                    return;
                }

                RegisterFailure(sendResult.ErrorText);
                await Task.Delay(RetryDelayAfterFailure, cancellationToken);
            }
        }

        // Выполняет одну попытку отправки без смены текущего элемента очереди.
        private async Task<SendAttemptResult> TrySendLogOnceAsync(LogEntryTransferDto logEntry, CancellationToken cancellationToken)
        {
            try
            {
                using var content = new StringContent(
                    JsonSerializer.Serialize(logEntry),
                    Encoding.UTF8,
                    "application/json");

                using var response = await _httpClient.PostAsync(_loggerUrl, content, cancellationToken);
                response.EnsureSuccessStatusCode();
                return SendAttemptResult.Success();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return SendAttemptResult.Failure("Request timeout");
            }
            catch (Exception ex)
            {
                return SendAttemptResult.Failure(ex.Message);
            }
        }

        // После серии ошибок продолжаем ретраить текущую запись, но логируем накопившийся счетчик сбоев.
        private void RegisterFailure(string errorText)
        {
            var failures = Interlocked.Increment(ref _consecutiveFailures);
            if (failures >= FailureThresholdBeforeSuspension)
            {
                Console.WriteLine($"[WebLoggerService] Current log entry is still pending after {failures} failures. Will keep retrying to {_loggerUrl}. Last error: {errorText}");
                Interlocked.Exchange(ref _consecutiveFailures, 0);
                return;
            }

            Console.WriteLine($"[WebLoggerService] Log delivery failed ({failures}/{FailureThresholdBeforeSuspension}) to {_loggerUrl}: {errorText}");
        }

        // Хранит результат одной HTTP-попытки отправки.
        private readonly record struct SendAttemptResult(bool IsSuccess, string ErrorText)
        {
            public static SendAttemptResult Success()
            {
                return new SendAttemptResult(true, string.Empty);
            }

            public static SendAttemptResult Failure(string errorText)
            {
                return new SendAttemptResult(false, errorText);
            }
        }

        // Формирует подробный текст исключения с цепочкой inner exceptions.
        private static string FormatException(Exception ex, string? context = null)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(context))
            {
                sb.AppendLine($"[Exception] Context: {context}");
            }

            int level = 0;
            var current = ex;
            while (current != null)
            {
                var prefix = level == 0 ? "EX" : $"INNER[{level}]";
                sb.AppendLine($"{prefix}: {current.GetType().FullName} HResult=0x{current.HResult:X8}");
                sb.AppendLine($"Message: {current.Message}");

                if (!string.IsNullOrWhiteSpace(current.Source))
                {
                    sb.AppendLine($"Source: {current.Source}");
                }

                if (current.TargetSite != null)
                {
                    sb.AppendLine($"TargetSite: {current.TargetSite}");
                }

                if (current.Data != null && current.Data.Count > 0)
                {
                    sb.AppendLine("Data:");
                    foreach (DictionaryEntry entry in current.Data)
                    {
                        sb.AppendLine($"  {entry.Key} = {entry.Value}");
                    }
                }

                sb.AppendLine("StackTrace:");
                sb.AppendLine(current.StackTrace);
                sb.AppendLine("----");
                current = current.InnerException;
                level++;
            }

            return sb.ToString();
        }
    }
}
