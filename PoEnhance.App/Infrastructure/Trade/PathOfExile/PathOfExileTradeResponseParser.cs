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
                return PathOfExileTradeProviderErrorParser.Parse(errorElement);
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

    private static PathOfExileTradeResponseParseResult Failure(
        string code,
        string message)
    {
        return PathOfExileTradeResponseParseResult.Failure(
            new PathOfExileTradeQueryDiagnostic(code, message));
    }
}
