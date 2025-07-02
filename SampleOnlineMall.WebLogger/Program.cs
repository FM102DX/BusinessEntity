using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Appilcation;
using SampleOnlineMall.Core.Managers;
using SampleOnlineMall.DataAccess;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.DataAccess.DataAccess;
using SampleOnlineMall.Service;
using SampleOnlineMall.WebLogger.Service;
using Serilog;


namespace SampleOnlineMall.WebLogger
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            
            builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(7000));

            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            var configuration = new ConfigurationBuilder().Build();

            builder.Configuration.AddConfiguration(configuration);

            var _app = new SampleOnlineMallWebLoggerApp();

            string logFilePath = System.IO.Path.Combine(_app.LogsDirectory, Functions.GetNextFreeFileName(_app.LogsDirectory, "AssortmentApiLogs", "txt"));

            Serilog.ILogger _logger = new LoggerConfiguration()
                  .WriteTo.File(logFilePath)
                  .MinimumLevel.Debug()
                  .CreateLogger();
            
            _logger.Information("P1");
            
            //var confMgr = new ConfigurationManager();

            //confMgr.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            
            builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var cnnStr = builder.Configuration.GetConnectionString("PostgreConnection");
            // Add services to the container
            builder.Services.AddSingleton(typeof(Microsoft.Extensions.Configuration.ConfigurationManager), (x) => builder.Configuration);
            builder.Services.AddScoped(typeof(DbContext), typeof(EfPostgresWebLoggerDbContext));
            builder.Services.AddSingleton(typeof(SampleOnlineMallWebLoggerApp), (x) => _app);
            builder.Services.AddScoped(typeof(WebLoggerMessageManager), typeof(WebLoggerMessageManager));
            builder.Services.AddScoped(typeof(WebLoggerSettingsManager), typeof(WebLoggerSettingsManager));
            builder.Services.AddSingleton(typeof(Serilog.ILogger), (x) => _logger);
            builder.Services.AddScoped(typeof(IAsyncRepository<WebLoggerMessage>), typeof(EfAsyncRepository<WebLoggerMessage>));
            builder.Services.AddScoped(typeof(IAsyncRepository<WebLoggerSettings>), typeof(EfAsyncRepository<WebLoggerSettings>));

            builder.Services.AddCors(confg =>
                confg.AddPolicy("AllowAll",
                     p => p.AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader()));
            
            builder.Services.AddControllersWithViews();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            _logger.Information("P2");

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
            
            app.MigrateDatabase();

            _logger.Information("P5");

            app.Run();
            
            _logger.Information("P6");
        }
    }
}