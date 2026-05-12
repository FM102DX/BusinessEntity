namespace BusinessEntity.MiniApps.MediaServerMiniApp.Contracts;

// Событие MediaServerMiniApp: видео полностью сохранено и готово к встраиванию/просмотру.
public sealed record MediaVideoUploadedMessage(
    string? ClientUploadToken,
    MediaVideoInfo Video,
    Guid? SpaceId);
