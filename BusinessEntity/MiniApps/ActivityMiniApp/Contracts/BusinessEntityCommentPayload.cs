namespace BusinessEntity.MiniApps.ActivityMiniApp.Contracts;

/// <summary>
/// Хранимый JSON payload комментария к BusinessEntity.
/// </summary>
public sealed class BusinessEntityCommentPayload
{
    public int SchemaVersion { get; set; } = 1;
    public string Kind { get; set; } = "BusinessEntityComment";
    public string Text { get; set; } = string.Empty;
    public string Format { get; set; } = "plainText";
    public Guid? AuthorUserId { get; set; }
    public string AuthorDisplayName { get; set; } = string.Empty;
}
