namespace PoEnhance.App.Features.PriceChecking;

internal static class PriceCheckerRarity
{
    public const string Any = "Any";
    public const string Normal = "Normal";
    public const string Magic = "Magic";
    public const string Rare = "Rare";
    public const string Unique = "Unique";

    public static bool IsOrdinary(string? rarity)
    {
        return Equals(rarity, Normal) || Equals(rarity, Magic) || Equals(rarity, Rare);
    }

    public static string DisplayValue(string? rarity)
    {
        return TryNormalizeEditable(rarity, out var normalized)
            ? normalized
            : string.Equals(rarity?.Trim(), Unique, StringComparison.OrdinalIgnoreCase)
                ? Unique
                : Any;
    }

    public static bool TryNormalizeEditable(string? rarity, out string normalized)
    {
        foreach (var option in new[] { Any, Normal, Magic, Rare })
        {
            if (Equals(rarity, option))
            {
                normalized = option;
                return true;
            }
        }

        normalized = string.Empty;
        return false;
    }

    private static bool Equals(string? rarity, string expected)
    {
        return string.Equals(rarity?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }
}
