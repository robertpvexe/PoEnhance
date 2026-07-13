using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradeJson
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string SerializeSearchRequest(PathOfExileTradeSearchRequest request)
    {
        return JsonSerializer.Serialize(request, JsonOptions);
    }
}
