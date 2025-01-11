using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Appilcation;
using SampleOnlineMall.Core.Managers;
using SampleOnlineMall.Core.Mappers;
using SampleOnlineMall.Core.Models;
using SampleOnlineMall.DataAccess;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.DataAccess.DataAccess;
using SampleOnlineMall.Service;
using SampleOnlineMall.Service.WebLogging;
using SampleOnlineMall.WebLogger.Services;
using Serilog;
using System.Runtime;
using WebLoggerSettings = SampleOnlineMall.Core.WebLoggerSettings;


namespace SampleOnlineMall
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            var configuration = new ConfigurationBuilder().Build();
            builder.Configuration.AddConfiguration(configuration);
            builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            #region бизнес-логика

            // App class
            var genApp = new GenericAppSettings();
            genApp.IsDocker = Environment.GetEnvironmentVariable("IS_DOCKER") == "true";
            builder.Services.AddSingleton(typeof(SampleOnlineMallAssortmentApiApp), (x) => genApp);


            // Добавляем в DI секцию WebLoggerLocalSettings из appsettings.json
            builder.Services.Configure<WebLoggerLocalSettings>(
                builder.Configuration.GetSection("WebLoggerLocalSettings"));

            // Для прямого использования через конструктор добавляем AddSingleton
            builder.Services.AddSingleton(resolver =>
                resolver.GetRequiredService<IOptions<WebLoggerLocalSettings>>().Value);

            
            builder.Services.AddScoped<IWebLoggerService>();
            
            Console.WriteLine($"Configuring DI");


            
            // используем логгер
            var serviceProvider = builder.Services.BuildServiceProvider();
            var wLogger = serviceProvider.GetRequiredService<IWebLoggerService>();
            wLogger.Information("Приложение запущено");



            foreach (var key in Environment.GetEnvironmentVariables().Keys)
            {
                wLogger.Information($"{key}={Environment.GetEnvironmentVariable(key.ToString())}");
            }

            // Add services to the container

            builder.Services.AddSingleton(typeof(Microsoft.Extensions.Configuration.ConfigurationManager), (x) => builder.Configuration);
            builder.Services.AddScoped(typeof(DbContext), typeof(EfPostgresDbContext));
            builder.Services.AddScoped(typeof(CommodityItemManager));
            builder.Services.AddScoped(typeof(CommodityItemFrontendManager));
            builder.Services.AddScoped(typeof(SupplierManager));
            builder.Services.AddScoped(typeof(IAsyncRepository<CommodityItem>), typeof(EfAsyncRepository<CommodityItem>));
            builder.Services.AddScoped(typeof(IAsyncRepository<Supplier>), typeof(EfAsyncRepository<Supplier>));
            builder.Services.AddScoped<CustomMapper>();

            #endregion

            builder.Services.AddCors(confg =>
                confg.AddPolicy("AllowAll",
                p => p.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()));

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();





            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseSwagger();

            app.UseSwaggerUI();

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.UseCors("AllowAll");

            app.Run();
        }
    }
}