using BusinessEntity.MiniApps.DataProviderMiniApp.Connectors;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Connectors;
using BusinessEntity.MiniApps.DataProviderMiniApp.Internal;
using BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.EfPostgres;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Registration
{
    /// <summary>
    /// Регистрирует все публичные и внутренние компоненты data-provider mini-app в DI.
    /// </summary>
    public static class DataProviderMiniAppRegistration
    {
        /// <summary>
        /// Подключает mini-app хранения данных, его connector, state и message handler.
        /// </summary>
        // Регистрирует все зависимости mini-app в контейнере DI.
        public static IServiceCollection AddDataProviderMiniApp(this IServiceCollection services)
        {
            services.AddSingleton<BusinessEntityDtoEfPostgresRepository>();
            services.AddSingleton<BusinessEntityDataDtoEfPostgresRepository>();
            services.AddSingleton<BusinessEntityRelationDtoEfPostgresRepository>();
            services.AddSingleton<DataProviderState>();
            services.AddScoped<IDataProviderCrudService, DataProviderService>();
            services.AddScoped<DataProviderMessageHandler>();
            services.AddScoped<IDataProviderMiniApp, Facade.DataProviderMiniApp>();
            services.AddScoped<IDataProviderConnector, DataProviderConnector>();

            return services;
        }
    }
}
