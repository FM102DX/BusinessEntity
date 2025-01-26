using System;
using System.Net.Http;
using System.Runtime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SampleOnlineMall.Service;
using SampleOnlineMall.Service.WebLogging;


namespace SampleOnlineMall.WebLogger.Services
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
        public WebLoggerService(WebLoggerLocalSettings settings, GenericAppSettings genAppSettings)
        {
           // Console.WriteLine($"Constructing class WebLoggerService");
            _settings = settings;
            _genAppSettings = genAppSettings;

            _host = _genAppSettings.IsDocker ? _settings.HostAliasWhenDocker : _settings.HostAliasWhenIISExpress;
            //Console.WriteLine($"ServiceCode={_settings.ServiceCode}");
            _loggerUrl = $"http://{_host}/api/WebLogger/CreateLogRecord";
            //Console.WriteLine($"LoggerUrl={_loggerUrl}");
        }

        public void SetActiveStatus(bool newStatus)
        {
            _isActive = newStatus;
        }
        public async Task Debug(string text)
        {
            await SendLogAsync("Debug", text);
        }

        public async Task Information(string text)
        {
            await SendLogAsync("Info", text);
        }

        public async Task Warning(string text)
        {
            await SendLogAsync("Warning", text);
        }
        public async Task Error(string text)
        {
            await SendLogAsync("Error", text);
        }
        public async Task Error(Exception ex)
        {
            await SendLogAsync("Error", $"Error occured messgae={ex.Message} inner exception={ex.InnerException}");
        }
        public async Task SendObject(object data)
        {
            // Сериализация объекта в JSON
            string serializedData = JsonConvert.SerializeObject(data, Formatting.Indented);

            // Логирование через SendLogAsync
            await SendLogAsync("OBJECT", serializedData);
        }

        private async Task SendLogAsync(string messageType, string message)
        {
            if (!_isActive) return;
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
                using var httpClient = new HttpClient();
                //Console.WriteLine($"LGR_P2");
                var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(logEntry), Encoding.UTF8, "application/json");
                //Console.WriteLine($"LGR_P3 url={_loggerUrl} content={content} base={httpClient.BaseAddress}");
                var response = await httpClient.PostAsync(_loggerUrl, content);
                //Console.WriteLine($"LGR_P4");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: msg={ex.Message} inn={ex.InnerException?.Message}");
            }
        }

    }
}