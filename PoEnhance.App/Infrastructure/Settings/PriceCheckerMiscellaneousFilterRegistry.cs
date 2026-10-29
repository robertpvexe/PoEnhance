namespace PoEnhance.App.Infrastructure.Settings;

/// <summary>
/// Stable registry of Miscellaneous Panel 2 groups currently implemented in the Price Checker.
/// Permanent groups always appear in the popup; optional groups honor saved visibility.
/// </summary>
public static class PriceCheckerMiscellaneousFilterRegistry
{
    public const string FracturedItem = "fractured-item";
    public const string ItemLevel = "item-level";
    public const string Quality = "quality";
    public const string Sockets = "sockets";
    public const string Links = "links";
    public const string BasePercentile = "base-percentile";
    public const string SynthesisedItem = "synthesised-item";
    public const string Split = "split";
    public const string Crafted = "crafted";
    public const string Veiled = "veiled";
    public const string Foreseeing = "foreseeing";
    public const string CrucibleItem = "crucible-item";
    public const string Foulborn = "foulborn";
    public const string Vestigial = "vestigial";
    public const string MemoryStrands = "memory-strands";
    public const string Intangibility = "intangibility";

    internal static IReadOnlyList<PriceCheckerMiscellaneousFilterDefinition> Definitions { get; } =
    [
        new(
            FracturedItem,
            "Fractured Item",
            "Cycles Any, Yes, or No for the fractured-item state filter.",
            CanToggleVisibility: true),
        new(
            ItemLevel,
            "Item Level",
            "Filters results by the item level value entered in the Price Checker.",
            CanToggleVisibility: false),
        new(
            Quality,
            "Quality",
            "Filters results by the quality value entered in the Price Checker.",
            CanToggleVisibility: false),
        new(
            Sockets,
            "Sockets",
            "Socket count together with optional red, green, blue, and white colour counts.",
            CanToggleVisibility: false),
        new(
            Links,
            "Links",
            "Link count together with optional red, green, blue, and white colour counts.",
            CanToggleVisibility: false),
        new(
            BasePercentile,
            "Base %",
            "Optional base-percentile filter for applicable item bases.",
            CanToggleVisibility: true),
        new(
            SynthesisedItem,
            "Synthesised Item",
            "Cycles Any, Yes, or No for the synthesised-item state filter.",
            CanToggleVisibility: true),
        new(
            Split,
            "Split",
            "Cycles Any, Yes, or No for the split-item state filter.",
            CanToggleVisibility: true),
        new(
            Crafted,
            "Crafted",
            "Cycles Any, Yes, or No for the crafted-mod status filter.",
            CanToggleVisibility: true),
        new(
            Veiled,
            "Veiled",
            "Cycles Any, Yes, or No for items with an unrevealed veiled modifier.",
            CanToggleVisibility: true),
        new(
            Foreseeing,
            "Foreseeing",
            "Cycles Any, Yes, or No for the Hinekora's Lock foreseeing state.",
            CanToggleVisibility: true),
        new(
            CrucibleItem,
            "Crucible Item",
            "Cycles Any, Yes, or No for the Crucible item state filter.",
            CanToggleVisibility: true),
        new(
            Foulborn,
            "Foulborn",
            "Cycles Any, Yes, or No for the Foulborn (mutated) state filter.",
            CanToggleVisibility: true),
        new(
            Vestigial,
            "Vestigial",
            "Cycles Any, Yes, or No for the Vestigial state filter.",
            CanToggleVisibility: true),
        new(
            MemoryStrands,
            "Memory Strands",
            "Optional Min lower bound for Memory Strands on the copied item.",
            CanToggleVisibility: true),
        new(
            Intangibility,
            "Intangibility",
            "Optional Max upper bound for Intangibility percentage on the copied item.",
            CanToggleVisibility: true),
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

    internal static bool TryGetDefinition(
        string key,
        out PriceCheckerMiscellaneousFilterDefinition definition)
    {
        definition = Definitions.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, key, StringComparison.Ordinal))!;
        return definition is not null;
    }

    public static bool CanToggleVisibility(string key)
    {
        return TryGetDefinition(key, out var definition) && definition.CanToggleVisibility;
    }

    public static bool IsPreferenceVisibleInPopup(
        PriceCheckerMiscellaneousFilterPreference preference)
    {
        if (!TryGetDefinition(preference.Key, out var definition))
        {
            return false;
        }

        return !definition.CanToggleVisibility || preference.IsVisible;
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
                    !seen.Add(key) ||
                    !TryGetDefinition(key, out var definition))
                {
                    continue;
                }

                ordered.Add(new PriceCheckerMiscellaneousFilterPreference(
                    key,
                    definition.CanToggleVisibility ? entry.IsVisible : true,
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
