using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SampleOnlineMall.Service.WebLogging;


namespace SampleOnlineMall.WebLogger.Services
{
    public interface IWebLoggerService
    {
        Task Information(string text);
        Task Warning(string text);
        Task Error(string text);
        void SetActiveStatus(bool newStatus);
    }

    public class WebLoggerService : IWebLoggerService
    {
        private bool _isActive { get; set; } = true;
        private readonly string _serviceCode;
        private const string LoggerUrl = "http://localhost:5080/api/WebLogger/CreateLogRecord";
        public WebLoggerService(string serviceCode)
        {
            _serviceCode = serviceCode ?? throw new ArgumentNullException(nameof(serviceCode));
        }

        public void SetActiveStatus(bool newStatus)
        {
            _isActive = newStatus;
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

        private async Task SendLogAsync(string messageType, string message)
        {
            var logEntry = new LogEntryTransferDto
            {
                Timestamp = DateTime.UtcNow,
                ServiceCode = _serviceCode,
                MessageType = messageType,
                Message = message
            };

            using var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5080") };
            var content = new StringContent(JsonSerializer.Serialize(logEntry), Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(LoggerUrl, content);
            response.EnsureSuccessStatusCode();
        }
    }
}