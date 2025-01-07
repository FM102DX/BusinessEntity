using Microsoft.EntityFrameworkCore;
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


            builder.Services.AddScoped<IWebLoggerService>(provider => new WebLoggerService("ASSORT"));

            var serviceProvider = builder.Services.BuildServiceProvider();
            var wLogger = serviceProvider.GetRequiredService<IWebLoggerService>();

            // Используем логгер
            wLogger.Information("Приложение запущено.");


            #region бизнес-логика

            var _app = new SampleOnlineMallAssortmentApiApp();
            _app.IsDocker = Environment.GetEnvironmentVariable("IS_DOCKER") == "true";
            
            string logFilePath = System.IO.Path.Combine(_app.LogsDirectory, Functions.GetNextFreeFileName(_app.LogsDirectory, "AssortmentApiLogs", "txt"));

            Serilog.ILogger _logger = new LoggerConfiguration()
                  .WriteTo.File(logFilePath)
                  .MinimumLevel.Debug()
                  .CreateLogger();

            _logger.Information("P1");
            _logger.Information($"_app.IsDocker={Environment.GetEnvironmentVariable("IS_DOCKER")}");

            foreach (var key in Environment.GetEnvironmentVariables().Keys)
            {
                _logger.Information($"{key}={Environment.GetEnvironmentVariable(key.ToString())}");
            }


            // Add services to the container
            builder.Services.AddSingleton(typeof(SampleOnlineMallAssortmentApiApp), (x) => _app);//само приложение
            builder.Services.AddSingleton(typeof(Microsoft.Extensions.Configuration.ConfigurationManager), (x) => builder.Configuration);
            builder.Services.AddScoped(typeof(DbContext), typeof(EfPostgresDbContext));
            builder.Services.AddScoped(typeof(CommodityItemManager));
            builder.Services.AddScoped(typeof(CommodityItemFrontendManager));
            builder.Services.AddScoped(typeof(SupplierManager));
            builder.Services.AddSingleton(typeof(Serilog.ILogger), (x) => _logger);
            builder.Services.AddScoped(typeof(IAsyncRepository<CommodityItem>), typeof(EfAsyncRepository<CommodityItem>));
            builder.Services.AddScoped(typeof(IAsyncRepository<Supplier>), typeof(EfAsyncRepository<Supplier>));

            builder.Services.AddScoped<CustomMapper>();
            //builder.Services.AddScoped(typeof(WebLoggerManager), (x) => new WebLoggerManager("assortment", loggerOptions));



            #endregion

            builder.Services.AddCors(confg =>
                confg.AddPolicy("AllowAll",
                p => p.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()));

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            _logger.Information("P2");


            //var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
            //if (!string.IsNullOrEmpty(urls))
            //{
            //    builder.WebHost.UseUrls(urls);
            //}

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(5000); // HTTP на всех интерфейсах
                //нам не нужен сертификат внутри контейнера, это делает шлюз
                //options.ListenAnyIP(443, listenOptions =>
                //{
                //    listenOptions.UseHttps(); // HTTPS на всех интерфейсах
                //});
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseSwagger();
            app.UseSwaggerUI();


            if (app.Environment.IsDevelopment())
            {
            }

            _logger.Information("P3");

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.UseCors("AllowAll");

            _logger.Information("P4");

            app.Run();

            _logger.Information("P5");
        }
    }
}