using System.Text;

namespace PoEnhance.Core.Items.GameData;

public enum CanonicalItemClassResolutionStatus
{
    Unsupported = 0,
    Exact = 1,
    Alias = 2,
}

public sealed record CanonicalItemClassIdentity
{
    public string? RawItemClass { get; init; }

    public string? CanonicalItemClass { get; init; }

    public CanonicalItemClassResolutionStatus Status { get; init; }

    public string Diagnostic { get; init; } = string.Empty;

    public bool IsSupported => Status is
        CanonicalItemClassResolutionStatus.Exact or
        CanonicalItemClassResolutionStatus.Alias;
}

public static class CanonicalItemClassIdentityResolver
{
    private static readonly IReadOnlyDictionary<string, string> Aliases = CreateAliases();

    public static CanonicalItemClassIdentity Resolve(string? rawItemClass)
    {
        var raw = rawItemClass?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Unsupported(raw, "The raw item class is empty.");
        }

        var normalized = Normalize(raw);
        if (normalized.Length == 0 || !Aliases.TryGetValue(normalized, out var canonical))
        {
            return Unsupported(
                raw,
                $"Raw item class '{raw}' has no reviewed canonical item-class identity.");
        }

        var status = string.Equals(raw, canonical, StringComparison.Ordinal)
            ? CanonicalItemClassResolutionStatus.Exact
            : CanonicalItemClassResolutionStatus.Alias;
        return new CanonicalItemClassIdentity
        {
            RawItemClass = raw,
            CanonicalItemClass = canonical,
            Status = status,
            Diagnostic =
                $"Raw item class '{raw}' resolved to canonical item class '{canonical}' using reviewed item-class identity.",
        };
    }

    internal static bool HaveEquivalentNormalizedText(string first, string second)
    {
        return string.Equals(Normalize(first), Normalize(second), StringComparison.Ordinal);
    }

    private static CanonicalItemClassIdentity Unsupported(string? raw, string diagnostic)
    {
        return new CanonicalItemClassIdentity
        {
            RawItemClass = raw,
            Status = CanonicalItemClassResolutionStatus.Unsupported,
            Diagnostic = diagnostic,
        };
    }

    private static IReadOnlyDictionary<string, string> CreateAliases()
    {
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);

        Add(aliases, "AbyssJewel", "Abyss Jewel", "Abyss Jewels");
        Add(aliases, "Amulet", "Amulets");
        Add(aliases, "Belt", "Belts");
        Add(aliases, "Body Armour", "Body Armours");
        Add(aliases, "Boots");
        Add(aliases, "Bow", "Bows");
        Add(aliases, "Claw", "Claws");
        Add(aliases, "Dagger", "Daggers");
        Add(aliases, "Gloves");
        Add(aliases, "Helmet", "Helmets");
        Add(aliases, "Jewel", "Jewels");
        Add(aliases, "One Hand Axe", "One Hand Axes", "One-Hand Axe", "One-Hand Axes", "One-Handed Axe");
        Add(aliases, "One Hand Mace", "One Hand Maces", "One-Hand Mace", "One-Hand Maces");
        Add(aliases, "One Hand Sword", "One Hand Swords", "One-Hand Sword", "One-Hand Swords");
        Add(aliases, "Quiver", "Quivers");
        Add(aliases, "Ring", "Rings");
        Add(aliases, "Rune Dagger", "Rune Daggers");
        Add(aliases, "Sceptre", "Sceptres");
        Add(aliases, "Shield", "Shields");
        Add(aliases, "Staff", "Staves");
        Add(
            aliases,
            "Thrusting One Hand Sword",
            "Thrusting One Hand Swords",
            "Thrusting One-Hand Sword",
            "Thrusting One-Hand Swords",
            "Rapier",
            "Rapiers");
        Add(aliases, "Two Hand Axe", "Two Hand Axes", "Two-Hand Axe", "Two-Hand Axes");
        Add(aliases, "Two Hand Mace", "Two Hand Maces", "Two-Hand Mace", "Two-Hand Maces");
        Add(aliases, "Two Hand Sword", "Two Hand Swords", "Two-Hand Sword", "Two-Hand Swords");
        Add(aliases, "Wand", "Wands");
        Add(aliases, "Warstaff", "Warstaves", "War Staff", "War Staves");

        // Existing non-equipment compatibility retained by the shared identity mechanism.
        Add(aliases, "HybridFlask", "Hybrid Flask", "Hybrid Flasks");
        Add(aliases, "IncubatorStackable", "Incubator", "Incubators");
        Add(aliases, "LifeFlask", "Life Flask", "Life Flasks");
        Add(aliases, "ManaFlask", "Mana Flask", "Mana Flasks");
        Add(aliases, "Map", "Maps");
        Add(aliases, "StackableCurrency", "Stackable Currency", "Currency");
        Add(aliases, "UtilityFlask", "Utility Flask", "Utility Flasks");

        return aliases;
    }

    private static void Add(
        IDictionary<string, string> aliases,
        string canonical,
        params string[] additionalAliases)
    {
        foreach (var alias in additionalAliases.Prepend(canonical))
        {
            var normalized = Normalize(alias);
            if (aliases.TryGetValue(normalized, out var existing) &&
                !string.Equals(existing, canonical, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Item-class alias '{alias}' collides between '{existing}' and '{canonical}'.");
            }

            aliases[normalized] = canonical;
        }
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSeparator = false;
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = builder.Length > 0;
            }
        }

        return builder.ToString();
    }
}
