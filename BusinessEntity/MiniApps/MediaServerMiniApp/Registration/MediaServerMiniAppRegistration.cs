using BusinessEntity.MiniApps.MediaServerMiniApp.Contracts;
using BusinessEntity.MiniApps.MediaServerMiniApp.Internal;

namespace BusinessEntity.MiniApps.MediaServerMiniApp.Registration;

// Регистрация MediaServerMiniApp в основном DI приложения.
public static class MediaServerMiniAppRegistration
{
    public static IServiceCollection AddMediaServerMiniApp(this IServiceCollection services)
    {
        services.AddSingleton<MediaServerUploadJobRegistry>();
        services.AddSingleton<IMediaServerUploadJobTracker>(provider => provider.GetRequiredService<MediaServerUploadJobRegistry>());
        services.AddSingleton<MediaServerUploadManager>();
        services.AddSingleton<IMediaServerUploadManager>(provider => provider.GetRequiredService<MediaServerUploadManager>());
        services.AddHostedService(provider => provider.GetRequiredService<MediaServerUploadManager>());
        services.AddScoped<MediaServerService>();
        services.AddScoped<IMediaServerService>(provider => provider.GetRequiredService<MediaServerService>());
        services.AddScoped<IMediaServerMiniApp, Facade.MediaServerMiniApp>();

        return services;
    }
}
