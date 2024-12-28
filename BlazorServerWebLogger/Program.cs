using BlazorServerWebLogger.Data;
using BlazorServerWebLogger.Data.App;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Models;
using SampleOnlineMall.DataAccess;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.WebLogger.DataAccess;
using SampleOnlineMall.WebLogger.Models;

namespace BlazorServerWebLogger
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();
            

            var _app = new WebLoggerApp();



            // Add services to the container

            // Чтение настроек из appsettings.json

            var logEraserSettings = new LogEraserSettings();
            builder.Services.AddScoped(provider =>
            {
                builder.Configuration.GetSection("LogEraserSettings").Bind(logEraserSettings);
                return logEraserSettings;
            });
            
            var sampleLogSettings = new SampleLogSettings();
            builder.Services.AddScoped(provider =>
            {
                builder.Configuration.GetSection("SampleLogSettings").Bind(sampleLogSettings);
                return sampleLogSettings;
            });

            builder.Services.AddSingleton(typeof(WebLoggerApp), (x) => _app); // само приложение

            var connectionString = Environment.GetEnvironmentVariable("IS_DOCKER") == "true"
                ? builder.Configuration.GetConnectionString("DockerConnection")
                : builder.Configuration.GetConnectionString("IisExpressConnection");

            builder.Services.AddDbContext<WebLoggerDbContext>(options =>
                options.UseNpgsql(connectionString));
            
            builder.Services.AddScoped<LogReaderService>();
            builder.Services.AddScoped<IHostedService, SampleLogGeneratorService>();
            builder.Services.AddScoped<IHostedService, LogEraserService>();

            var app = builder.Build();



            //Создаем новый Scope для генератора логов


            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");

            app.Run();
        }
    }
}