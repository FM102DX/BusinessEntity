using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Managers;
using SampleOnlineMall.Core.Mappers;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.DataAccess.DataAccess;
using SampleOnlineMall.FrontEnd.Blazor;
using SampleOnlineMall.FrontEnd.Blazor.Data;
using SampleOnlineMall.Service;
using Serilog;
using Serilog.Events;

namespace SampleOnlineMall.FrontEnd.Blazor
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            // Load configuration from appsettings.json
            using var http = new HttpClient() { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
            var configurationJson = await http.GetStringAsync("appsettings.json");
            var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(configurationJson);

            var _app = new SampleOnlineMallFrontEndBlazorApp();
            string logFilePath = System.IO.Path.Combine(_app.LogsDirectory, Functions.GetNextFreeFileName(_app.LogsDirectory, "SampleMallBlazorFrontend", "txt"));

            Serilog.ILogger _logger = new LoggerConfiguration()
                       .MinimumLevel.Override("Microsoft", LogEventLevel.Debug)
                       .Enrich.FromLogContext()
                       .WriteTo.BrowserConsole()
                       .WriteTo.File(logFilePath)
                       .CreateLogger();


            // Get configuration values
            var webLoggerBaseAddress = GetConfigValue(config, "WebLogger:BaseAddress", "https://weblogger.t109.tech");
            var assortmentApiBaseAddress = GetConfigValue(config, "AssortmentApi:BaseAddress", "https://mallassortapi01.t109.tech/");

            var webLoggerOptions = new WebApiAsyncRepositoryOptions()
                        .SetLogger(_logger)
                        .SetBaseAddress(webLoggerBaseAddress)
                        .SetInsertHostPath("insertitem/");

            var webLogger = new WebLoggerManager("blazorfrontend", webLoggerOptions);
            webLogger.Log("Weblogger p1");
            _logger.Information("Blazor P1");
            
            _logger.Information("Blazor P2");

            builder.Services.AddSingleton(typeof(Serilog.ILogger), (x) => _logger);

            var webRepoOptions = new WebApiAsyncRepositoryOptions()
                .SetLogger(_logger)
                .SetBaseAddress(assortmentApiBaseAddress)
                .SetGetAllHostPath("getall/")
                .SetGetByIdOrNullHostPath("GetByIdOrNull/")
                .SetGetAllByRequestHostPath("getallbyrequest/")
                .SetSearchHostPath("search/");

            builder.Services.AddScoped(typeof(IAsyncRepository<CommodityItemFrontend>), (x) => new WebApiAsyncRepository<CommodityItemFrontend>(webRepoOptions));
            builder.Services.AddScoped(typeof(SampleOnlineMallFrontEndBlazorApp), typeof(SampleOnlineMallFrontEndBlazorApp));

            FrontEndSettings frontEndSettings = new FrontEndSettings();
            frontEndSettings.DisplayTopHorizontalMenu = false;
            frontEndSettings.DisplayMainHorizontalMenu = false;
            frontEndSettings.DisplayNavBar = false;
            builder.Services.AddScoped(typeof(FrontEndSettings), (x) => frontEndSettings);
            builder.Services.AddScoped(typeof(StoreManager), typeof(StoreManager));
            builder.Services.AddScoped(typeof(Mapper), typeof(Mapper));
            builder.Services.AddScoped(typeof(ComponentHub), typeof(ComponentHub));
            _logger.Information("Blazor P3");

            var host = builder.Build();
            
            await host.RunAsync();
        }

        private static string GetConfigValue(Dictionary<string, object> config, string key, string defaultValue)
        {
            try
            {
                var keys = key.Split(':');
                object current = config;
                
                foreach (var k in keys)
                {
                    if (current is System.Text.Json.JsonElement element)
                    {
                        if (element.TryGetProperty(k, out var prop))
                        {
                            current = prop;
                        }
                        else
                        {
                            return defaultValue;
                        }
                    }
                    else if (current is Dictionary<string, object> dict)
                    {
                        if (dict.TryGetValue(k, out var value))
                        {
                            current = value;
                        }
                        else
                        {
                            return defaultValue;
                        }
                    }
                    else
                    {
                        return defaultValue;
                    }
                }
                
                if (current is System.Text.Json.JsonElement jsonElement)
                {
                    return jsonElement.GetString() ?? defaultValue;
                }
                
                return current?.ToString() ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}