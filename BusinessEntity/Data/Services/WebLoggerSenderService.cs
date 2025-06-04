using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessEntity.Data.Services
{
    public interface IWebLoggerSenderService
    {
        Task Information(string text);
        Task Warning(string text);
        Task Error(string text);
    }


    //builder.Services.AddControllers();
    //builder.Services.AddWebLoggerService("MyServiceCode");

    public class WebLoggerSenderService : IWebLoggerSenderService
    {
        private readonly string _serviceCode;
        private readonly HttpClient _httpClient;
        private const string LoggerUrl = "http://localhost:5080/api/WebLogger/CreateLogRecord";

        public WebLoggerSenderService(string serviceCode, HttpClient httpClient)
        {
            _serviceCode = serviceCode ?? throw new ArgumentNullException(nameof(serviceCode));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
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
            var logEntry = new
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                ServiceCode = _serviceCode,
                MessageType = messageType,
                Message = message
            };

            var content = new StringContent(JsonSerializer.Serialize(logEntry), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(LoggerUrl, content);
            response.EnsureSuccessStatusCode();
        }
    }

    public static class WebLoggerServiceExtensions
    {
        public static void AddWebLoggerService(this IServiceCollection services, string serviceCode)
        {
            services.AddHttpClient<IWebLoggerSenderService, WebLoggerSenderService>(client =>
            {
                client.BaseAddress = new Uri("http://localhost:5080");
            }).ConfigureHttpClient(serviceProvider => new WebLoggerSenderService(serviceCode, new HttpClient()));
        }
    }
}