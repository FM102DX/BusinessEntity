namespace BusinessEntity.MiniApps.MediaServerMiniApp.Contracts;

// Физический файл видео для range-enabled отдачи через контроллер.
public sealed class MediaVideoFileContent
{
    public string PhysicalPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
}
