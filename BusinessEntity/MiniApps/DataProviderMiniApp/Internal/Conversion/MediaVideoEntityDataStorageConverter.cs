using System.Text.Json.Serialization;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.DomainEntities;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal.Conversion;

// Конвертер payload-а видео из общего мультимедиа-хранилища.
internal sealed class MediaVideoEntityDataStorageConverter : EntityDataStorageConverterBase<MediaVideo>
{
    public override BusinessEntityTypeEnum SupportedType => BusinessEntityTypeEnum.MediaVideo;

    public override string SerializePayload(MediaVideo data)
    {
        return SerializeBody(new MediaVideoPayloadBody
        {
            Tag = data.Tag ?? string.Empty,
            FileName = data.FileName ?? string.Empty,
            DisplayName = data.DisplayName ?? string.Empty,
            ContentType = data.ContentType ?? "application/octet-stream",
            OriginalSizeBytes = data.OriginalSizeBytes,
            DurationSeconds = data.DurationSeconds,
            UploadedByUserId = data.UploadedByUserId,
            UploadedDate = data.UploadedDate,
            StorageRelativePath = data.StorageRelativePath ?? string.Empty,
            EmbedUrl = data.EmbedUrl ?? string.Empty
        });
    }

    public override MediaVideo DeserializePayload(string payloadJson)
    {
        var body = DeserializeBody<MediaVideoPayloadBody>(payloadJson);
        return new MediaVideo
        {
            Tag = body.Tag ?? string.Empty,
            FileName = body.FileName ?? string.Empty,
            DisplayName = body.DisplayName ?? string.Empty,
            ContentType = string.IsNullOrWhiteSpace(body.ContentType)
                ? "application/octet-stream"
                : body.ContentType,
            OriginalSizeBytes = body.OriginalSizeBytes,
            DurationSeconds = body.DurationSeconds,
            UploadedByUserId = body.UploadedByUserId,
            UploadedDate = body.UploadedDate == default ? DateTime.UtcNow : body.UploadedDate,
            StorageRelativePath = body.StorageRelativePath ?? string.Empty,
            EmbedUrl = body.EmbedUrl ?? string.Empty
        };
    }

    private sealed class MediaVideoPayloadBody
    {
        [JsonPropertyName("tag")]
        public string? Tag { get; set; }

        [JsonPropertyName("fileName")]
        public string? FileName { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("contentType")]
        public string? ContentType { get; set; }

        [JsonPropertyName("originalSizeBytes")]
        public long OriginalSizeBytes { get; set; }

        [JsonPropertyName("durationSeconds")]
        public double? DurationSeconds { get; set; }

        [JsonPropertyName("uploadedByUserId")]
        public Guid? UploadedByUserId { get; set; }

        [JsonPropertyName("uploadedDate")]
        public DateTime UploadedDate { get; set; }

        [JsonPropertyName("storageRelativePath")]
        public string? StorageRelativePath { get; set; }

        [JsonPropertyName("embedUrl")]
        public string? EmbedUrl { get; set; }
    }
}
