namespace BusinessEntity.MiniApps.MediaServerMiniApp.Contracts;

// Прикладной сервис общего мультимедиа-хранилища.
public interface IMediaServerService
{
    Task<IReadOnlyList<MediaVideoInfo>> GetVideosAsync(CancellationToken cancellationToken = default);

    Task<MediaVideoInfo?> GetVideoAsync(Guid videoId, CancellationToken cancellationToken = default);

    Task<MediaVideoInfo> UploadVideoAsync(
        Stream content,
        string fileName,
        string? contentType,
        long? length,
        CancellationToken cancellationToken = default);

    Task<MediaVideoInfo> RenameVideoAsync(Guid videoId, string displayName, CancellationToken cancellationToken = default);

    Task DeleteVideoAsync(Guid videoId, CancellationToken cancellationToken = default);

    Task<MediaVideoFileContent?> GetVideoFileAsync(Guid videoId, CancellationToken cancellationToken = default);
}
