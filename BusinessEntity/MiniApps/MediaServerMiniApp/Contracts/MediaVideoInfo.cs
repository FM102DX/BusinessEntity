namespace BusinessEntity.MiniApps.MediaServerMiniApp.Contracts;

// DTO для UI и API общего видео-хранилища.
public sealed class MediaVideoInfo
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long OriginalSizeBytes { get; set; }
    public double? DurationSeconds { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public DateTime UploadedDate { get; set; }
    public string EmbedUrl { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}
