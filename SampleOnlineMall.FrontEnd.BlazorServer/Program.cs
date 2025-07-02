using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Mappers;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.DataAccess.DataAccess;
using SampleOnlineMall.FrontEnd.BlazorServer.Data;
using SampleOnlineMall.Service;
using SampleOnlineMall.Service.WebLogging;
using SampleOnlineMall.WebLogger.Services;
using SampleOnlineMall.FrontEnd.BlazorServer.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Serilog;
using Serilog.Events;


namespace SampleOnlineMall.FrontEnd.BlazorServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();

            // Добавляем авторизацию и аутентификацию
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/auterlink/login";
                    options.LogoutPath = "/auterlink/logout";
                    options.AccessDeniedPath = "/unauthorized";
                    options.ExpireTimeSpan = TimeSpan.FromHours(24);
                    options.SlidingExpiration = true;
                });

            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });

            // Добавляем HttpContextAccessor для доступа к контексту запроса
            builder.Services.AddHttpContextAccessor();

            // Регистрируем наш сервис авторизации
            builder.Services.AddScoped<IAuterlinkAuthService, AuterlinkAuthService>();

            #region custom calls

            // App classes
            var genApp = new GenericAppSettings();
            genApp.IsDocker = Environment.GetEnvironmentVariable("IS_DOCKER") == "true";
            builder.Services.AddSingleton(typeof(GenericAppSettings), (x) => genApp);

            // Регистрация WebLoggerLocalSettings в DI
            var webLoggerSettings = new WebLoggerLocalSettings();
            builder.Configuration.GetSection("WebLoggerLocalSettings").Bind(webLoggerSettings);
            builder.Services.AddSingleton(webLoggerSettings);
            builder.Services.AddScoped<IWebLoggerService, WebLoggerService>();

            // Получение логгера
            var serviceProvider = builder.Services.BuildServiceProvider();
            var wLogger = serviceProvider.GetRequiredService<IWebLoggerService>();
            wLogger.Information("App launched");

            var _app = new SampleOnlineMallFrontEndBlazorApp();

            builder.Services.AddScoped<IAsyncRepository<CommodityItemFrontend>>(serviceProvider =>
            {
                wLogger.Information($"INTRO");
                var baseAddress = genApp.IsDocker ? "http://assort-api-container:80/" : "http://localhost:5010/";
                wLogger.Information($"B_ADDR={baseAddress}");
                var options = new WebApiAsyncRepositoryOptions()
                    .SetBaseAddress(baseAddress)
                    .SetGetAllHostPath("frontend/getall/")
                    .SetGetByIdOrNullHostPath("frontend/GetByIdOrNull/")
                    .SetGetAllByRequestHostPath("frontend/getallbyrequest/")
                    .SetSearchHostPath("frontend/search/");
                wLogger.SendObject($"{options}");
                return new WebApiAsyncRepository<CommodityItemFrontend>(options, wLogger);
            });


            builder.Services.AddScoped(typeof(SampleOnlineMallFrontEndBlazorApp), typeof(SampleOnlineMallFrontEndBlazorApp));
            builder.Services.AddScoped(provider => new FrontEndSettings()
            {
                DisplayMainHorizontalMenu = false,
                DisplayNavBar = false, 
                DisplayTopHorizontalMenu = false

            });
            builder.Services.AddScoped(typeof(StoreManager), typeof(StoreManager));
            
            builder.Services.AddScoped(typeof(CustomMapper), typeof(CustomMapper));
            
            builder.Services.AddScoped(typeof(ComponentHub), typeof(ComponentHub));

            #endregion 

            var app = builder.Build();
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            // Добавляем middleware для аутентификации и авторизации
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });

            app.Run();
        }
    }
}