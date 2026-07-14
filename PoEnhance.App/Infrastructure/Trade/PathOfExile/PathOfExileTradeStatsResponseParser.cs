using System.Text.Json;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeStatsResponseParser
{
    public PathOfExileTradeStatsResponseParseResult ParseStatsResponse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return StructuralFailure(
                PathOfExileTradeStatsDiagnosticCodes.MalformedJson,
                "The Trade stats response body is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("result", out var resultElement) ||
                resultElement.ValueKind != JsonValueKind.Array)
            {
                return StructuralFailure(
                    PathOfExileTradeStatsDiagnosticCodes.MissingResultCollection,
                    "A Trade stats response requires a result collection.");
            }

            var diagnostics = new List<PathOfExileTradeQueryDiagnostic>();
            var entries = new List<PathOfExileTradeStatEntry>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var providerOrder = 0;
            var groupIndex = 0;

            foreach (var groupElement in resultElement.EnumerateArray())
            {
                if (groupElement.ValueKind != JsonValueKind.Object)
                {
                    diagnostics.Add(Diagnostic(
                        PathOfExileTradeStatsDiagnosticCodes.MalformedGroup,
                        $"Trade stats group at index {groupIndex} must be an object."));
                    groupIndex++;
                    continue;
                }

                var groupId = ReadOptionalString(groupElement, "id");
                var groupLabel = ReadOptionalString(groupElement, "label");
                if (!groupElement.TryGetProperty("entries", out var entriesElement) ||
                    entriesElement.ValueKind != JsonValueKind.Array)
                {
                    diagnostics.Add(Diagnostic(
                        PathOfExileTradeStatsDiagnosticCodes.MalformedGroup,
                        $"Trade stats group at index {groupIndex} requires an entries array."));
                    groupIndex++;
                    continue;
                }

                var entryIndex = 0;
                foreach (var entryElement in entriesElement.EnumerateArray())
                {
                    if (TryParseEntry(
                        entryElement,
                        providerOrder,
                        groupIndex,
                        entryIndex,
                        groupId,
                        groupLabel,
                        seenIds,
                        diagnostics,
                        out var entry))
                    {
                        entries.Add(entry);
                        providerOrder++;
                    }

                    entryIndex++;
                }

                groupIndex++;
            }

            if (entries.Count == 0)
            {
                diagnostics.Add(Diagnostic(
                    PathOfExileTradeStatsDiagnosticCodes.UnusableEmptyCatalog,
                    "The Trade stats catalog contains no usable entries."));
            }

            var catalog = new PathOfExileTradeStatCatalog(entries, diagnostics);
            return PathOfExileTradeStatsResponseParseResult.Success(catalog, diagnostics);
        }
        catch (JsonException)
        {
            return StructuralFailure(
                PathOfExileTradeStatsDiagnosticCodes.MalformedJson,
                "The Trade stats response body is not valid JSON.");
        }
    }

    private static bool TryParseEntry(
        JsonElement entryElement,
        int providerOrder,
        int groupIndex,
        int entryIndex,
        string? groupId,
        string? groupLabel,
        HashSet<string> seenIds,
        List<PathOfExileTradeQueryDiagnostic> diagnostics,
        out PathOfExileTradeStatEntry entry)
    {
        entry = null!;
        if (entryElement.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(MalformedEntry(
                groupIndex,
                entryIndex,
                "A Trade stats entry must be an object."));
            return false;
        }

        if (!TryReadRequiredString(entryElement, "id", out var id))
        {
            diagnostics.Add(MalformedEntry(
                groupIndex,
                entryIndex,
                "A Trade stats entry requires a stat id.",
                PathOfExileTradeStatsDiagnosticCodes.MissingStatId));
            return false;
        }

        if (!TryReadRequiredString(entryElement, "text", out var text))
        {
            diagnostics.Add(MalformedEntry(
                groupIndex,
                entryIndex,
                "A Trade stats entry requires template text.",
                PathOfExileTradeStatsDiagnosticCodes.MissingTemplateText));
            return false;
        }

        if (!seenIds.Add(id))
        {
            diagnostics.Add(MalformedEntry(
                groupIndex,
                entryIndex,
                $"Duplicate Trade stats id '{id}' was observed; exact-id lookup keeps the first entry.",
                PathOfExileTradeStatsDiagnosticCodes.DuplicateStatId));
        }

        entry = new PathOfExileTradeStatEntry
        {
            ProviderOrder = providerOrder,
            GroupId = groupId,
            GroupLabel = groupLabel,
            Id = id,
            Text = text,
            Type = ReadOptionalString(entryElement, "type"),
            OptionMetadata = ReadOptionMetadata(entryElement),
        };
        return true;
    }

    private static IReadOnlyDictionary<string, string> ReadOptionMetadata(JsonElement entryElement)
    {
        if (!entryElement.TryGetProperty("option", out var optionElement) ||
            optionElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in optionElement.EnumerateObject())
        {
            var value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => null,
            };

            if (value is not null)
            {
                values[property.Name] = value;
            }
        }

        return values;
    }

    private static string? ReadOptionalString(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var element) &&
            element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }

    private static bool TryReadRequiredString(
        JsonElement parent,
        string propertyName,
        out string value)
    {
        if (parent.TryGetProperty(propertyName, out var element) &&
            element.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(element.GetString()))
        {
            value = element.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static PathOfExileTradeStatsResponseParseResult StructuralFailure(
        string code,
        string message)
    {
        return PathOfExileTradeStatsResponseParseResult.Failure(Diagnostic(code, message));
    }

    private static PathOfExileTradeQueryDiagnostic MalformedEntry(
        int groupIndex,
        int entryIndex,
        string message,
        string code = PathOfExileTradeStatsDiagnosticCodes.MalformedEntry)
    {
        return Diagnostic(code, $"{message} Group index: {groupIndex}. Entry index: {entryIndex}.");
    }

    private static PathOfExileTradeQueryDiagnostic Diagnostic(
        string code,
        string message)
    {
        return new PathOfExileTradeQueryDiagnostic(code, message);
    }
}
