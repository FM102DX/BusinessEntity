using BusinessEntity.MiniApps.MediaServerMiniApp.Contracts;
using BusinessEntity.MiniApps.MediaServerMiniApp.Internal;

namespace BusinessEntity.MiniApps.MediaServerMiniApp.Registration;

// Регистрация MediaServerMiniApp в основном DI приложения.
public static class MediaServerMiniAppRegistration
{
    public static IServiceCollection AddMediaServerMiniApp(this IServiceCollection services)
    {
        services.AddScoped<MediaServerService>();
        services.AddScoped<IMediaServerService>(provider => provider.GetRequiredService<MediaServerService>());
        services.AddScoped<IMediaServerMiniApp, Facade.MediaServerMiniApp>();

        return services;
    }
}
