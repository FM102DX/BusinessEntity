namespace BusinessEntity.MiniApps.MediaServerMiniApp.Contracts;

public sealed class MediaVideoUploadJobInfo
{
    public Guid JobId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long? TotalBytes { get; set; }
    public long UploadedBytes { get; set; }
    public MediaVideoUploadJobState State { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid? VideoId { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    public double? ProgressPercent
    {
        get
        {
            if (!TotalBytes.HasValue || TotalBytes.Value <= 0)
            {
                return State == MediaVideoUploadJobState.Completed ? 100 : null;
            }

            var value = UploadedBytes * 100d / TotalBytes.Value;
            return Math.Clamp(value, 0, 100);
        }
    }
}
