using BusinessEntity.MiniApps.DataProviderMiniApp.Connectors;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Connectors;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;
using BusinessEntity.MiniApps.DataProviderMiniApp.Internal;
using BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.EfPostgres;
using BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.InMemory;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Registration
{
    /// <summary>
    /// Регистрирует все публичные и внутренние компоненты data-provider mini-app в DI.
    /// </summary>
    public static class DataProviderMiniAppRegistration
    {
        /// <summary>
        /// Подключает mini-app хранения данных, его connector, репозитории и message handler.
        /// </summary>
        // Регистрирует все зависимости mini-app в контейнере DI.
        public static IServiceCollection AddDataProviderMiniApp(this IServiceCollection services)
        {
            // EF/Postgres реализацию пока держим выключенной.
            // services.AddSingleton<IAsyncRepository<BusinessEntityDto>, BusinessEntityDtoEfPostgresRepository>();
            // services.AddSingleton<IAsyncRepository<BusinessEntityDataDto>, BusinessEntityDataDtoEfPostgresRepository>();
            // services.AddSingleton<IAsyncRepository<BusinessEntityRelationDto>, BusinessEntityRelationDtoEfPostgresRepository>();

            // Пока используем in-memory репозитории для всех DTO-хранилищ.
            services.AddSingleton<IAsyncRepository<BusinessEntityDto>, BusinessEntityDtoInMemoryRepository>();
            services.AddSingleton<IAsyncRepository<BusinessEntityDataDto>, BusinessEntityDataDtoInMemoryRepository>();
            services.AddSingleton<IAsyncRepository<BusinessEntityRelationDto>, BusinessEntityRelationDtoInMemoryRepository>();
            services.AddScoped<IDataProviderCrudService, DataProviderService>();
            services.AddScoped<DataProviderMessageHandler>();
            services.AddScoped<IDataProviderMiniApp, Facade.DataProviderMiniApp>();
            services.AddScoped<IDataProviderConnector, DataProviderConnector>();

            return services;
        }
    }
}
