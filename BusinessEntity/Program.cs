using BusinessEntity.Services;
using BusinessEntity.Middleware;
using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using BusinessEntity.DataAccess.Classes;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BusinessEntity.DataAccess.Repositories;
using Radzen;
using BusinessEntity.Contracts;
using SampleOnlineMall.Service;
using SampleOnlineMall.Service.WebLogging;
using SampleOnlineMall.WebLogger.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using BusinessEntity.Authentik;
using BusinessEntity.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace BusinessEntity
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			// Ensure console logging is enabled early so bootstrap logs appear in console
			builder.Logging.AddConsole();

			// Add services to the container.
			builder.Services.AddControllers();
			builder.Services.AddRazorPages();
			builder.Services.AddServerSideBlazor()
				.AddCircuitOptions(options => { options.DetailedErrors = true; });
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddAutoMapper(typeof(Program));

            // App classes
            var genApp = new GenericAppSettings();
            genApp.IsDocker = Environment.GetEnvironmentVariable("IS_DOCKER") == "true";
            builder.Services.AddSingleton(typeof(GenericAppSettings), (x) => genApp);

            // Регистрация WebLoggerLocalSettings в DI
            var webLoggerSettings = new WebLoggerLocalSettings();
            builder.Configuration.GetSection("WebLoggerLocalSettings").Bind(webLoggerSettings);
            builder.Services.AddSingleton(webLoggerSettings);
            builder.Services.AddScoped<IWebLoggerService, WebLoggerService>();

            // Add Radzen services
            builder.Services.AddRadzenComponents();
            
            // Authentik OIDC bootstrap and configuration
            // 1) Bootstrap: Ensure() idempotently creates/patches provider+application in Authentik
            //    and returns Authority/ClientId/ClientSecret/RedirectUris for our app.
            // 2) OIDC: AddAuthentikOpenIdConnect() registers OpenID Connect handler with these settings.
            //    Authority uses /application/o/{slug}/; built-in middleware handles /signin-oidc callback.
            var appName = builder.Environment.ApplicationName ?? "BusinessEntity";
            BusinessEntity.Authentik.CreatedOidcSettings oidcSettings;
            using (var temp = builder.Services.BuildServiceProvider())
            using (var scope = temp.CreateScope())
            {
                var webLogger = scope.ServiceProvider.GetService<IWebLoggerService>();
                var consoleLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                _ = webLogger?.Information("[AK] Program: starting bootstrap");
                oidcSettings = AuthentikBootstrapService.Ensure(appName, builder.Configuration, consoleLogger, webLogger);
                _ = webLogger?.Information("[AK] Program: bootstrap completed");
            }
            builder.Services.AddAuthentikOpenIdConnect(oidcSettings);

			// Настройка JWT аутентификации
			var jwtSettings = builder.Configuration.GetSection("JwtSettings");
			var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyForJWTTokenGeneration1234567890";
			var issuer = jwtSettings["Issuer"] ?? "http://localhost:9000";
			var audience = jwtSettings["Audience"] ?? "business-entity";

			// Authentication defaults
			// - DefaultScheme/Authenticate: Cookies (local session)
			// - DefaultChallenge: OpenID Connect (redirects unauthenticated users to Authentik)
			builder.Services.AddAuthentication(options =>
			{
				options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
				options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
				options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
			})
			.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
			{
				options.LoginPath = "/auth/login";
				options.LogoutPath = "/auth/logout";
				options.AccessDeniedPath = "/unauthorized";
				options.ExpireTimeSpan = TimeSpan.FromHours(24);
				options.SlidingExpiration = true;
				options.Cookie.HttpOnly = true;
				options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
				options.Cookie.SameSite = SameSiteMode.Lax;
			})
			.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
			{
				options.TokenValidationParameters = new TokenValidationParameters
				{
					ValidateIssuer = true,
					ValidateAudience = true,
					ValidateLifetime = true,
					ValidateIssuerSigningKey = true,
					ValidIssuer = issuer,
					ValidAudience = audience,
					IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
					ClockSkew = TimeSpan.Zero
				};

				// Настройка для получения токена из куки и заголовка
				options.Events = new JwtBearerEvents
				{
					OnMessageReceived = context =>
					{
						// Проверяем токен в куки сначала
						var token = context.Request.Cookies["jwt_token"];
						if (string.IsNullOrEmpty(token))
						{
							// Если нет в куки, проверяем заголовок Authorization
							token = context.Request.Headers["Authorization"]
								.FirstOrDefault()?.Split(" ").Last();
						}
						
						if (!string.IsNullOrEmpty(token))
						{
							context.Token = token;
						}
						
						return Task.CompletedTask;
					},
					OnAuthenticationFailed = context =>
					{
						var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
						logger.LogWarning("JWT authentication failed: {Error}", context.Exception.Message);
						return Task.CompletedTask;
					}
				};
			});

			builder.Services.AddAuthorization();

			// Добавляем HttpContextAccessor для доступа к контексту запроса
			builder.Services.AddHttpContextAccessor();

			// Добавляем HttpClient для сервиса авторизации
			builder.Services.AddHttpClient();

			            // Настройка HttpClient для Authentic/Authentik (base URL из ENV или конфигурации)
            var akBaseUrl = Environment.GetEnvironmentVariable("AUTHENTIK_BASE_URL")
                             ?? builder.Configuration["AuthentIC2:BaseUrl"]
                             ?? "http://localhost:9000";
            akBaseUrl = akBaseUrl.TrimEnd('/') + "/";

            builder.Services.AddHttpClient("AuthentIC", client =>
            {
                client.BaseAddress = new Uri(akBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            builder.Services.AddHttpClient("AuthentIC2", client =>
            {
                client.BaseAddress = new Uri(akBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            // Регистрируем наш сервис авторизации
            builder.Services.AddScoped<IApplicationSideAuthService, ApplicationSideAuthService>();
            
           
            // Регистрируем сервис возможных типов отношений между сущностями
            builder.Services.AddScoped<IPossibleEntityRelationTypesProvider, PossibleEntityRelationTypesProvider>();

            // Регистрируем репозитории
            builder.Services.AddSingleton<BusinessEntity.Core.Contracts.IAsyncRepository<BusinessEntity.Core.Classes.BusinessEntity>, InMemoryRepository<BusinessEntity.Core.Classes.BusinessEntity>>();
            builder.Services.AddSingleton<BusinessEntity.Core.Contracts.IAsyncRepository<BusinessEntity.Core.Classes.Relation>, InMemoryRepository<BusinessEntity.Core.Classes.Relation>>();
            builder.Services.AddSingleton<BusinessEntity.Core.Contracts.IAsyncRepository<BusinessEntity.Core.Classes.BusinessEntityData>, InMemoryRepository<BusinessEntity.Core.Classes.BusinessEntityData>>();

            // Регистрируем BusinessEntityHelper
            builder.Services.AddScoped<BusinessEntity.Core.Services.BusinessEntityHelper>();

            // Регистрируем SpaceHelper
            builder.Services.AddScoped<BusinessEntity.Services.SpaceHelper>();

            // Регистрируем SampleDataService как Scoped (не Singleton), так как он зависит от Scoped BusinessEntityHelper
            builder.Services.AddScoped<BusinessEntity.Core.Contracts.ISampleDataService, BusinessEntity.Core.Services.SampleDataService>();

            // Регистрируем поставщик строк для тестового наполнения документов (Scoped)
            builder.Services.AddScoped<BusinessEntity.Core.Contracts.IDataFillLineProvider, BusinessEntity.Services.DataFillLineProvider>();

            // Регистрируем UserContextService для хранения выбранного пространства
            builder.Services.AddScoped<BusinessEntity.Contracts.IUserContextService, BusinessEntity.Services.UserContextService>();

            // ReactiveUI MessageBus как событийная шина (per-circuit)
            builder.Services.AddScoped<ReactiveUI.IMessageBus, ReactiveUI.MessageBus>();

            // Регистрируем TreeSelectionService для управления выбором узлов дерева
            builder.Services.AddScoped<BusinessEntity.Services.ITreeSelectionService, BusinessEntity.Services.TreeSelectionService>();

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

			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddSwaggerGen();

			var app = builder.Build();

            // Seed sample data once at startup
            using (var scope = app.Services.CreateScope())
            {
                try
                {
                    var seeder = scope.ServiceProvider.GetRequiredService<BusinessEntity.Core.Contracts.ISampleDataService>();
                    seeder.InitializeSampleDataAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Sample data seeding failed");
                }
            }

			// Configure the HTTP request pipeline
			if (!app.Environment.IsDevelopment())
			{
				app.UseExceptionHandler("/Error");
			}
			
			app.UseSwagger();
			app.UseSwaggerUI();
			app.UseStaticFiles();
			app.UseRouting();

			// Добавляем наш JWT middleware перед аутентификацией
			app.UseMiddleware<JwtAuthenticationMiddleware>();

			// ASP.NET Core auth pipeline: UseAuthentication() must come before UseAuthorization().
			// OIDC and cookies are configured above; unauthenticated requests will be challenged to Authentik.
			app.UseAuthentication();
			app.UseAuthorization();

			// Middleware, перенаправляющее на страницу выбора пространства при его отсутствии
			app.UseMiddleware<BusinessEntity.Middleware.SpaceSelectionMiddleware>();
			app.MapControllers();
			app.MapRazorPages();
			app.MapBlazorHub();
			app.MapFallbackToPage("/_Host");
			app.Run();
		}
	}
}
