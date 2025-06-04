using System.Diagnostics;
using BusinessEntity.Contracts;
using BusinessEntity.Data;
using BusinessEntity.Data.App;
using BusinessEntity.Data.Services;
using BusinessEntity.Data.Services.HostedServices;
using BusinessEntity.DataAccess.Repository;
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

namespace BusinessEntity
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

            //Создаем опции
            var optionsBuilder = new DbContextOptionsBuilder<WebLoggerDbContext>();
            optionsBuilder.UseNpgsql(connectionString); // Указываем использование PostgreSQL

            // Регистрируем DbContextOptions<WebLoggerDbContext> в DI
            builder.Services.AddSingleton(provider => optionsBuilder.Options);

            // Убедитесь, что база данных и таблицы созданы
            using (var context = new WebLoggerDbContext(optionsBuilder.Options))
            {
                //context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            }
            builder.Services.AddAutoMapper(typeof(Program));

            builder.Services.AddSingleton<AppSettingsManager>();
            builder.Services.AddScoped<Contracts.IAsyncRepository<AppSettingsDbStorable>, BusinessEntity.DataAccess.Repository.EfAsyncRepository<AppSettingsDbStorable>>();
            builder.Services.AddScoped<Contracts.IAsyncRepository<LogEntryDbStorable>, BusinessEntity.DataAccess.Repository.EfAsyncRepository<LogEntryDbStorable>>();
            builder.Services.AddSingleton<ThreadSafeDbContextFactory>(); // Регистрация фабрики дбконтекстов
            builder.Services.AddSingleton<IRepositoryFactory<LogEntryDbStorable>, RepositoryFactory<LogEntryDbStorable>>(); // Регистрация фабрики репозиториев
            builder.Services.AddSingleton<IRepositoryFactory<AppSettingsDbStorable>, RepositoryFactory<AppSettingsDbStorable>>(); // Регистрация фабрики репозиториев
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
