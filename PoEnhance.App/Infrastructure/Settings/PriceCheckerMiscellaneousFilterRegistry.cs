namespace PoEnhance.App.Infrastructure.Settings;

/// <summary>
/// Stable registry of Miscellaneous Panel 2 groups currently implemented in the Price Checker.
/// Display order matches the existing popup layout until UI 04 consumes preferences.
/// </summary>
internal static class PriceCheckerMiscellaneousFilterRegistry
{
    public const string FracturedItem = "fractured-item";
    public const string ItemLevel = "item-level";
    public const string Quality = "quality";
    public const string Sockets = "sockets";
    public const string Links = "links";
    public const string BasePercentile = "base-percentile";

    public static IReadOnlyList<PriceCheckerMiscellaneousFilterDefinition> Definitions { get; } =
    [
        new(
            FracturedItem,
            "Fractured Item",
            "Cycles Any, Yes, or No for the fractured-item state filter."),
        new(
            ItemLevel,
            "Item Level",
            "Filters results by the item level value entered in the Price Checker."),
        new(
            Quality,
            "Quality",
            "Filters results by the quality value entered in the Price Checker."),
        new(
            Sockets,
            "Sockets",
            "Socket count together with optional red, green, blue, and white colour counts."),
        new(
            Links,
            "Links",
            "Link count together with optional red, green, blue, and white colour counts."),
        new(
            BasePercentile,
            "Base Percentile",
            "Optional base-percentile filter for applicable item bases."),
    ];

    public static IReadOnlyList<PriceCheckerMiscellaneousFilterPreference> CreateDefaults()
    {
        return Definitions
            .Select((definition, index) => new PriceCheckerMiscellaneousFilterPreference(
                definition.Key,
                IsVisible: true,
                Order: index))
            .ToArray();
    }

    public static bool TryGetDefinition(
        string key,
        out PriceCheckerMiscellaneousFilterDefinition definition)
    {
        definition = Definitions.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, key, StringComparison.Ordinal))!;
        return definition is not null;
    }

    public static IReadOnlyList<PriceCheckerMiscellaneousFilterPreference> MergeStoredPreferences(
        IEnumerable<PriceCheckerMiscellaneousFilterPreference>? stored)
    {
        var knownKeys = Definitions
            .Select(definition => definition.Key)
            .ToHashSet(StringComparer.Ordinal);
        var ordered = new List<PriceCheckerMiscellaneousFilterPreference>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (stored is not null)
        {
            foreach (var entry in stored
                .Where(candidate => candidate is not null)
                .OrderBy(candidate => candidate.Order)
                .ThenBy(candidate => candidate.Key, StringComparer.Ordinal))
            {
                var key = entry.Key?.Trim();
                if (string.IsNullOrWhiteSpace(key) ||
                    !knownKeys.Contains(key) ||
                    !seen.Add(key))
                {
                    continue;
                }

                ordered.Add(new PriceCheckerMiscellaneousFilterPreference(
                    key,
                    entry.IsVisible,
                    ordered.Count));
            }
        }

        foreach (var definition in Definitions)
        {
            if (!seen.Add(definition.Key))
            {
                continue;
            }

            ordered.Add(new PriceCheckerMiscellaneousFilterPreference(
                definition.Key,
                IsVisible: true,
                Order: ordered.Count));
        }

        return ordered
            .Select((preference, index) => preference with { Order = index })
            .ToArray();
    }
}
