using System.Diagnostics;
using BlazorServerWebLogger.Contracts;
using BlazorServerWebLogger.Data;
using BlazorServerWebLogger.Data.App;
using BlazorServerWebLogger.Data.Services;
using BlazorServerWebLogger.Data.Services.HostedServices;
using BlazorServerWebLogger.DataAccess.Repository;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Models;
using SampleOnlineMall.DataAccess;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.WebLogger.DataAccess;
using SampleOnlineMall.WebLogger.Models;
using AutoMapper;

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
            var _app = new WebLoggerApp();

            // Чтение настроек из appsettings.json
            builder.Services.Configure<LogEraserSettings>(
                builder.Configuration.GetSection("LogEraserSettings"));
            builder.Services.Configure<SampleLogSettings>(
                builder.Configuration.GetSection("SampleLogSettings"));

            builder.Services.AddSingleton(typeof(WebLoggerApp), (x) => _app); // само приложение
           
            var connectionString = Environment.GetEnvironmentVariable("IS_DOCKER") == "true"
                ? builder.Configuration.GetConnectionString("DockerConnection")
                : builder.Configuration.GetConnectionString("IisExpressConnection");

            Console.WriteLine($"ConnectionString={connectionString}");

            // Регистрируем DbContextOptions<WebLoggerDbContext> в DI
            builder.Services.AddSingleton(provider =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<WebLoggerDbContext>();
                optionsBuilder.UseNpgsql(connectionString); // Указываем использование PostgreSQL
                return optionsBuilder.Options;
            });

            
            builder.Services.AddScoped<Contracts.IAsyncRepository<AppSettingsDbStorable>, BlazorServerWebLogger.DataAccess.Repository.EfAsyncRepository<AppSettingsDbStorable>>();
            builder.Services.AddScoped<Contracts.IAsyncRepository<LogEntryDbStorable>, BlazorServerWebLogger.DataAccess.Repository.EfAsyncRepository<LogEntryDbStorable>>();
            builder.Services.AddSingleton<ThreadSafeDbContextFactory>(); // Регистрация фабрики дбконтекстов
            builder.Services.AddSingleton<IRepositoryFactory<LogEntryDbStorable>, RepositoryFactory<LogEntryDbStorable>>(); // Регистрация фабрики репозиториев
            builder.Services.AddScoped<LogReaderService>();
            builder.Services.AddHostedService<SampleLogGeneratorService>();
            builder.Services.AddHostedService<LogEraserService>();

            // Добавление Swagger
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
