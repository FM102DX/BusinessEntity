using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace BusinessEntity.MiniApps.ActivityMiniApp.Internal;

/// <summary>
/// JSON-настройки payload-ов activity mini-app без Unicode-escaping.
/// </summary>
internal static class ActivityJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };
}
