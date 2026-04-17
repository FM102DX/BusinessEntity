using BusinessEntity.MiniApps.UserMiniApp.Connectors;
using BusinessEntity.MiniApps.UserMiniApp.Contracts;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Connectors;
using BusinessEntity.MiniApps.UserMiniApp.Facade;
using BusinessEntity.MiniApps.UserMiniApp.Internal;

namespace BusinessEntity.MiniApps.UserMiniApp.Registration
{
    // Регистрирует все публичные и внутренние компоненты user mini-app в DI.
    public static class UserMiniAppRegistration
    {
        // Подключает user mini-app, его connector, state, factory и message handler.
        public static IServiceCollection AddUserMiniApp(this IServiceCollection services)
        {
            services.AddScoped<UserMiniAppState>();
            services.AddScoped<BusinessEntityUserFactory>();
            services.AddScoped<UserMiniAppService>();
            services.AddScoped<UserMiniAppMessageHandler>();
            services.AddScoped<IUserMiniApp, Facade.UserMiniApp>();
            services.AddScoped<IUserConnector, UserConnector>();

            return services;
        }
    }
}
