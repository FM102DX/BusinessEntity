using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace BusinessEntity.MiniApps.UserMiniApp.Internal;

// JSON-настройки storage-контура user mini-app.
internal static class UserMiniAppJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };
}
