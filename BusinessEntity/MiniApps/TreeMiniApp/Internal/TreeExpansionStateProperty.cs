using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace BusinessEntity.MiniApps.TreeMiniApp.Internal;

internal sealed class TreeExpansionStateProperty
{
    public string Kind { get; set; } = nameof(TreeExpansionStateProperty);
    public int SchemaVersion { get; set; } = 1;
    public Guid SpaceId { get; set; }
    public List<Guid> CollapsedFolderIds { get; set; } = new();
}

internal static class TreeMiniAppJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };
}
