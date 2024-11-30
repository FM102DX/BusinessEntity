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

            var _app = new SampleOnlineMallFrontEndBlazorApp();
            string logFilePath = System.IO.Path.Combine(_app.LogsDirectory, Functions.GetNextFreeFileName(_app.LogsDirectory, "SampleMallBlazorFrontend", "txt"));

            Serilog.ILogger _logger = new LoggerConfiguration()
                       .MinimumLevel.Override("Microsoft", LogEventLevel.Debug)
                       .Enrich.FromLogContext()
                       .WriteTo.BrowserConsole()
                       .WriteTo.File(logFilePath)
                       .CreateLogger();


            var webLoggerOptions = new WebApiAsyncRepositoryOptions()
                        .SetLogger(_logger)
                        .SetBaseAddress("https://weblogger.t109.tech")
                        .SetInsertHostPath("insertitem/");

            var webLogger = new WebLoggerManager("blazorfrontend", webLoggerOptions);
            webLogger.Log("Weblogger p1");
            _logger.Information("Blazor P1");
            
            _logger.Information("Blazor P2");

            builder.Services.AddSingleton(typeof(Serilog.ILogger), (x) => _logger);

            var webRepoOptions = new WebApiAsyncRepositoryOptions()
                .SetLogger(_logger)
                .SetBaseAddress("https://mallassortapi01.t109.tech/")
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
    }
}