using System.Text.Json;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradeProviderErrorParser
{
    public static PathOfExileTradeResponseParseResult Parse(JsonElement errorElement)
    {
        if (errorElement.ValueKind != JsonValueKind.Object ||
            !errorElement.TryGetProperty("code", out var codeElement) ||
            !TryReadProviderCode(codeElement, out var code) ||
            !errorElement.TryGetProperty("message", out var messageElement) ||
            messageElement.ValueKind != JsonValueKind.String)
        {
            return PathOfExileTradeResponseParseResult.Failure(
                new PathOfExileTradeQueryDiagnostic(
                    PathOfExileTradeResponseDiagnosticCodes.MalformedProviderError,
                    "The Trade provider error shape is malformed."));
        }

        return PathOfExileTradeResponseParseResult.ProviderFailure(
            new PathOfExileTradeProviderError
            {
                Code = code,
                Message = messageElement.GetString() ?? string.Empty,
            });
    }

    private static bool TryReadProviderCode(
        JsonElement codeElement,
        out string code)
    {
        if (codeElement.ValueKind == JsonValueKind.String)
        {
            code = codeElement.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(code);
        }

        if (codeElement.ValueKind == JsonValueKind.Number)
        {
            code = codeElement.GetRawText();
            return true;
        }

        code = string.Empty;
        return false;
    }
}
