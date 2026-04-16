using System;
using System.Collections;
using System.Net.Http;
using System.Runtime;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using BusinessEntity.Service;
using BusinessEntity.Service.WebLogging;

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

    public class WebLoggerService : IWebLoggerService
    {
        private bool _isActive { get; set; } = true;
        private string _loggerUrl;
        private WebLoggerLocalSettings _settings;
        private GenericAppSettings _genAppSettings;
        private string _host;
        private readonly HttpClient _httpClient;
        private int _consecutiveFailures;
        private DateTime _suspendedUntilUtc = DateTime.MinValue;

        private static readonly TimeSpan LogRequestTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan SuspensionDuration = TimeSpan.FromMinutes(1);
        private const int FailureThresholdBeforeSuspension = 5;

        public WebLoggerService(WebLoggerLocalSettings settings, GenericAppSettings genAppSettings)
        {
           // Console.WriteLine($"Constructing class WebLoggerService");
            _settings = settings;
            _genAppSettings = genAppSettings;

            _host = _genAppSettings.IsDocker ? _settings.HostAliasWhenDocker : _settings.HostAliasWhenIISExpress;
            //Console.WriteLine($"ServiceCode={_settings.ServiceCode}");
            _loggerUrl = $"http://{_host}/api/WebLogger/CreateLogRecord";
            _httpClient = new HttpClient
            {
                Timeout = LogRequestTimeout
            };
            //Console.WriteLine($"LoggerUrl={_loggerUrl}");
        }

        public void SetActiveStatus(bool newStatus)
        {
            _isActive = newStatus;
        }
        public Task Debug(string text)
        {
            _ = SendLogAsync("Debug", text);
            return Task.CompletedTask;
        }

        public Task Information(string text)
        {
            _ = SendLogAsync("Info", text);
            return Task.CompletedTask;
        }

        public Task Warning(string text)
        {
            _ = SendLogAsync("Warning", text);
            return Task.CompletedTask;
        }
        public Task Error(string text)
        {
            _ = SendLogAsync("Error", text);
            return Task.CompletedTask;
        }
        public Task Error(Exception ex)
        {
            var details = FormatException(ex);
            _ = SendLogAsync("Error", details);
            return Task.CompletedTask;
        }
        public Task SendObject(object data)
        {
            // Сериализация объекта в JSON
            string serializedData = JsonConvert.SerializeObject(data, Formatting.Indented);

            _ = SendLogAsync("OBJECT", serializedData);
            return Task.CompletedTask;
        }

        private async Task SendLogAsync(string messageType, string message)
        {
            if (!_isActive) return;
            if (DateTime.UtcNow < _suspendedUntilUtc) return;
            try
            {
                var logEntry = new LogEntryTransferDto
                {
                    Timestamp = DateTime.UtcNow,
                    ServiceCode = _settings.ServiceCode,
                    MessageType = messageType,
                    Message = message
                };
                //Console.WriteLine($"LGR_P1 -- sending message {message}");
                //Console.WriteLine($"LGR_P2");
                var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(logEntry), Encoding.UTF8, "application/json");
                //Console.WriteLine($"LGR_P3 url={_loggerUrl} content={content} base={httpClient.BaseAddress}");
                var response = await _httpClient.PostAsync(_loggerUrl, content);
                //Console.WriteLine($"LGR_P4 {response.IsSuccessStatusCode} {response.ToString()}");
                if (response.IsSuccessStatusCode)
                {
                    Interlocked.Exchange(ref _consecutiveFailures, 0);
                }
            }
            catch (Exception ex)
            {
                var failures = Interlocked.Increment(ref _consecutiveFailures);
                if (failures >= FailureThresholdBeforeSuspension)
                {
                    _suspendedUntilUtc = DateTime.UtcNow.Add(SuspensionDuration);
                    Console.WriteLine($"[WebLoggerService] Suspended outgoing log delivery for {SuspensionDuration.TotalSeconds} seconds after {failures} failures. Last error: {ex.Message}");
                    Interlocked.Exchange(ref _consecutiveFailures, 0);
                    return;
                }

                Console.WriteLine($"[WebLoggerService] Log delivery failed ({failures}/{FailureThresholdBeforeSuspension}) to {_loggerUrl}: {ex.Message}");
            }
        }

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
                if (!string.IsNullOrWhiteSpace(current.Source)) sb.AppendLine($"Source: {current.Source}");
                if (current.TargetSite != null) sb.AppendLine($"TargetSite: {current.TargetSite}");
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


