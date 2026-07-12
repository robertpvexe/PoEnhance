using System.Collections.ObjectModel;
using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed record ItemModifierEligibilityContext
{
    private static readonly IReadOnlyDictionary<string, string> InfluenceTagSuffixes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Shaper Item"] = "shaper",
            ["Elder Item"] = "elder",
            ["Crusader Item"] = "adjudicator",
            ["Redeemer Item"] = "eyrie",
            ["Hunter Item"] = "basilisk",
            ["Warlord Item"] = "crusader",
        };

    private static readonly HashSet<string> InfluenceTagBasePrefixes =
    [
        "2h_axe",
        "2h_mace",
        "2h_sword",
        "amulet",
        "axe",
        "belt",
        "body_armour",
        "boots",
        "bow",
        "claw",
        "dagger",
        "gloves",
        "helmet",
        "mace",
        "quiver",
        "ring",
        "rune_dagger",
        "sceptre",
        "shield",
        "staff",
        "sword",
        "wand",
        "warstaff",
    ];

    public required ItemBaseRecord ItemBase { get; init; }

    public IReadOnlyList<string> DynamicTags { get; init; } = [];

    public IReadOnlyList<string> TraditionalInfluences { get; init; } = [];

    public IReadOnlyList<string> Diagnostics { get; init; } = [];

    public static ItemModifierEligibilityContext ForItemBase(ItemBaseRecord itemBase)
    {
        ArgumentNullException.ThrowIfNull(itemBase);

        return new ItemModifierEligibilityContext
        {
            ItemBase = itemBase,
        };
    }

    public static ItemModifierEligibilityContext Create(
        ItemBaseRecord itemBase,
        IReadOnlyList<string> traditionalInfluences)
    {
        ArgumentNullException.ThrowIfNull(itemBase);
        ArgumentNullException.ThrowIfNull(traditionalInfluences);

        var dynamicTags = new List<string>();
        var diagnostics = new List<string>();
        var normalizedInfluences = new List<string>();
        var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var influence in traditionalInfluences)
        {
            var normalizedInfluence = Normalize(influence);
            if (normalizedInfluence is null)
            {
                continue;
            }

            normalizedInfluences.Add(normalizedInfluence);
            if (!InfluenceTagSuffixes.TryGetValue(normalizedInfluence, out var suffix))
            {
                diagnostics.Add($"Unsupported traditional influence: {normalizedInfluence}");
                continue;
            }

            foreach (var baseTag in itemBase.Tags)
            {
                var normalizedBaseTag = Normalize(baseTag);
                if (normalizedBaseTag is null || !InfluenceTagBasePrefixes.Contains(normalizedBaseTag))
                {
                    continue;
                }

                var dynamicTag = $"{normalizedBaseTag}_{suffix}";
                if (seenTags.Add(dynamicTag))
                {
                    dynamicTags.Add(dynamicTag);
                }
            }
        }

        return new ItemModifierEligibilityContext
        {
            ItemBase = itemBase,
            DynamicTags = ToReadOnly(dynamicTags),
            TraditionalInfluences = ToReadOnly(normalizedInfluences),
            Diagnostics = ToReadOnly(diagnostics),
        };
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static IReadOnlyList<T> ToReadOnly<T>(IEnumerable<T> values)
    {
        return new ReadOnlyCollection<T>(values.ToArray());
    }
}
