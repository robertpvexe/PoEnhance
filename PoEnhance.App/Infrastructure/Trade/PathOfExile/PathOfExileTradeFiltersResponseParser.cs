using System.Text.Json;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeFiltersResponseParser
{
    private const string TypeFiltersGroupId = "type_filters";
    private const string CategoryFilterId = "category";

    public PathOfExileTradeFiltersResponseParseResult ParseFiltersResponse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return StructuralFailure(
                PathOfExileTradeFiltersDiagnosticCodes.MalformedJson,
                "The Trade filters response body is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("result", out var resultElement) ||
                resultElement.ValueKind != JsonValueKind.Array)
            {
                return StructuralFailure(
                    PathOfExileTradeFiltersDiagnosticCodes.MissingResultCollection,
                    "A Trade filters response requires a result collection.");
            }

            var diagnostics = new List<PathOfExileTradeQueryDiagnostic>();
            if (!TryFindFilter(resultElement, TypeFiltersGroupId, CategoryFilterId, diagnostics, out var categoryFilter))
            {
                diagnostics.Add(Diagnostic(
                    PathOfExileTradeFiltersDiagnosticCodes.MissingCategoryFilter,
                    "The Trade filters catalog did not include the type_filters/category filter."));
                return PathOfExileTradeFiltersResponseParseResult.Failure(diagnostics.ToArray());
            }

            var categoryOptions = ParseCategoryOptions(categoryFilter, diagnostics).ToArray();
            if (categoryOptions.Length == 0)
            {
                diagnostics.Add(Diagnostic(
                    PathOfExileTradeFiltersDiagnosticCodes.UnusableEmptyCatalog,
                    "The Trade filters catalog contains no usable item category options."));
            }

            var catalog = new PathOfExileTradeFilterCatalog(categoryOptions, diagnostics);
            return PathOfExileTradeFiltersResponseParseResult.Success(catalog, diagnostics);
        }
        catch (JsonException)
        {
            return StructuralFailure(
                PathOfExileTradeFiltersDiagnosticCodes.MalformedJson,
                "The Trade filters response body is not valid JSON.");
        }
    }

    private static bool TryFindFilter(
        JsonElement resultElement,
        string groupId,
        string filterId,
        List<PathOfExileTradeQueryDiagnostic> diagnostics,
        out JsonElement filterElement)
    {
        filterElement = default;
        var groupIndex = 0;
        foreach (var groupElement in resultElement.EnumerateArray())
        {
            if (groupElement.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(Diagnostic(
                    PathOfExileTradeFiltersDiagnosticCodes.MalformedGroup,
                    $"Trade filters group at index {groupIndex} must be an object."));
                groupIndex++;
                continue;
            }

            if (!string.Equals(ReadOptionalString(groupElement, "id"), groupId, StringComparison.Ordinal))
            {
                groupIndex++;
                continue;
            }

            if (!groupElement.TryGetProperty("filters", out var filtersElement) ||
                filtersElement.ValueKind != JsonValueKind.Array)
            {
                diagnostics.Add(Diagnostic(
                    PathOfExileTradeFiltersDiagnosticCodes.MalformedGroup,
                    $"Trade filters group '{groupId}' requires a filters array."));
                return false;
            }

            var filterIndex = 0;
            foreach (var currentFilterElement in filtersElement.EnumerateArray())
            {
                if (currentFilterElement.ValueKind != JsonValueKind.Object)
                {
                    diagnostics.Add(Diagnostic(
                        PathOfExileTradeFiltersDiagnosticCodes.MalformedFilter,
                        $"Trade filter at index {filterIndex} in group '{groupId}' must be an object."));
                    filterIndex++;
                    continue;
                }

                if (string.Equals(ReadOptionalString(currentFilterElement, "id"), filterId, StringComparison.Ordinal))
                {
                    filterElement = currentFilterElement;
                    return true;
                }

                filterIndex++;
            }

            return false;
        }

        return false;
    }

    private static IEnumerable<PathOfExileTradeFilterOption> ParseCategoryOptions(
        JsonElement categoryFilter,
        List<PathOfExileTradeQueryDiagnostic> diagnostics)
    {
        if (!categoryFilter.TryGetProperty("option", out var optionElement) ||
            optionElement.ValueKind != JsonValueKind.Object ||
            !optionElement.TryGetProperty("options", out var optionsElement) ||
            optionsElement.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(Diagnostic(
                PathOfExileTradeFiltersDiagnosticCodes.MissingCategoryOptions,
                "The Trade category filter requires option/options."));
            yield break;
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var providerOrder = 0;
        foreach (var currentOptionElement in optionsElement.EnumerateArray())
        {
            if (currentOptionElement.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(Diagnostic(
                    PathOfExileTradeFiltersDiagnosticCodes.MalformedOption,
                    $"Trade category option at index {providerOrder} must be an object."));
                providerOrder++;
                continue;
            }

            var id = ReadOptionalString(currentOptionElement, "id");
            var text = ReadOptionalString(currentOptionElement, "text");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(text))
            {
                providerOrder++;
                continue;
            }

            if (!seenIds.Add(id))
            {
                diagnostics.Add(Diagnostic(
                    PathOfExileTradeFiltersDiagnosticCodes.DuplicateCategoryOptionId,
                    $"Duplicate Trade category option id '{id}' was observed; exact-id lookup keeps the first entry."));
                providerOrder++;
                continue;
            }

            yield return new PathOfExileTradeFilterOption
            {
                ProviderOrder = providerOrder,
                GroupId = "type_filters",
                FilterId = "category",
                Id = id,
                Text = text,
            };
            providerOrder++;
        }
    }

    private static string? ReadOptionalString(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var element) &&
            element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }

    private static PathOfExileTradeFiltersResponseParseResult StructuralFailure(
        string code,
        string message)
    {
        return PathOfExileTradeFiltersResponseParseResult.Failure(Diagnostic(code, message));
    }

    private static PathOfExileTradeQueryDiagnostic Diagnostic(
        string code,
        string message)
    {
        return new PathOfExileTradeQueryDiagnostic(code, message);
    }
}
