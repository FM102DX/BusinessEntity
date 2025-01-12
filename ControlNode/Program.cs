using ControlNode.Data;
using ControlNode.Data.AssortmentLoader;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SampleOnlineMall.Core;
using SampleOnlineMall.DataAccess.DataAccess;
using SampleOnlineMall.Service;
using SampleOnlineMall.Service.WebLogging;
using SampleOnlineMall.WebLogger.Services;

namespace ControlNode
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();

            //логгирование в веб-логгер

            // App classes
            var genApp = new GenericAppSettings();
            genApp.IsDocker = Environment.GetEnvironmentVariable("IS_DOCKER") == "true";
            builder.Services.AddSingleton(typeof(GenericAppSettings), (x) => genApp);

            // Регистрация WebLoggerLocalSettings в DI
            var webLoggerSettings = new WebLoggerLocalSettings();
            builder.Configuration.GetSection("WebLoggerLocalSettings").Bind(webLoggerSettings);
            builder.Services.AddSingleton(webLoggerSettings);
            builder.Services.AddScoped<IWebLoggerService, WebLoggerService>();

            // используем логгер
            var serviceProvider = builder.Services.BuildServiceProvider();
            var wLogger = serviceProvider.GetRequiredService<IWebLoggerService>();
            wLogger.Information("App launched");

            builder.Services.AddScoped<WebApiAsyncRepository<CommodityItemApiFeed>>(serviceProvider =>
            {
                var baseAddress = genApp.IsDocker ? "http://assort-api-container:80/" : "http://localhost:5010/";
                var options = new WebApiAsyncRepositoryOptions()
                    .SetBaseAddress(baseAddress)
                    .SetGetAllHostPath("getall")
                    .SetGetAllByRequestHostPath("getall")
                    .SetInsertHostPath("insertitem")
                    .SetDeleteAllHostPath("deleteallitems");
                return new WebApiAsyncRepository<CommodityItemApiFeed>(options, wLogger);
            });
            builder.Services.AddScoped<AssortmentLoader>();
            builder.Services.Configure<AppSettings>(builder.Configuration);

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts
                app.UseHsts();
            }
            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseRouting();

            app.MapBlazorHub();

            app.MapFallbackToPage("/_Host");

            app.Run();
        }
    }
}