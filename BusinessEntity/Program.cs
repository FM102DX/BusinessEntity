using System.Diagnostics;
using BusinessEntity.Contracts;
using BusinessEntity.Data;
using BusinessEntity.Data.App;
using BusinessEntity.Data.Services;
using BusinessEntity.Data.Services.HostedServices;
using BusinessEntity.DataAccess.Repository;
using BusinessEntity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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
using Microsoft.EntityFrameworkCore.Infrastructure;

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

			// Добавляем авторизацию и аутентификацию
			builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
				.AddCookie(options =>
				{
					options.LoginPath = PathString.Empty; // Убираем автоматический редирект на логин
					options.LogoutPath = PathString.Empty; // Убираем автоматический редирект на логаут
					options.AccessDeniedPath = PathString.Empty; // Убираем автоматический редирект на access denied
					options.ExpireTimeSpan = TimeSpan.FromHours(24);
					options.SlidingExpiration = true;
					// Важно: полностью отключаем автоматические редиректы
					options.Events.OnRedirectToLogin = context =>
					{
						context.Response.StatusCode = 401;
						return Task.CompletedTask;
					};
					options.Events.OnRedirectToAccessDenied = context =>
					{
						context.Response.StatusCode = 403;
						return Task.CompletedTask;
					};
					options.Events.OnRedirectToLogout = context =>
					{
						context.Response.StatusCode = 200;
						return Task.CompletedTask;
					};
				});

			// Убираем принудительную авторизацию совсем
			builder.Services.AddAuthorization();

			// Добавляем HttpContextAccessor для доступа к контексту запроса
			builder.Services.AddHttpContextAccessor();

			// Добавляем HttpClient для сервиса авторизации
			builder.Services.AddHttpClient();

			// Регистрируем наш сервис авторизации
			builder.Services.AddScoped<IAuterlinkAuthService, AuterlinkAuthService>();

			var _app = new WebLoggerApp();

			// read settings from appsettings.json
			//builder.Services.Configure<LogEraserSettings>(
			//	builder.Configuration.GetSection("LogEraserSettings"));
			//builder.Services.Configure<SampleLogSettings>(
			//	builder.Configuration.GetSection("SampleLogSettings"));

			//builder.Services.AddSingleton(typeof(WebLoggerApp), (x) => _app); 

            var connectionString = builder.Configuration.GetConnectionString("DockerConnection");
			var optionsBuilder = new DbContextOptionsBuilder<KmsBusinessEntityDbContext>();
			optionsBuilder.UseNpgsql(connectionString);


			builder.Services.AddSingleton(provider => optionsBuilder.Options);

			using (var context = new KmsBusinessEntityDbContext(optionsBuilder.Options))
			{
				//context.Database.EnsureDeleted();
				context.Database.EnsureCreated();
			}
			builder.Services.AddAutoMapper(typeof(Program));

			//builder.Services.AddSingleton<AppSettingsManager>();
			//builder.Services.AddScoped<Contracts.IAsyncRepository<AppSettingsDbStorable>, BusinessEntity.DataAccess.Repository.EfAsyncRepository<AppSettingsDbStorable>>();
			//builder.Services.AddScoped<Contracts.IAsyncRepository<LogEntryDbStorable>, BusinessEntity.DataAccess.Repository.EfAsyncRepository<LogEntryDbStorable>>();
			//builder.Services.AddSingleton<ThreadSafeDbContextFactory>();
   //         builder.Services.AddSingleton<IRepositoryFactory<LogEntryDbStorable>, RepositoryFactory<LogEntryDbStorable>>();
			//builder.Services.AddSingleton<IRepositoryFactory<AppSettingsDbStorable>, RepositoryFactory<AppSettingsDbStorable>>();
			
			//builder.Services.AddScoped<LogReaderService>();

			//builder.Services.AddHostedService<SampleLogGeneratorService>();
			//builder.Services.AddHostedService<LogEraserService>();

			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddSwaggerGen();

			var app = builder.Build();

			// Configure the HTTP request pipeline
			if (!app.Environment.IsDevelopment())
			{
				app.UseExceptionHandler("/Error");
				// Не используем HSTS в продакшене для отключения HTTPS
			}
			else
			{

			}
			
			app.UseSwagger();
			app.UseSwaggerUI();

			app.UseStaticFiles();

			app.UseRouting();

			// Добавляем middleware для аутентификации и авторизации
			app.UseAuthentication();
			app.UseAuthorization();

			app.MapControllers();
			app.MapBlazorHub();
			app.MapFallbackToPage("/_Host");

			app.Run();
		}
	}
}
