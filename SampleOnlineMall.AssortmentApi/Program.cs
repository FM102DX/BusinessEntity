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
using Serilog;


namespace SampleOnlineMall
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(6000));

            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            var configuration = new ConfigurationBuilder().Build();

            builder.Configuration.AddConfiguration(configuration);

            var _app = new SampleOnlineMallAssortmentApiApp();

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

            var loggerOptions = new WebApiAsyncRepositoryOptions()
                        .SetLogger(_logger)
                        .SetBaseAddress("https://weblogger.t109.tech")
                        .SetInsertHostPath("insertitem/");

            // Add services to the container
            builder.Services.AddSingleton(typeof(Microsoft.Extensions.Configuration.ConfigurationManager), (x) => builder.Configuration);
            builder.Services.AddScoped(typeof(DbContext), typeof(EfPostgresDbContext));
            builder.Services.AddSingleton(typeof(SampleOnlineMallAssortmentApiApp), (x) => _app);
            builder.Services.AddScoped(typeof(CommodityItemManager));
            builder.Services.AddScoped(typeof(CommodityItemFrontendManager));
            builder.Services.AddScoped(typeof(SupplierManager));
            builder.Services.AddSingleton(typeof(Serilog.ILogger), (x) => _logger);
            builder.Services.AddScoped(typeof(IAsyncRepository<CommodityItem>), typeof(EfAsyncRepository<CommodityItem>));
            builder.Services.AddScoped(typeof(IAsyncRepository<Supplier>), typeof(EfAsyncRepository<Supplier>));

            builder.Services.AddScoped<CustomMapper>();
            builder.Services.AddScoped(typeof(WebLoggerManager), (x) => new WebLoggerManager("assortment", loggerOptions));

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