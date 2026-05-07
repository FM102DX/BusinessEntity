using BusinessEntity.Contracts;
using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.Services;
using BusinessEntity.DataAccess.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Registration;
using BusinessEntity.MiniApps.SampleDataMiniApp.Contracts;
using BusinessEntity.MiniApps.SampleDataMiniApp.Registration;
using BusinessEntity.MiniApps.TreeMiniApp.Contracts;
using BusinessEntity.MiniApps.TreeMiniApp.Registration;
using BusinessEntity.MiniApps.UserMiniApp.Contracts;
using BusinessEntity.MiniApps.UserMiniApp.Registration;
using BusinessEntity.MiniApps.UserMiniApp.Storage;
using BusinessEntity.Service;
using BusinessEntity.Service.WebLogging;
using BusinessEntity.Services;
using BusinessEntity.Services.RichTextImport;
using BusinessEntity.Settings;
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

			// Включает базовый консольный логгер для всего приложения.
			builder.Logging.AddConsole();
            builder.Logging.AddFilter("System.Net.Http.HttpClient.WebLogger", LogLevel.Warning);

			// Регистрирует базовый ASP.NET Core UI/API стек.
			builder.Services.AddControllers();
			builder.Services.AddRazorPages();
			builder.Services.AddServerSideBlazor()
				.AddCircuitOptions(options => { options.DetailedErrors = true; })
				.AddHubOptions(options =>
				{
					options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
				});
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddAutoMapper(typeof(Program));

            // Регистрирует общие настройки приложения и режим запуска.
            var genApp = new GenericAppSettings();
            genApp.IsDocker = Environment.GetEnvironmentVariable("IS_DOCKER") == "true";
            builder.Services.AddSingleton(typeof(GenericAppSettings), (x) => genApp);

            // Регистрирует локальные настройки web-логгера и его сервис.
            var webLoggerSettings = new WebLoggerLocalSettings();
            builder.Configuration.GetSection("WebLoggerLocalSettings").Bind(webLoggerSettings);
            builder.Services.AddSingleton(webLoggerSettings);
            builder.Services.AddHttpClient("WebLogger", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
            });
            builder.Services.AddSingleton<WebLoggerService>();
            builder.Services.AddSingleton<IWebLoggerService>(provider => provider.GetRequiredService<WebLoggerService>());
            builder.Services.AddHostedService(provider => provider.GetRequiredService<WebLoggerService>());

            // Подключает UI-компоненты Radzen.
            builder.Services.AddRadzenComponents();

            // Настраивает HTTP-клиент для интеграции с Authentik.
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

			// Подключает cookie-аутентификацию приложения.
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

            // Регистрирует авторизацию и веб-контекст запроса.
            builder.Services.AddAuthorization();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<AuthentikSessionManager>();

            // Регистрирует mini-app модули приложения в DI.
            builder.Services.AddDataProviderMiniApp();
            builder.Services.AddSampleDataMiniApp();
            builder.Services.AddTreeMiniApp();
            builder.Services.AddUserMiniApp();

            // Регистрирует прикладные сервисы и message bus.
            builder.Services.Configure<RichTextDocumentSettings>(
                builder.Configuration.GetSection(RichTextDocumentSettings.SectionName));
            builder.Services.AddScoped<IPossibleEntityRelationTypesProvider, PossibleEntityRelationTypesProvider>();
            builder.Services.AddScoped<IBusinessEntityFactory, BusinessEntityFactory>();
            builder.Services.AddScoped<BusinessEntity.Core.Services.BusinessEntityHelper>();
            builder.Services.AddScoped<BusinessEntity.Services.SpaceHelper>();
            builder.Services.AddScoped<BusinessEntity.Services.RichTextDocumentHelper>();
            builder.Services.AddScoped<BusinessEntity.Services.RichTextDocumentSettingsService>();
            builder.Services.AddScoped<HtmlToRichTextBlocksConverter>();
            builder.Services.AddScoped<IRichDocFormatConverter, PlainTextRichTextImportConverter>();
            builder.Services.AddScoped<IRichDocFormatConverter, MarkdownRichTextImportConverter>();
            builder.Services.AddScoped<IRichDocFormatConverter, HtmlRichTextImportConverter>();
            builder.Services.AddScoped<IRichDocFormatConverterFactory, RichDocFormatConverterFactory>();
            builder.Services.AddScoped<BusinessEntity.Services.RichTextDocumentImportService>();
            builder.Services.AddScoped<BusinessEntity.Contracts.IUserContextService, BusinessEntity.Services.UserContextService>();
            builder.Services.AddScoped<ReactiveUI.IMessageBus, ReactiveUI.MessageBus>();
            builder.Services.AddScoped<BusinessEntity.Services.ITreeSelectionService, BusinessEntity.Services.TreeSelectionService>();

            // Настраивает EF Core и фабрику DbContext для работы с БД.
            var connectionString = builder.Configuration.GetConnectionString("DockerConnection");
			var optionsBuilder = new DbContextOptionsBuilder<KmsBusinessEntityDbContext>();
			optionsBuilder.UseNpgsql(connectionString);
            var userMiniAppOptionsBuilder = new DbContextOptionsBuilder<UserMiniAppDbContext>();
            userMiniAppOptionsBuilder.UseNpgsql(connectionString);

			builder.Services.AddSingleton(provider => optionsBuilder.Options);
            builder.Services.AddSingleton(provider => userMiniAppOptionsBuilder.Options);
            builder.Services.AddSingleton<ThreadSafeDbContextFactory>();

			using (var context = new KmsBusinessEntityDbContext(optionsBuilder.Options))
			{
				EnsureBusinessEntityStorageSchema(context);
			}

            using (var userMiniAppContext = new UserMiniAppDbContext(userMiniAppOptionsBuilder.Options))
            {
                UserMiniAppStorageSchema.EnsureSchema(userMiniAppContext);
            }

			// Подключает Swagger для диагностики API.
			builder.Services.AddSwaggerGen();

			var app = builder.Build();

            // Явно поднимает DataProviderMiniApp при старте приложения.
            using (var scope = app.Services.CreateScope())
            {
                var dataProviderMiniApp = scope.ServiceProvider.GetRequiredService<IDataProviderMiniApp>();
                dataProviderMiniApp.EnsureInitialized();
            }

            // Явно поднимает UserMiniApp при старте приложения.
            using (var scope = app.Services.CreateScope())
            {
                var userMiniApp = scope.ServiceProvider.GetRequiredService<IUserMiniApp>();
                userMiniApp.EnsureInitialized();
            }

            // Явно поднимает TreeMiniApp при старте приложения.
            using (var scope = app.Services.CreateScope())
            {
                var treeMiniApp = scope.ServiceProvider.GetRequiredService<ITreeMiniApp>();
                treeMiniApp.EnsureInitialized();
            }

            // Инициализирует тестовые данные при старте приложения.
            using (var scope = app.Services.CreateScope())
            {
                try
                {
                    var sampleDataMiniApp = scope.ServiceProvider.GetRequiredService<ISampleDataMiniApp>();
                    sampleDataMiniApp.EnsureInitializedAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Sample data seeding failed");
                }
            }

			// Подключает production-only обработчик ошибок.
			if (!app.Environment.IsDevelopment())
			{
				app.UseExceptionHandler("/Error");
			}
			
			// Собирает HTTP pipeline приложения.
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

        // Явно создает DTO-таблицы mini-app в shared Postgres-базе, даже если в базе уже есть таблицы других сервисов.
        private static void EnsureBusinessEntityStorageSchema(KmsBusinessEntityDbContext context)
        {
            context.Database.EnsureCreated();

            context.Database.ExecuteSqlRaw(
                @"
                CREATE TABLE IF NOT EXISTS ""BusinessEntities"" (
                    ""Id"" uuid NOT NULL,
                    ""CreatedDate"" timestamp with time zone NOT NULL,
                    ""LastModifiedDate"" timestamp with time zone NOT NULL,
                    ""Name"" text NOT NULL,
                    ""BusinessEntityType"" integer NOT NULL,
                    ""EntityType"" integer NOT NULL,
                    CONSTRAINT ""PK_BusinessEntities"" PRIMARY KEY (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""BusinessEntityRelations"" (
                    ""Id"" uuid NOT NULL,
                    ""CreatedDate"" timestamp with time zone NOT NULL,
                    ""LastModifiedDate"" timestamp with time zone NOT NULL,
                    ""ObjectAId"" uuid NOT NULL,
                    ""ObjectBId"" uuid NOT NULL,
                    ""RelationType"" text NOT NULL,
                    ""RelationParams"" text NOT NULL,
                    CONSTRAINT ""PK_BusinessEntityRelations"" PRIMARY KEY (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""BusinessEntityDataItems"" (
                    ""Id"" uuid NOT NULL,
                    ""CreatedDate"" timestamp with time zone NOT NULL,
                    ""LastModifiedDate"" timestamp with time zone NOT NULL,
                    ""BusinessEntityId"" uuid NOT NULL,
                    ""Version"" integer NOT NULL DEFAULT 1,
                    ""Data"" text NOT NULL,
                    CONSTRAINT ""PK_BusinessEntityDataItems"" PRIMARY KEY (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""BusinessEntityDataChunks"" (
                    ""Id"" uuid NOT NULL,
                    ""CreatedDate"" timestamp with time zone NOT NULL,
                    ""LastModifiedDate"" timestamp with time zone NOT NULL,
                    ""BusinessEntityId"" uuid NOT NULL,
                    ""SortOrder"" bigint NOT NULL,
                    ""Data"" text NOT NULL,
                    ""PlainText"" text NULL,
                    ""HtmlCache"" text NULL,
                    ""BlockCount"" integer NOT NULL DEFAULT 0,
                    ""CharCount"" integer NOT NULL DEFAULT 0,
                    ""DataSizeBytes"" integer NOT NULL DEFAULT 0,
                    ""Version"" integer NOT NULL DEFAULT 1,
                    ""Checksum"" text NULL,
                    CONSTRAINT ""PK_BusinessEntityDataChunks"" PRIMARY KEY (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""BusinessEntityProperties"" (
                    ""Id"" uuid NOT NULL,
                    ""CreatedDate"" timestamp with time zone NOT NULL,
                    ""LastModifiedDate"" timestamp with time zone NOT NULL,
                    ""ParentEntityId"" uuid NOT NULL,
                    ""PropertyType"" integer NOT NULL,
                    ""Data"" text NOT NULL,
                    ""Metadata"" text NOT NULL,
                    CONSTRAINT ""PK_BusinessEntityProperties"" PRIMARY KEY (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""BusinessEntityDataProperties"" (
                    ""Id"" uuid NOT NULL,
                    ""CreatedDate"" timestamp with time zone NOT NULL,
                    ""LastModifiedDate"" timestamp with time zone NOT NULL,
                    ""ParentEntityId"" uuid NOT NULL,
                    ""PropertyType"" integer NOT NULL,
                    ""Data"" text NOT NULL,
                    ""Metadata"" text NOT NULL,
                    CONSTRAINT ""PK_BusinessEntityDataProperties"" PRIMARY KEY (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""BusinessEntityDataChunkProperties"" (
                    ""Id"" uuid NOT NULL,
                    ""CreatedDate"" timestamp with time zone NOT NULL,
                    ""LastModifiedDate"" timestamp with time zone NOT NULL,
                    ""ParentEntityId"" uuid NOT NULL,
                    ""PropertyType"" integer NOT NULL,
                    ""Data"" text NOT NULL,
                    ""Metadata"" text NOT NULL,
                    CONSTRAINT ""PK_BusinessEntityDataChunkProperties"" PRIMARY KEY (""Id"")
                );

                CREATE INDEX IF NOT EXISTS ""IX_BusinessEntityRelations_ObjectAId"" ON ""BusinessEntityRelations"" (""ObjectAId"");
                CREATE INDEX IF NOT EXISTS ""IX_BusinessEntityRelations_ObjectBId"" ON ""BusinessEntityRelations"" (""ObjectBId"");
                ALTER TABLE ""BusinessEntityDataItems"" ADD COLUMN IF NOT EXISTS ""Version"" integer NOT NULL DEFAULT 1;
                ALTER TABLE ""BusinessEntityDataChunks"" ADD COLUMN IF NOT EXISTS ""Version"" integer NOT NULL DEFAULT 1;
                CREATE INDEX IF NOT EXISTS ""IX_BusinessEntityDataItems_BusinessEntityId"" ON ""BusinessEntityDataItems"" (""BusinessEntityId"");
                CREATE INDEX IF NOT EXISTS ""IX_BusinessEntityDataItems_BusinessEntityId_Version"" ON ""BusinessEntityDataItems"" (""BusinessEntityId"", ""Version"");
                CREATE INDEX IF NOT EXISTS ""IX_BusinessEntityDataChunks_BusinessEntityId_SortOrder"" ON ""BusinessEntityDataChunks"" (""BusinessEntityId"", ""SortOrder"");
                CREATE INDEX IF NOT EXISTS ""IX_BusinessEntityDataChunks_BusinessEntityId_SortOrder_Version"" ON ""BusinessEntityDataChunks"" (""BusinessEntityId"", ""SortOrder"", ""Version"");
                CREATE INDEX IF NOT EXISTS ""IX_BusinessEntityProperties_ParentEntityId"" ON ""BusinessEntityProperties"" (""ParentEntityId"");
                CREATE INDEX IF NOT EXISTS ""IX_BusinessEntityProperties_ParentEntityId_PropertyType"" ON ""BusinessEntityProperties"" (""ParentEntityId"", ""PropertyType"");
                CREATE INDEX IF NOT EXISTS ""IX_BusinessEntityDataProperties_ParentEntityId"" ON ""BusinessEntityDataProperties"" (""ParentEntityId"");
                CREATE INDEX IF NOT EXISTS ""IX_BusinessEntityDataProperties_ParentEntityId_PropertyType"" ON ""BusinessEntityDataProperties"" (""ParentEntityId"", ""PropertyType"");
                CREATE INDEX IF NOT EXISTS ""IX_BusinessEntityDataChunkProperties_ParentEntityId"" ON ""BusinessEntityDataChunkProperties"" (""ParentEntityId"");
                CREATE INDEX IF NOT EXISTS ""IX_BusinessEntityDataChunkProperties_ParentEntityId_PropertyType"" ON ""BusinessEntityDataChunkProperties"" (""ParentEntityId"", ""PropertyType"");
                ");
        }
	}
}
