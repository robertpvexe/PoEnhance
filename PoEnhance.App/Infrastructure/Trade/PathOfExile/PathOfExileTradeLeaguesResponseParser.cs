using System.Text.Json;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeLeaguesResponseParser
{
    public PathOfExileTradeLeaguesResponseParseResult ParseLeaguesResponse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Failure(
                PathOfExileTradeLeaguesDiagnosticCodes.MalformedJson,
                "The Trade leagues response body is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("result", out var result) ||
                result.ValueKind != JsonValueKind.Array)
            {
                return Failure(
                    PathOfExileTradeLeaguesDiagnosticCodes.MissingResultCollection,
                    "A Trade leagues response requires a result collection.");
            }

            var diagnostics = new List<PathOfExileTradeQueryDiagnostic>();
            var entries = new List<PathOfExileTradeLeagueEntry>();
            var index = 0;
            foreach (var row in result.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                {
                    diagnostics.Add(Diagnostic(
                        PathOfExileTradeLeaguesDiagnosticCodes.MalformedRow,
                        $"Trade league row at index {index} must be an object."));
                    index++;
                    continue;
                }

                var id = ReadString(row, "id");
                var text = ReadString(row, "text");
                var realm = ReadString(row, "realm");
                if (id is null || text is null || realm is null)
                {
                    var code = id is null
                        ? PathOfExileTradeLeaguesDiagnosticCodes.MissingProviderId
                        : text is null
                            ? PathOfExileTradeLeaguesDiagnosticCodes.MissingDisplayText
                            : PathOfExileTradeLeaguesDiagnosticCodes.MissingRealm;
                    diagnostics.Add(Diagnostic(
                        code,
                        $"Trade league row at index {index} is missing a required provider field."));
                    index++;
                    continue;
                }

                entries.Add(new PathOfExileTradeLeagueEntry(id, text, realm, index));
                index++;
            }

            if (entries.Count == 0)
            {
                diagnostics.Add(Diagnostic(
                    PathOfExileTradeLeaguesDiagnosticCodes.UnusableEmptyCatalog,
                    "The Trade leagues catalog contains no usable rows."));
                return new PathOfExileTradeLeaguesResponseParseResult { Diagnostics = diagnostics };
            }

            return new PathOfExileTradeLeaguesResponseParseResult
            {
                Entries = entries,
                Diagnostics = diagnostics,
            };
        }
        catch (JsonException)
        {
            return Failure(
                PathOfExileTradeLeaguesDiagnosticCodes.MalformedJson,
                "The Trade leagues response body is not valid JSON.");
        }
    }

    private static string? ReadString(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var trimmed = value.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static PathOfExileTradeLeaguesResponseParseResult Failure(string code, string message) =>
        new() { Diagnostics = [Diagnostic(code, message)] };

    private static PathOfExileTradeQueryDiagnostic Diagnostic(string code, string message) =>
        new(code, message);
}
