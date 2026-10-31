using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeItemStateCriteriaFactoryTests
{
    private readonly ItemTextParser parser = new();

    [Fact]
    public void CreateRequestedDefaults_PositiveMirroredBecomesYes()
    {
        var mirrored = parser.Parse("""
Item Class: Rings
Rarity: Rare
Mirror Band
Iron Ring
--------
Item Level: 84
--------
Mirrored
""");
        var criteria = TradeItemStateCriteriaFactory.CreateRequestedDefaults(mirrored);

        Assert.Equal(TradeTriState.Yes, criteria.Mirrored);
        Assert.Equal(TradeTriState.Any, criteria.Corrupted);
        Assert.Equal(TradeTriState.Any, criteria.Identified);
        Assert.Equal(TradeTriState.Any, criteria.Fractured);
        Assert.Equal(TradeTriState.Any, criteria.Foulborn);
        AssertAllUnrelatedPanel2StatesAreAny(criteria);
        Assert.True(mirrored.IsMirrored);
    }

    [Fact]
    public void CreateRequestedDefaults_FracturedItemMarkerBecomesYes()
    {
        var item = parser.Parse("""
Item Class: Rings
Rarity: Rare
Fracture Band
Iron Ring
--------
Item Level: 84
--------
{ Fractured Suffix Modifier "of the Whelpling" (Tier: 8) — Elemental, Fire, Resistance }
+6(6-11)% to Fire Resistance
--------
Fractured Item
""");

        var criteria = TradeItemStateCriteriaFactory.CreateRequestedDefaults(item);

        Assert.True(item.IsFractured);
        Assert.Equal(TradeTriState.Yes, criteria.Fractured);
        Assert.Equal(TradeTriState.Any, criteria.Mirrored);
        Assert.Equal(TradeTriState.Any, criteria.Foulborn);
        Assert.Equal(TradeTriState.Any, criteria.Corrupted);
        Assert.Equal(TradeTriState.Any, criteria.Identified);
        AssertAllUnrelatedPanel2StatesAreAny(criteria);
    }

    [Fact]
    public void CreateRequestedDefaults_FracturedModifierWithoutTrailingMarkerStillBecomesYes()
    {
        var item = parser.Parse("""
Item Class: Bows
Rarity: Rare
Death Twine
Thicket Bow
--------
Item Level: 84
--------
{ Fractured Prefix Modifier "Tempered" (Tier: 3) — Damage, Physical, Attack }
80(75-89)% increased Physical Damage
{ Fractured Suffix Modifier "of Venom" (Tier: 4) — Attack, Chaos }
30(25-35)% chance to Poison on Hit
""");

        var criteria = TradeItemStateCriteriaFactory.CreateRequestedDefaults(item);

        Assert.True(item.IsFractured);
        Assert.DoesNotContain("Fractured Item", item.ItemStates);
        Assert.Equal(TradeTriState.Yes, criteria.Fractured);
        Assert.Equal(TradeTriState.Any, criteria.Foulborn);
    }

    [Fact]
    public void CreateRequestedDefaults_FoulbornUniqueNameAndModifierBecomeYes()
    {
        var item = parser.Parse("""
Item Class: One Hand Swords
Rarity: Unique
Foulborn Rebuke of the Vaal
Vaal Blade
--------
Item Level: 83
--------
{ Unique Modifier — Damage, Physical, Attack }
Adds 68(49-98) to 120(101-140) Physical Damage
{ Foulborn Unique Modifier — Speed }
38(20-40)% increased Trap Throwing Speed
""");

        var criteria = TradeItemStateCriteriaFactory.CreateRequestedDefaults(item);

        Assert.True(item.IsFoulborn);
        Assert.Equal(TradeTriState.Yes, criteria.Foulborn);
        Assert.Equal(TradeTriState.Any, criteria.Fractured);
        Assert.Equal(TradeTriState.Any, criteria.Mirrored);
        AssertAllUnrelatedPanel2StatesAreAny(criteria);
    }

    [Theory]
    [InlineData("Corrupted")]
    [InlineData("Unidentified")]
    [InlineData("Synthesised Item")]
    [InlineData("Split")]
    [InlineData("Foreseeing")]
    [InlineData("Crucible Item")]
    [InlineData("Vestigial")]
    public void CreateRequestedDefaults_OtherObservedStatesStayRequestedAny(string state)
    {
        var item = parser.Parse($"""
Item Class: Rings
Rarity: Magic
Hale Iron Ring
--------
Item Level: 20
--------
{state}
""");

        var criteria = TradeItemStateCriteriaFactory.CreateRequestedDefaults(item);

        Assert.Equal(TradeTriState.Any, criteria.Mirrored);
        Assert.Equal(TradeTriState.Any, criteria.Corrupted);
        Assert.Equal(TradeTriState.Any, criteria.Identified);
        Assert.Equal(TradeTriState.Any, criteria.Fractured);
        Assert.Equal(TradeTriState.Any, criteria.Foulborn);
        AssertAllUnrelatedPanel2StatesAreAny(criteria);
        Assert.Contains(state, item.ItemStates);
    }

    [Fact]
    public void CreateRequestedDefaults_OrdinaryRareStaysAnyForFoulbornAndFractured()
    {
        var item = parser.Parse("""
Item Class: One Hand Axes
Rarity: Rare
Plain Edge
Reaver Axe
--------
Physical Damage: 38-114
Attacks per Second: 1.20
--------
Item Level: 83
--------
{ Suffix Modifier "of Strength" (Tier: 8) - Attribute }
+10(8-12) to Strength
""");

        var criteria = TradeItemStateCriteriaFactory.CreateRequestedDefaults(item);

        Assert.False(item.IsFractured);
        Assert.False(item.IsFoulborn);
        Assert.Equal(TradeTriState.Any, criteria.Fractured);
        Assert.Equal(TradeTriState.Any, criteria.Foulborn);
        Assert.Equal(TradeTriState.Any, criteria.Mirrored);
    }

    private static void AssertAllUnrelatedPanel2StatesAreAny(TradeItemStateCriteria criteria)
    {
        Assert.Equal(TradeTriState.Any, criteria.Synthesised);
        Assert.Equal(TradeTriState.Any, criteria.Split);
        Assert.Equal(TradeTriState.Any, criteria.Crafted);
        Assert.Equal(TradeTriState.Any, criteria.Veiled);
        Assert.Equal(TradeTriState.Any, criteria.Foreseeing);
        Assert.Equal(TradeTriState.Any, criteria.Crucible);
        Assert.Equal(TradeTriState.Any, criteria.Vestigial);
    }
}
