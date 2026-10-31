using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.Core.Tests.Trade;

/// <summary>
/// Stage 03.8A: Foulborn / Fractured Panel-2 tri-state auto-selection from parsed facts.
/// </summary>
public sealed class TradeItemStateStage038ATests
{
    private readonly ItemTextParser parser = new();
    private readonly TradeSearchDraftMapper mapper = new();

    [Fact]
    public void CreateDraft_FoulbornRebuke_InitializesPanelFoulbornYesWithoutAutomaticNoElsewhere()
    {
        // zfortests_v2.txt — Foulborn Rebuke of the Vaal
        var item = parser.Parse("""
Item Class: One Hand Swords
Rarity: Unique
Foulborn Rebuke of the Vaal
Vaal Blade
--------
One Handed Sword
Physical Damage: 114-206 (augmented)
--------
Item Level: 83
--------
{ Unique Modifier — Damage, Physical, Attack }
Adds 68(49-98) to 120(101-140) Physical Damage
{ Foulborn Unique Modifier — Speed }
38(20-40)% increased Trap Throwing Speed
""");

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item));

        Assert.True(item.IsFoulborn);
        Assert.Equal(TradeTriState.Yes, draft.ItemStateCriteria.Foulborn);
        Assert.Equal(TradeTriState.Yes, draft.ItemVariantCriteria.Foulborn);
        Assert.Equal(TradeTriState.Any, draft.ItemStateCriteria.Fractured);
        Assert.Contains(
            item.Modifiers,
            modifier => modifier.UniqueOrigin == ParsedUniqueModifierOrigin.Foulborn);
    }

    [Fact]
    public void CreateDraft_OrdinaryUnique_FoulbornStaysAnyNotNo()
    {
        var item = parser.Parse("""
Item Class: Belts
Rarity: Unique
Mageblood
Heavy Belt
--------
Item Level: 80
--------
{ Unique Modifier — Attribute }
+49(30-50) to Dexterity
""");

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item));

        Assert.False(item.IsFoulborn);
        Assert.Equal(TradeTriState.Any, draft.ItemStateCriteria.Foulborn);
        Assert.Equal(TradeTriState.Any, draft.ItemVariantCriteria.Foulborn);
    }

    [Fact]
    public void CreateDraft_FracturedRareWithMarker_InitializesFracturedYes()
    {
        // zfortests_v2.txt style — Fractured Item marker + fractured mod
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
{ Suffix Modifier "of Archaeology" (Tier: 2) — Drop }
17(15-20)% increased Rarity of Items found
--------
Fractured Item
""");

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item));

        Assert.True(item.IsFractured);
        Assert.Equal(TradeTriState.Yes, draft.ItemStateCriteria.Fractured);
        Assert.Equal(TradeTriState.Any, draft.ItemStateCriteria.Foulborn);
        Assert.Contains(draft.ModifierFilters, modifier => modifier.IsFractured);
    }

    [Fact]
    public void CreateDraft_DeathTwineStyleFracturedModsWithoutMarker_StillYes()
    {
        // Manual screenshot shape — may not exist in corpus; labeled as synthetic clipboard shape.
        var item = parser.Parse("""
Item Class: Bows
Rarity: Rare
Death Twine
Thicket Bow
--------
Bow
Physical Damage: 20-60
--------
Item Level: 84
--------
{ Fractured Prefix Modifier "Tempered" (Tier: 3) — Damage, Physical, Attack }
80(75-89)% increased Physical Damage
{ Fractured Suffix Modifier "of Venom" (Tier: 4) — Attack, Chaos }
30(25-35)% chance to Poison on Hit
""");

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item));

        Assert.True(item.IsFractured);
        Assert.DoesNotContain("Fractured Item", item.ItemStates);
        Assert.Equal(TradeTriState.Yes, draft.ItemStateCriteria.Fractured);
        Assert.Equal(TradeTriState.Any, draft.ItemStateCriteria.Foulborn);
    }

    [Fact]
    public void CreateDraft_OrdinaryRareAndMagic_KeepBothAny()
    {
        var rare = AssertSuccessfulDraft(mapper.CreateDraft(parser.Parse("""
Item Class: One Hand Axes
Rarity: Rare
Plain Edge
Reaver Axe
--------
Physical Damage: 38-114
Attacks per Second: 1.20
--------
Item Level: 83
""")));
        var magic = AssertSuccessfulDraft(mapper.CreateDraft(parser.Parse("""
Item Class: Thrusting One Hand Swords
Rarity: Magic
Apex Rapier of Recuperation
--------
Rapier
Physical Damage: 29-67
--------
Item Level: 66
""")));

        Assert.Equal(TradeTriState.Any, rare.ItemStateCriteria.Foulborn);
        Assert.Equal(TradeTriState.Any, rare.ItemStateCriteria.Fractured);
        Assert.Equal(TradeTriState.Any, magic.ItemStateCriteria.Foulborn);
        Assert.Equal(TradeTriState.Any, magic.ItemStateCriteria.Fractured);
    }

    [Fact]
    public void CreateDraft_SwitchSequence_SpecialToNormalToSpecial_ReinitializesFromParsedItem()
    {
        var foulborn = AssertSuccessfulDraft(mapper.CreateDraft(parser.Parse("""
Item Class: Belts
Rarity: Unique
Foulborn Mageblood
Heavy Belt
--------
Item Level: 80
--------
{ Foulborn Unique Modifier }
Rightmost 4(2-4) Magic Utility Flasks constantly apply their Flask Effects to you
""")));
        var ordinary = AssertSuccessfulDraft(mapper.CreateDraft(parser.Parse("""
Item Class: One Hand Axes
Rarity: Normal
Reaver Axe
--------
Physical Damage: 38-114
Attacks per Second: 1.20
--------
Item Level: 83
""")));
        var fractured = AssertSuccessfulDraft(mapper.CreateDraft(parser.Parse("""
Item Class: Rings
Rarity: Rare
Fracture Band
Iron Ring
--------
Item Level: 70
--------
{ Fractured Prefix Modifier "Tempered" (Tier: 3) }
80% increased Physical Damage
--------
Fractured Item
""")));

        Assert.Equal(TradeTriState.Yes, foulborn.ItemStateCriteria.Foulborn);
        Assert.Equal(TradeTriState.Any, foulborn.ItemStateCriteria.Fractured);

        Assert.Equal(TradeTriState.Any, ordinary.ItemStateCriteria.Foulborn);
        Assert.Equal(TradeTriState.Any, ordinary.ItemStateCriteria.Fractured);

        Assert.Equal(TradeTriState.Any, fractured.ItemStateCriteria.Foulborn);
        Assert.Equal(TradeTriState.Yes, fractured.ItemStateCriteria.Fractured);
    }

    private static TradeSearchDraft AssertSuccessfulDraft(TradeSearchDraftResult result)
    {
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }
}
