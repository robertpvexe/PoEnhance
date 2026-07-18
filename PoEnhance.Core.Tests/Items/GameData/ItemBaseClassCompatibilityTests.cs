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
    [InlineData("Utility-Flasks", "UtilityFlask")]
    [InlineData("Two Hand Swords", "Two Hand Sword")]
    [InlineData("Two-Hand Maces", "Two Hand Mace")]
    [InlineData("One Hand Swords", "One Hand Sword")]
    [InlineData("One-Hand Maces", "One Hand Mace")]
    [InlineData("Staves", "Staff")]
    [InlineData("Warstaves", "Warstaff")]
    [InlineData("Claws", "Claw")]
    [InlineData("Daggers", "Dagger")]
    [InlineData("Rune Daggers", "Rune Dagger")]
    [InlineData("Thrusting One Hand Swords", "Thrusting One Hand Sword")]
    [InlineData("Quivers", "Quiver")]
    [InlineData("Abyss Jewels", "AbyssJewel")]
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

    [Theory]
    [InlineData("Staves", "Warstaff")]
    [InlineData("Daggers", "Rune Dagger")]
    [InlineData("Jewels", "AbyssJewel")]
    [InlineData("One Hand Swords", "Thrusting One Hand Sword")]
    [InlineData("One Hand Maces", "Sceptre")]
    public void AreCompatible_SemanticallyDifferentClasses_DoNotCollide(
        string parsedItemClass,
        string catalogItemClass)
    {
        Assert.False(ItemBaseClassCompatibility.AreCompatible(parsedItemClass, catalogItemClass));
    }

    [Theory]
    [InlineData("  TWO---HAND   SWORDS ", "Two Hand Sword")]
    [InlineData("rune...daggers", "Rune Dagger")]
    [InlineData("Abyss-Jewels", "AbyssJewel")]
    public void Resolve_ReviewedAliases_AreCaseWhitespaceAndPunctuationSafe(
        string rawItemClass,
        string expectedCanonicalItemClass)
    {
        var result = CanonicalItemClassIdentityResolver.Resolve(rawItemClass);

        Assert.True(result.IsSupported);
        Assert.Equal(expectedCanonicalItemClass, result.CanonicalItemClass);
        Assert.Equal(rawItemClass.Trim(), result.RawItemClass);
        Assert.Contains(result.RawItemClass!, result.Diagnostic, StringComparison.Ordinal);
        Assert.Contains(expectedCanonicalItemClass, result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_UnknownFutureClass_RemainsUnsupportedWithoutGuessing()
    {
        var result = CanonicalItemClassIdentityResolver.Resolve("Prototype Widgets");

        Assert.Equal(CanonicalItemClassResolutionStatus.Unsupported, result.Status);
        Assert.Null(result.CanonicalItemClass);
        Assert.Contains("Prototype Widgets", result.Diagnostic, StringComparison.Ordinal);
    }
}
