using BusinessEntity.Contracts;
using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.Services;
using BusinessEntity.DataAccess.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Registration;
using BusinessEntity.MiniApps.UserMiniApp.Registration;
using BusinessEntity.Service;
using BusinessEntity.Service.WebLogging;
using BusinessEntity.Services;
using BusinessEntity.WebLogger.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Radzen;

namespace BusinessEntity
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			builder.Logging.AddConsole();

			builder.Services.AddControllers();
			builder.Services.AddRazorPages();
			builder.Services.AddServerSideBlazor()
				.AddCircuitOptions(options => { options.DetailedErrors = true; });
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddAutoMapper(typeof(Program));

            var genApp = new GenericAppSettings();
            genApp.IsDocker = Environment.GetEnvironmentVariable("IS_DOCKER") == "true";
            builder.Services.AddSingleton(typeof(GenericAppSettings), (x) => genApp);

            var webLoggerSettings = new WebLoggerLocalSettings();
            builder.Configuration.GetSection("WebLoggerLocalSettings").Bind(webLoggerSettings);
            builder.Services.AddSingleton(webLoggerSettings);
            builder.Services.AddScoped<IWebLoggerService, WebLoggerService>();

            builder.Services.AddRadzenComponents();

            var akBaseUrl = (
                Environment.GetEnvironmentVariable("AUTHENTIK_BASE_URL")
                ?? builder.Configuration["AuthentikAuth:BaseUrl"]
                ?? "http://localhost:9000").TrimEnd('/') + "/";
            var akBrowserBaseUrl = (
                Environment.GetEnvironmentVariable("AUTHENTIK_BASE_URL_FOR_BROWSER")
                ?? builder.Configuration["AuthentikAuth:BaseUrlForBrowser"]
                ?? builder.Configuration["AuthentikAuth:BaseUrl"]
                ?? "http://localhost:9000").TrimEnd('/');
            var akHostHeader = new Uri(akBrowserBaseUrl).Authority;

            builder.Services.AddHttpClient("AuthentikAuth", client =>
            {
                client.BaseAddress = new Uri(akBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Host = akHostHeader;
            });

			builder.Services.AddAuthentication(options =>
			{
				options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
				options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
				options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
			})
			.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
			{
				options.LoginPath = "/auth/login";
				options.LogoutPath = "/auth/logout";
				options.AccessDeniedPath = "/auth/access-denied";
				options.ExpireTimeSpan = TimeSpan.FromHours(8);
				options.SlidingExpiration = false;
				options.Cookie.HttpOnly = true;
				options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
				options.Cookie.SameSite = SameSiteMode.Lax;
				options.Cookie.Name = "be.auth";
				options.Events = new CookieAuthenticationEvents
				{
					OnValidatePrincipal = context =>
					{
						var sessionManager = context.HttpContext.RequestServices.GetRequiredService<AuthentikSessionManager>();
						return sessionManager.RefreshSessionAsync(context);
					}
				};
			});

            builder.Services.AddAuthorization();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<AuthentikSessionManager>();
            builder.Services.AddDataProviderMiniApp();
            builder.Services.AddUserMiniApp();

            builder.Services.AddScoped<IPossibleEntityRelationTypesProvider, PossibleEntityRelationTypesProvider>();
            builder.Services.AddScoped<BusinessEntity.Core.Services.BusinessEntityHelper>();
            builder.Services.AddScoped<BusinessEntity.Services.SpaceHelper>();
            builder.Services.AddScoped<BusinessEntity.Core.Contracts.ISampleDataService, BusinessEntity.Core.Services.SampleDataService>();
            builder.Services.AddScoped<BusinessEntity.Core.Contracts.IDataFillLineProvider, BusinessEntity.Services.DataFillLineProvider>();
            builder.Services.AddScoped<BusinessEntity.Contracts.IUserContextService, BusinessEntity.Services.UserContextService>();
            builder.Services.AddScoped<ReactiveUI.IMessageBus, ReactiveUI.MessageBus>();
            builder.Services.AddScoped<BusinessEntity.Services.ITreeSelectionService, BusinessEntity.Services.TreeSelectionService>();

            var connectionString = builder.Configuration.GetConnectionString("DockerConnection");
			var optionsBuilder = new DbContextOptionsBuilder<KmsBusinessEntityDbContext>();
			optionsBuilder.UseNpgsql(connectionString);

			builder.Services.AddSingleton(provider => optionsBuilder.Options);
            builder.Services.AddSingleton<ThreadSafeDbContextFactory>();

			using (var context = new KmsBusinessEntityDbContext(optionsBuilder.Options))
			{
				context.Database.EnsureCreated();
			}

			builder.Services.AddSwaggerGen();

			var app = builder.Build();

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

			if (!app.Environment.IsDevelopment())
			{
				app.UseExceptionHandler("/Error");
			}
			
			app.UseSwagger();
			app.UseSwaggerUI();
			app.UseStaticFiles();
			app.UseRouting();
			app.UseAuthentication();
			app.UseAuthorization();

			app.UseMiddleware<BusinessEntity.Middleware.SpaceSelectionMiddleware>();
			app.MapControllers();
			app.MapRazorPages();
			app.MapBlazorHub();
			app.MapFallbackToPage("/_Host");
			app.Run();
		}
	}
}
