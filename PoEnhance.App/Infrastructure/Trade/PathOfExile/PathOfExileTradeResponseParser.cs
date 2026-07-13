using System.Globalization;
using System.Text.Json;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeResponseParser
{
    public PathOfExileTradeResponseParseResult ParseSearchResponse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Failure(
                PathOfExileTradeResponseDiagnosticCodes.MalformedJson,
                "The Trade search response body is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out var errorElement))
            {
                return ParseProviderError(errorElement);
            }

            if (!root.TryGetProperty("id", out var idElement) ||
                idElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(idElement.GetString()))
            {
                return Failure(
                    PathOfExileTradeResponseDiagnosticCodes.MissingSearchId,
                    "A successful Trade search response requires an id field.");
            }

            if (!root.TryGetProperty("result", out var resultElement))
            {
                return Failure(
                    PathOfExileTradeResponseDiagnosticCodes.MissingResultCollection,
                    "A successful Trade search response requires a result collection.");
            }

            if (resultElement.ValueKind != JsonValueKind.Array)
            {
                return Failure(
                    PathOfExileTradeResponseDiagnosticCodes.MalformedResultCollection,
                    "The Trade search response result field must be an array.");
            }

            if (!root.TryGetProperty("total", out var totalElement) ||
                totalElement.ValueKind != JsonValueKind.Number ||
                !totalElement.TryGetInt32(out var total))
            {
                return Failure(
                    PathOfExileTradeResponseDiagnosticCodes.MissingTotal,
                    "A successful Trade search response requires an integer total field.");
            }

            var resultIds = new List<string>();
            foreach (var resultIdElement in resultElement.EnumerateArray())
            {
                if (resultIdElement.ValueKind != JsonValueKind.String)
                {
                    return Failure(
                        PathOfExileTradeResponseDiagnosticCodes.MalformedResultCollection,
                        "The Trade search response result collection must contain string identifiers.");
                }

                resultIds.Add(resultIdElement.GetString() ?? string.Empty);
            }

            bool? inexact = null;
            if (root.TryGetProperty("inexact", out var inexactElement) &&
                inexactElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                inexact = inexactElement.GetBoolean();
            }

            return PathOfExileTradeResponseParseResult.Success(
                new PathOfExileTradeSearchResponse
                {
                    Id = idElement.GetString()!,
                    Result = resultIds,
                    Total = total,
                    Inexact = inexact,
                });
        }
        catch (JsonException)
        {
            return Failure(
                PathOfExileTradeResponseDiagnosticCodes.MalformedJson,
                "The Trade search response body is not valid JSON.");
        }
    }

    private static PathOfExileTradeResponseParseResult ParseProviderError(
        JsonElement errorElement)
    {
        if (errorElement.ValueKind != JsonValueKind.Object ||
            !errorElement.TryGetProperty("code", out var codeElement) ||
            !TryReadProviderCode(codeElement, out var code) ||
            !errorElement.TryGetProperty("message", out var messageElement) ||
            messageElement.ValueKind != JsonValueKind.String)
        {
            return Failure(
                PathOfExileTradeResponseDiagnosticCodes.MalformedProviderError,
                "The Trade provider error shape is malformed.");
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

    private static PathOfExileTradeResponseParseResult Failure(
        string code,
        string message)
    {
        return PathOfExileTradeResponseParseResult.Failure(
            new PathOfExileTradeQueryDiagnostic(code, message));
    }
}
