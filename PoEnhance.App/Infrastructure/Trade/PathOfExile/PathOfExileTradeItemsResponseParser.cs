using System.Text.Json;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeItemsResponseParser
{
    public PathOfExileTradeItemsResponseParseResult ParseItemsResponse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return StructuralFailure(
                PathOfExileTradeItemsDiagnosticCodes.MalformedJson,
                "The Trade items response body is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("result", out var resultElement) ||
                resultElement.ValueKind != JsonValueKind.Array)
            {
                return StructuralFailure(
                    PathOfExileTradeItemsDiagnosticCodes.MissingResultCollection,
                    "A Trade items response requires a result collection.");
            }

            var diagnostics = new List<PathOfExileTradeQueryDiagnostic>();
            var entries = new List<PathOfExileTradeItemEntry>();
            var providerOrder = 0;
            var groupIndex = 0;

            foreach (var groupElement in resultElement.EnumerateArray())
            {
                if (groupElement.ValueKind != JsonValueKind.Object)
                {
                    diagnostics.Add(Diagnostic(
                        PathOfExileTradeItemsDiagnosticCodes.MalformedGroup,
                        $"Trade items group at index {groupIndex} must be an object."));
                    groupIndex++;
                    continue;
                }

                var groupId = ReadOptionalString(groupElement, "id");
                var groupLabel = ReadOptionalString(groupElement, "label");
                if (!groupElement.TryGetProperty("entries", out var entriesElement) ||
                    entriesElement.ValueKind != JsonValueKind.Array)
                {
                    diagnostics.Add(Diagnostic(
                        PathOfExileTradeItemsDiagnosticCodes.MalformedGroup,
                        $"Trade items group at index {groupIndex} requires an entries array."));
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
                    PathOfExileTradeItemsDiagnosticCodes.UnusableEmptyCatalog,
                    "The Trade items catalog contains no usable entries."));
            }

            var catalog = new PathOfExileTradeItemCatalog(entries, diagnostics);
            return PathOfExileTradeItemsResponseParseResult.Success(catalog, diagnostics);
        }
        catch (JsonException)
        {
            return StructuralFailure(
                PathOfExileTradeItemsDiagnosticCodes.MalformedJson,
                "The Trade items response body is not valid JSON.");
        }
    }

    private static bool TryParseEntry(
        JsonElement entryElement,
        int providerOrder,
        int groupIndex,
        int entryIndex,
        string? groupId,
        string? groupLabel,
        List<PathOfExileTradeQueryDiagnostic> diagnostics,
        out PathOfExileTradeItemEntry entry)
    {
        entry = null!;
        if (entryElement.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(MalformedEntry(groupIndex, entryIndex, "A Trade item entry must be an object."));
            return false;
        }

        if (!TryReadRequiredString(entryElement, "type", out var type))
        {
            diagnostics.Add(MalformedEntry(
                groupIndex,
                entryIndex,
                "A Trade item entry requires a type.",
                PathOfExileTradeItemsDiagnosticCodes.MissingItemType));
            return false;
        }

        entry = new PathOfExileTradeItemEntry
        {
            ProviderOrder = providerOrder,
            GroupId = groupId,
            GroupLabel = groupLabel,
            Name = ReadOptionalString(entryElement, "name"),
            Type = type,
            IsUnique = ReadUniqueFlag(entryElement),
        };
        return true;
    }

    private static bool ReadUniqueFlag(JsonElement entryElement)
    {
        if (!entryElement.TryGetProperty("flags", out var flagsElement) ||
            flagsElement.ValueKind != JsonValueKind.Object ||
            !flagsElement.TryGetProperty("unique", out var uniqueElement))
        {
            return false;
        }

        return uniqueElement.ValueKind == JsonValueKind.True ||
            uniqueElement.ValueKind == JsonValueKind.String &&
            bool.TryParse(uniqueElement.GetString(), out var parsed) &&
            parsed;
    }

    private static string? ReadOptionalString(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var element) &&
            element.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(element.GetString())
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

    private static PathOfExileTradeItemsResponseParseResult StructuralFailure(
        string code,
        string message)
    {
        return PathOfExileTradeItemsResponseParseResult.Failure(Diagnostic(code, message));
    }

    private static PathOfExileTradeQueryDiagnostic MalformedEntry(
        int groupIndex,
        int entryIndex,
        string message,
        string code = PathOfExileTradeItemsDiagnosticCodes.MalformedEntry)
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
