using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Mappers;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.DataAccess.DataAccess;
using SampleOnlineMall.FrontEnd.BlazorServer.Data;
using SampleOnlineMall.Service;
using SampleOnlineMall.Service.WebLogging;
using SampleOnlineMall.WebLogger.Services;
using Serilog;
using Serilog.Events;


namespace SampleOnlineMall.FrontEnd.BlazorServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();

            #region custom calls

            var _app = new SampleOnlineMallFrontEndBlazorApp();
            string logFilePath = System.IO.Path.Combine(_app.LogsDirectory, Functions.GetNextFreeFileName(_app.LogsDirectory, "SampleMallBlazorFrontend", "txt"));

            Serilog.ILogger _logger = new LoggerConfiguration()
                       .MinimumLevel.Override("Microsoft", LogEventLevel.Debug)
                       .Enrich.FromLogContext()
                       //.WriteTo.BrowserConsole()
                       .WriteTo.File(logFilePath)
                       .CreateLogger();

            // веб-логгер
            var _webLoggerSettings = new WebLoggerLocalSettings();
            _webLoggerSettings.ServiceCode = "FRNT";
            _webLoggerSettings.HostAlias = Environment.GetEnvironmentVariable("IS_DOCKER") == "true"
                ? builder.Configuration.GetConnectionString("web_logger-container")
                : builder.Configuration.GetConnectionString("localhost");
            builder.Services.AddSingleton(typeof(WebLoggerLocalSettings), (x) => _webLoggerSettings);
            builder.Services.AddScoped<IWebLoggerService>(provider => new WebLoggerService(_webLoggerSettings));

            var serviceProvider = builder.Services.BuildServiceProvider();
            var wLogger = serviceProvider.GetRequiredService<IWebLoggerService>();
            wLogger.Information("Приложение запущено");

            builder.Services.AddSingleton(typeof(Serilog.ILogger), (x) => _logger);

            var webRepoOptions = new WebApiAsyncRepositoryOptions()
                .SetLogger(_logger)
                .SetBaseAddress("https://mallassortapi01.t109.tech/")
                .SetGetAllHostPath("getall/")
                .SetGetByIdOrNullHostPath("GetByIdOrNull/")
                .SetGetAllByRequestHostPath("getallbyrequest/")
                .SetSearchHostPath("search/");

            builder.Services.AddSingleton(typeof(WebApiAsyncRepositoryOptions), (x) => webRepoOptions);
            builder.Services.AddScoped(typeof(IAsyncRepository<CommodityItemFrontend>), typeof(WebApiAsyncRepositoryOptions));
            builder.Services.AddScoped(typeof(SampleOnlineMallFrontEndBlazorApp), typeof(SampleOnlineMallFrontEndBlazorApp));

            FrontEndSettings frontEndSettings = new FrontEndSettings();
            frontEndSettings.DisplayTopHorizontalMenu = false;
            frontEndSettings.DisplayMainHorizontalMenu = false;
            frontEndSettings.DisplayNavBar = false;
            builder.Services.AddScoped(typeof(FrontEndSettings), (x) => frontEndSettings);
            builder.Services.AddScoped(typeof(StoreManager), typeof(StoreManager));
            builder.Services.AddScoped(typeof(CustomMapper), typeof(CustomMapper));
            builder.Services.AddScoped(typeof(ComponentHub), typeof(ComponentHub));

            #endregion 

            var app = builder.Build();
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });

            app.Run();
        }
    }
}