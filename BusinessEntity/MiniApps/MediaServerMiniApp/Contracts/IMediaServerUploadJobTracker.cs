namespace BusinessEntity.MiniApps.MediaServerMiniApp.Contracts;

public interface IMediaServerUploadJobTracker
{
    IReadOnlyList<MediaVideoUploadJobInfo> GetVideoUploadJobs();

    MediaVideoUploadJobInfo? GetVideoUploadJob(Guid jobId);

    bool CancelVideoUploadJob(Guid jobId);
}
