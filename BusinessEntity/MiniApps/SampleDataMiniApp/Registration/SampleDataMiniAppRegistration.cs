using BusinessEntity.MiniApps.SampleDataMiniApp.Contracts;
using BusinessEntity.MiniApps.SampleDataMiniApp.Internal;

namespace BusinessEntity.MiniApps.SampleDataMiniApp.Registration
{
    // Регистрирует все публичные и внутренние компоненты mini-app тестовой заливки в DI.
    public static class SampleDataMiniAppRegistration
    {
        // Подключает mini-app тестовой заливки и все его внутренние зависимости.
        public static IServiceCollection AddSampleDataMiniApp(this IServiceCollection services)
        {
            services.AddScoped<ISampleDataService, SampleDataService>();
            services.AddScoped<IDataFillLineProvider, DataFillLineProvider>();
            services.AddScoped<ISampleDataMiniApp, Facade.SampleDataMiniApp>();

            return services;
        }
    }
}
