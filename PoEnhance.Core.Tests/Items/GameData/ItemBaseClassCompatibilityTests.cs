using PoEnhance.Core.Items.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class ItemBaseClassCompatibilityTests
{
    [Theory]
    [InlineData(" Rings ", " ring ")]
    [InlineData("Bows", "Bow")]
    [InlineData("Helmets", "Helmet")]
    [InlineData("Maps", "Map")]
    [InlineData("Shields", "Shield")]
    [InlineData("Incubators", "IncubatorStackable")]
    [InlineData("Utility Flasks", "UtilityFlask")]
    [InlineData("Stackable Currency", "StackableCurrency")]
    [InlineData("Stackable Currency", "Currency")]
    [InlineData("Belts", "Belt")]
    [InlineData("Body Armours", "Body Armour")]
    [InlineData("One Hand Axes", "One Hand Axe")]
    [InlineData("Sceptres", "Sceptre")]
    [InlineData("Two Hand Axes", "Two Hand Axe")]
    [InlineData("Wands", "Wand")]
    [InlineData("Amulets", "Amulet")]
    [InlineData("Jewels", "Jewel")]
    [InlineData("Life Flasks", "LifeFlask")]
    [InlineData("Mana Flasks", "ManaFlask")]
    [InlineData("Hybrid Flasks", "HybridFlask")]
    public void AreCompatible_KnownDisplayAndCatalogClasses_ReturnsTrue(
        string parsedItemClass,
        string catalogItemClass)
    {
        var result = ItemBaseClassCompatibility.AreCompatible(parsedItemClass, catalogItemClass);

        Assert.True(result);
    }

    [Theory]
    [InlineData("Amulets", "Amulet", "normalized")]
    [InlineData("Belts", "Belt", "normalized")]
    [InlineData("Body Armours", "Body Armour", "normalized")]
    [InlineData("Boots", "Boots", "exact")]
    [InlineData("Bows", "Bow", "normalized")]
    [InlineData("Gloves", "Gloves", "exact")]
    [InlineData("Helmets", "Helmet", "normalized")]
    [InlineData("Jewels", "Jewel", "normalized")]
    [InlineData("One Hand Axes", "One Hand Axe", "normalized")]
    [InlineData("Rings", "Ring", "normalized")]
    [InlineData("Sceptres", "Sceptre", "normalized")]
    [InlineData("Shields", "Shield", "normalized")]
    [InlineData("Wands", "Wand", "normalized")]
    public void AreCompatible_OrdinaryCorpusClipboardClasses_MapToCatalogClasses(
        string clipboardClass,
        string canonicalCatalogClass,
        string compatibilityKind)
    {
        var result = ItemBaseClassCompatibility.AreCompatible(clipboardClass, canonicalCatalogClass);

        Assert.True(result);
        Assert.Contains(compatibilityKind, new[] { "exact", "normalized" });
    }

    [Theory]
    [InlineData("Utility-Flasks", "UtilityFlask")]
    [InlineData("Rings", "Rune Dagger")]
    [InlineData("Maps", "MapFragment")]
    [InlineData("Gloves", "Glove")]
    [InlineData("Bows", "Crossbow")]
    [InlineData("Sceptres", "Rune Dagger")]
    [InlineData(null, "Ring")]
    [InlineData("Rings", null)]
    public void AreCompatible_UnsupportedOrFuzzyForms_ReturnsFalse(
        string? parsedItemClass,
        string? catalogItemClass)
    {
        var result = ItemBaseClassCompatibility.AreCompatible(parsedItemClass, catalogItemClass);

        Assert.False(result);
    }
}
