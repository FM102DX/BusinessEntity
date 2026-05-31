using BusinessEntity.MiniApps.MediaServerMiniApp.Contracts;
using BusinessEntity.MiniApps.MediaServerMiniApp.Internal;

namespace BusinessEntity.MiniApps.MediaServerMiniApp.Facade;

// Facade mini-app. Пока bus-сообщений нет: сервис вызывается напрямую через DI.
public sealed class MediaServerMiniApp : IMediaServerMiniApp
{
    private readonly MediaServerService _service;
    private bool _initialized;

    public MediaServerMiniApp(MediaServerService service)
    {
        _service = service;
    }

    public void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _service.EnsureStorageInitialized();
        _initialized = true;
    }
}
