namespace BusinessEntity.MiniApps.MediaServerMiniApp.Contracts;

public enum MediaVideoUploadJobState
{
    Queued = 0,
    Uploading = 1,
    Processing = 2,
    Completed = 3,
    Cancelled = 4,
    Failed = 5
}
