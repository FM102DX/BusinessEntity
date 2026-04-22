using System.Diagnostics;
using BlazorServerWebLogger.Contracts;
using BlazorServerWebLogger.Data;
using BlazorServerWebLogger.Data.App;
using BlazorServerWebLogger.Data.Services;
using BlazorServerWebLogger.Data.Services.HostedServices;
using BlazorServerWebLogger.DataAccess.Repository;
using BlazorServerWebLogger.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using BusinessEntity.Core;
using AutoMapper;
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
            builder.Services.AddControllers();
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddAutoMapper(typeof(Program));
            builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(90));
            var _app = new WebLoggerApp();

            // ������ �������� �� appsettings.json
            builder.Services.Configure<LogEraserSettings>(
                builder.Configuration.GetSection("LogEraserSettings"));
            builder.Services.Configure<SampleLogSettings>(
                builder.Configuration.GetSection("SampleLogSettings"));

            builder.Services.AddSingleton(typeof(WebLoggerApp), (x) => _app); // ���� ����������
           


            var connectionString = Environment.GetEnvironmentVariable("IS_DOCKER") == "true"
                ? builder.Configuration.GetConnectionString("DockerConnection")
                : builder.Configuration.GetConnectionString("IisExpressConnection");

            Console.WriteLine($"[DB-DIAG] Environment IS_DOCKER = {Environment.GetEnvironmentVariable("IS_DOCKER")}");
            Console.WriteLine($"[DB-DIAG] Selected ConnectionString = {connectionString}");

            // Диагностика: только парсим строку подключения (без DNS/TCP проб)
            if (!string.IsNullOrEmpty(connectionString))
            {
                try
                {
                    var csb = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
                    Console.WriteLine($"[DB-DIAG] Parsed - Host: {csb.Host}, Port: {csb.Port}, Database: {csb.Database}, User: {csb.Username}");
                }
                catch (Exception parseEx)
                {
                    Console.WriteLine($"[DB-DIAG] ✗ Failed to parse connection string: {parseEx.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[DB-DIAG] ✗ Connection string is empty or null!");
            }

            //������� ����� � retry policy
            var optionsBuilder = new DbContextOptionsBuilder<WebLoggerDbContext>();
            optionsBuilder.UseNpgsql(connectionString, options =>
            {
                options.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
                options.CommandTimeout(120);
            }); // ��������� ������������� PostgreSQL

            // ������������ DbContextOptions<WebLoggerDbContext> � DI
            builder.Services.AddSingleton(provider => optionsBuilder.Options);

            // Ensure database schema exists (non-destructive). Do NOT drop or clear.
            using (var context = new WebLoggerDbContext(optionsBuilder.Options))
            {
                context.Database.EnsureCreated();
            }
            builder.Services.AddAutoMapper(typeof(Program));

            builder.Services.AddSingleton<AppSettingsManager>();
            builder.Services.AddScoped<Contracts.IAsyncRepository<AppSettingsDbStorable>, BlazorServerWebLogger.DataAccess.Repository.EfAsyncRepository<AppSettingsDbStorable>>();
            builder.Services.AddScoped<Contracts.IAsyncRepository<LogEntryDbStorable>, BlazorServerWebLogger.DataAccess.Repository.EfAsyncRepository<LogEntryDbStorable>>();
            builder.Services.AddSingleton<ThreadSafeDbContextFactory>(); // ����������� ������� ������������
            builder.Services.AddSingleton<IRepositoryFactory<LogEntryDbStorable>, RepositoryFactory<LogEntryDbStorable>>(); // ����������� ������� ������������
            builder.Services.AddSingleton<IRepositoryFactory<AppSettingsDbStorable>, RepositoryFactory<AppSettingsDbStorable>>(); // ����������� ������� ������������
            builder.Services.AddSingleton<LogIngestionQueue>();
            builder.Services.AddSingleton<ILogIngestionQueue>(provider => provider.GetRequiredService<LogIngestionQueue>());
            builder.Services.AddHostedService(provider => provider.GetRequiredService<LogIngestionQueue>());
            builder.Services.AddScoped<LogReaderService>();
            builder.Services.AddHostedService<SampleLogGeneratorService>();
            var logEraserEnabled = builder.Configuration.GetSection("LogEraserSettings").GetValue<bool>("Enabled");
            if (logEraserEnabled)
            {
                builder.Services.AddHostedService<LogEraserService>();
            }
            builder.Services.AddHostedService<BlazorServerWebLogger.Services.DatabaseConnectionMonitorService>(); // Мониторинг подключения к БД

            // ���������� Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }
            else
            {

            }
            
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseStaticFiles();

            app.UseRouting();
            app.MapControllers();
            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");

            app.Run();
        }
    }
}
