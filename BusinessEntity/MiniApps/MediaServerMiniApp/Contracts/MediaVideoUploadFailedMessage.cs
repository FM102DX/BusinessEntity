namespace BusinessEntity.MiniApps.MediaServerMiniApp.Contracts;

// Событие MediaServerMiniApp: video upload job завершился ошибкой до создания видео.
public sealed record MediaVideoUploadFailedMessage(
    string? ClientUploadToken,
    string ErrorMessage,
    Guid? SpaceId);
