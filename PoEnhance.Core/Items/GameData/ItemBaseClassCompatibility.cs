namespace PoEnhance.Core.Items.GameData;

public static class ItemBaseClassCompatibility
{
    private static readonly IReadOnlyDictionary<string, string[]> DisplayToCatalogClasses =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Amulets"] = ["Amulet"],
            ["Belts"] = ["Belt"],
            ["Body Armours"] = ["Body Armour"],
            ["Bows"] = ["Bow"],
            ["Helmets"] = ["Helmet"],
            ["Incubators"] = ["IncubatorStackable"],
            ["Jewels"] = ["Jewel"],
            ["Life Flasks"] = ["LifeFlask"],
            ["Mana Flasks"] = ["ManaFlask"],
            ["Hybrid Flasks"] = ["HybridFlask"],
            ["Maps"] = ["Map"],
            ["Rings"] = ["Ring"],
            ["Stackable Currency"] = ["StackableCurrency", "Currency"],
            ["Two Hand Axes"] = ["Two Hand Axe"],
            ["Utility Flasks"] = ["UtilityFlask"],
        };

    public static bool AreCompatible(string? parsedItemClass, string? catalogItemClass)
    {
        var parsed = parsedItemClass?.Trim();
        var catalog = catalogItemClass?.Trim();

        if (string.IsNullOrEmpty(parsed) || string.IsNullOrEmpty(catalog))
        {
            return false;
        }

        if (string.Equals(parsed, catalog, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return DisplayToCatalogClasses.TryGetValue(parsed, out var catalogClasses)
            && catalogClasses.Contains(catalog, StringComparer.OrdinalIgnoreCase);
    }
}
