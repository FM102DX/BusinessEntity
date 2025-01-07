using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SampleOnlineMall.Service.WebLogging
{
    public interface IWebLoggerService
    {
        Task Information(string text);
        Task Warning(string text);
        Task Error(string text);
    }

    public class WebLoggerService : IWebLoggerService
    {
        private readonly string _serviceCode;
        private readonly HttpClient _httpClient;
        private const string LoggerUrl = "/api/WebLogger/CreateLogRecord";

        public WebLoggerService(string serviceCode, HttpClient httpClient)
        {
            _serviceCode = serviceCode ?? throw new ArgumentNullException(nameof(serviceCode));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _httpClient.BaseAddress = new Uri("http://localhost:5080");
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

            var content = new StringContent(JsonSerializer.Serialize(logEntry), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(LoggerUrl, content);
            response.EnsureSuccessStatusCode();
        }
    }
}