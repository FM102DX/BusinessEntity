namespace BusinessEntity.MiniApps.MediaServerMiniApp.Contracts;

public enum MediaVideoUploadJobState
{
    Queued = 0,
    Uploading = 1,
    Completed = 2,
    Cancelled = 3,
    Failed = 4
}
