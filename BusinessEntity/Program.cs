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

			// Настройка JWT аутентификации
			var jwtSettings = builder.Configuration.GetSection("JwtSettings");
			var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyForJWTTokenGeneration1234567890";
			var issuer = jwtSettings["Issuer"] ?? "http://localhost:9000";
			var audience = jwtSettings["Audience"] ?? "business-entity";

			// Настройка аутентификации с приоритетом Cookie
			builder.Services.AddAuthentication(options =>
			{
				options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
				options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
				options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
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

			// Настройка HttpClient для Authentic
			builder.Services.AddHttpClient("AuthentIC", client =>
			{
				client.BaseAddress = new Uri("http://localhost:9000/");
				client.Timeout = TimeSpan.FromSeconds(30);
			});
            builder.Services.AddHttpClient("AuthentIC2", client =>
            {
                client.BaseAddress = new Uri("http://authentic-server-1:9000");
                client.Timeout = TimeSpan.FromSeconds(30);
            });            // Регистрируем наш сервис авторизации
            builder.Services.AddScoped<IApplicationSideAuthService, ApplicationSideAuthService>();
            
           
            // Регистрируем сервис возможных типов отношений между сущностями
            builder.Services.AddScoped<IPossibleEntityRelationTypesProvider, PossibleEntityRelationTypesProvider>();

            // Регистрируем репозитории
            builder.Services.AddSingleton<BusinessEntity.Core.Contracts.IAsyncRepository<BusinessEntity.Core.Classes.BusinessEntity>, InMemoryRepository<BusinessEntity.Core.Classes.BusinessEntity>>();
            builder.Services.AddSingleton<BusinessEntity.Core.Contracts.IAsyncRepository<BusinessEntity.Core.Classes.Relation>, InMemoryRepository<BusinessEntity.Core.Classes.Relation>>();

            // Регистрируем BusinessEntityHelper
            builder.Services.AddScoped<BusinessEntity.Core.Services.BusinessEntityHelper>();

            // Регистрируем SpaceHelper
            builder.Services.AddScoped<BusinessEntity.Services.SpaceHelper>();

            // Регистрируем SampleDataService как Scoped (не Singleton), так как он зависит от Scoped BusinessEntityHelper
            builder.Services.AddScoped<BusinessEntity.Core.Contracts.ISampleDataService, BusinessEntity.Core.Services.SampleDataService>();

            // Регистрируем UserContextService для хранения выбранного пространства
            builder.Services.AddScoped<BusinessEntity.Contracts.IUserContextService, BusinessEntity.Services.UserContextService>();

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

			// Добавляем middleware для аутентификации и авторизации
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
