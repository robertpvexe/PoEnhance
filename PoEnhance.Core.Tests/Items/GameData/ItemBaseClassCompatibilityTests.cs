using PoEnhance.Core.Items.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class ItemBaseClassCompatibilityTests
{
    [Theory]
    [InlineData(" Rings ", " ring ")]
    [InlineData("Bows", "Bow")]
    [InlineData("Helmets", "Helmet")]
    [InlineData("Maps", "Map")]
    [InlineData("Incubators", "IncubatorStackable")]
    [InlineData("Utility Flasks", "UtilityFlask")]
    [InlineData("Stackable Currency", "StackableCurrency")]
    [InlineData("Stackable Currency", "Currency")]
    [InlineData("Belts", "Belt")]
    [InlineData("Body Armours", "Body Armour")]
    [InlineData("Two Hand Axes", "Two Hand Axe")]
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
    [InlineData("Utility-Flasks", "UtilityFlask")]
    [InlineData("Rings", "Rune Dagger")]
    [InlineData("Maps", "MapFragment")]
    [InlineData("Gloves", "Glove")]
    [InlineData("Bows", "Crossbow")]
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
