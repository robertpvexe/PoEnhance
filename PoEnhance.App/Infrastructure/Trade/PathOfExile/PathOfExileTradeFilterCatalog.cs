namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeFilterCatalog
{
    private readonly Dictionary<string, PathOfExileTradeFilterOption> categoriesByText;
    private readonly Dictionary<string, PathOfExileTradeFilterOption> categoriesById;

    public PathOfExileTradeFilterCatalog(
        IEnumerable<PathOfExileTradeFilterOption> categoryOptions,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic>? diagnostics = null,
        IEnumerable<PathOfExileTradeNumericFilterDefinition>? numericFilterDefinitions = null)
    {
        ArgumentNullException.ThrowIfNull(categoryOptions);

        CategoryOptions = categoryOptions
            .OrderBy(option => option.ProviderOrder)
            .ToArray();
        NumericFilterDefinitions = (numericFilterDefinitions ?? [])
            .OrderBy(definition => definition.GroupProviderOrder)
            .ThenBy(definition => definition.ProviderOrder)
            .ToArray();
        Diagnostics = diagnostics ?? [];

        categoriesByText = new Dictionary<string, PathOfExileTradeFilterOption>(StringComparer.OrdinalIgnoreCase);
        categoriesById = new Dictionary<string, PathOfExileTradeFilterOption>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in CategoryOptions)
        {
            if (!string.IsNullOrWhiteSpace(option.Text) &&
                !categoriesByText.ContainsKey(option.Text))
            {
                categoriesByText.Add(option.Text, option);
            }

            if (!string.IsNullOrWhiteSpace(option.Id) &&
                !categoriesById.ContainsKey(option.Id))
            {
                categoriesById.Add(option.Id, option);
            }
        }
    }

    public IReadOnlyList<PathOfExileTradeFilterOption> CategoryOptions { get; }

    public IReadOnlyList<PathOfExileTradeNumericFilterDefinition> NumericFilterDefinitions { get; }

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> Diagnostics { get; }

    public bool TryFindCategoryOption(
        string? category,
        out PathOfExileTradeFilterOption option)
    {
        var trimmed = category?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            option = null!;
            return false;
        }

        foreach (var candidate in ProviderCategoryTextCandidates(trimmed))
        {
            if (categoriesByText.TryGetValue(candidate, out var foundByText))
            {
                option = foundByText;
                return true;
            }
        }

        if (categoriesById.TryGetValue(trimmed, out var foundById))
        {
            option = foundById;
            return true;
        }

        option = null!;
        return false;
    }

    public bool TryGetCategoryDisplayLabel(
        string? category,
        out string displayLabel)
    {
        if (TryFindCategoryOption(category, out var option) &&
            !string.IsNullOrWhiteSpace(option.Text))
        {
            displayLabel = option.Text;
            return true;
        }

        displayLabel = null!;
        return false;
    }

    public IReadOnlyList<PathOfExileTradeNumericFilterDefinition> FindNumericFilterDefinitions(
        string? groupId,
        string? filterId)
    {
        var trimmedGroupId = groupId?.Trim();
        var trimmedFilterId = filterId?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedGroupId) ||
            string.IsNullOrWhiteSpace(trimmedFilterId))
        {
            return [];
        }

        return NumericFilterDefinitions
            .Where(definition =>
                string.Equals(definition.GroupId, trimmedGroupId, StringComparison.Ordinal) &&
                string.Equals(definition.FilterId, trimmedFilterId, StringComparison.Ordinal))
            .ToArray();
    }

    private static IEnumerable<string> ProviderCategoryTextCandidates(string category)
    {
        yield return category;

        if (string.Equals(category, "Jewel", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Base Jewel";
        }

        if (string.Equals(category, "One Hand Axes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "One Hand Axe", StringComparison.OrdinalIgnoreCase))
        {
            yield return "One-Handed Axe";
        }
    }
}

internal sealed record PathOfExileTradeFilterOption
{
    public required int ProviderOrder { get; init; }

    public required string GroupId { get; init; }

    public required string FilterId { get; init; }

    public required string Id { get; init; }

    public required string Text { get; init; }
}

internal sealed record PathOfExileTradeNumericFilterDefinition
{
    public required int GroupProviderOrder { get; init; }

    public required int ProviderOrder { get; init; }

    public required string GroupId { get; init; }

    public required string GroupTitle { get; init; }

    public bool GroupHidden { get; init; }

    public required string FilterId { get; init; }

    public required string Text { get; init; }

    public bool SupportsMinMax { get; init; }
}
