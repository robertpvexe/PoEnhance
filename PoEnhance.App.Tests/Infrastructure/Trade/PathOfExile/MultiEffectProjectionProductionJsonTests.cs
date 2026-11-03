using System.Text.Json;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// Stage 03.11 query proof: the SYNTHETIC clipboard (assembled from real GameData modifier shapes)
/// resolves through the real GameData package, the real game-data-to-draft pipeline and the official
/// Trade stats fixture, then reaches the serialized Search JSON through the controller.
/// </summary>
public sealed class MultiEffectProjectionProductionJsonTests
{
    private const string League = "Mirage";
    private const string PseudoFire = "pseudo.pseudo_total_fire_resistance";
    private const string PseudoChaos = "pseudo.pseudo_total_chaos_resistance";
    private const string PseudoLightning = "pseudo.pseudo_total_lightning_resistance";
    private const string PseudoCold = "pseudo.pseudo_total_cold_resistance";
    private const string PseudoElemental = "pseudo.pseudo_total_elemental_resistance";
    private const string PseudoStrength = "pseudo.pseudo_total_strength";
    private const string PseudoDexterity = "pseudo.pseudo_total_dexterity";
    private const string PseudoIntelligence = "pseudo.pseudo_total_intelligence";
    private const string ExactAllElemental = "explicit.stat_2901986750";

    private const string CraftedFireAndChaosRing = """
        Item Class: Rings
        Rarity: Rare
        Synthetic Loop
        Unset Ring
        --------
        Requirements:
        Level: 60
        --------
        Item Level: 85
        --------
        { Suffix Modifier "of the Whelpling" (Tier: 8) }
        +10(6-11)% to Fire Resistance
        { Master Crafted Suffix Modifier "of Craft" (Rank: 2) }
        +10(9-10)% to Fire and Chaos Resistances
        """;

    // SYNTHETIC: mirrors live Sorrow Scarab one-line Crafted Lightning+Chaos (Group A).
    private const string CraftedLightningAndChaosRing = """
        Item Class: Rings
        Rarity: Rare
        Synthetic Loop
        Unset Ring
        --------
        Requirements:
        Level: 60
        --------
        Item Level: 85
        --------
        { Suffix Modifier "of the Whelpling" (Tier: 8) }
        +10(6-11)% to Fire Resistance
        { Master Crafted Suffix Modifier "of Craft" (Rank: 1) }
        +14(9-10)% to Lightning and Chaos Resistances
        """;

    // SYNTHETIC arithmetic mirrors live Sorrow Scarab (+10 all Attr +9 Strength => Str 19 / Int 10).
    // Abyss jewel modifiers are used so GameData Exact resolve is authentic (no item-name gates).
    private const string AllAttributesWithStrengthJewel = """
        Item Class: Abyss Jewels
        Rarity: Rare
        Synthetic Sorrow
        Murderous Eye Jewel
        --------
        Item Level: 85
        --------
        { Suffix Modifier "of Adaption" }
        +10(8-12) to all Attributes
        { Suffix Modifier "of Strength" }
        +9(8-12) to Strength
        """;

    // SYNTHETIC arithmetic mirrors live Dread Choker (+19 all Attributes => Pseudo Strength 19).
    private const string AllAttributesAloneJewel = """
        Item Class: Abyss Jewels
        Rarity: Rare
        Synthetic Dread
        Murderous Eye Jewel
        --------
        Item Level: 85
        --------
        { Suffix Modifier "of Adaption" }
        +19(16-20) to all Attributes
        """;

    // SYNTHETIC arithmetic mirrors live Ghoul Scarab (+8 all Elemental => Pseudo Cold 8).
    private const string AllElementalAloneJewel = """
        Item Class: Abyss Jewels
        Rarity: Rare
        Synthetic Ghoul
        Murderous Eye Jewel
        --------
        Item Level: 85
        --------
        { Suffix Modifier "of Resistance" }
        +8(6-10)% to all Elemental Resistances
        """;

    private const string AllElementalRing = """
        Item Class: Rings
        Rarity: Rare
        Synthetic Loop
        Unset Ring
        --------
        Requirements:
        Level: 60
        --------
        Item Level: 85
        --------
        { Suffix Modifier "of the Crystal" (Tier: 8) }
        +4(3-5)% to all Elemental Resistances
        { Suffix Modifier "of the Whelpling" (Tier: 8) }
        +10(6-11)% to Fire Resistance
        """;

    private const string CraftedFireAndChaosAloneRing = """
        Item Class: Rings
        Rarity: Rare
        Synthetic Loop
        Unset Ring
        --------
        Requirements:
        Level: 60
        --------
        Item Level: 85
        --------
        { Master Crafted Suffix Modifier "of Craft" (Rank: 2) }
        +13(9-10)% to Fire and Chaos Resistances
        """;

    [Fact]
    public async Task CraftedFireChaosAlone_LogicalRowsExposeFullCatalogModTypes()
    {
        var fixture = await Fixture.CreateAsync(CraftedFireAndChaosAloneRing);
        var fire = fixture.Row("Fire Resistance");
        var chaos = fixture.Row("Chaos Resistance");

        var fireLabels = string.Join(", ", fire.FilterVariants.Select(v => v.Label));
        var chaosLabels = string.Join(", ", chaos.FilterVariants.Select(v => v.Label));
        Assert.True(fire.CanSelectFilterVariant, $"Fire Mod Types truncated/static. Actual: [{fireLabels}]");
        Assert.True(chaos.CanSelectFilterVariant, $"Chaos Mod Types truncated/static. Actual: [{chaosLabels}]");
        foreach (var required in new[] { "Pseudo", "Explicit", "Fractured", "Implicit" })
        {
            Assert.True(
                fire.FilterVariants.Any(v => v.Label == required),
                $"Fire missing '{required}'. Actual: [{fireLabels}]");
            Assert.True(
                chaos.FilterVariants.Any(v => v.Label == required),
                $"Chaos missing '{required}'. Actual: [{chaosLabels}]");
        }

        // Official catalog has Crafted +#% Fire Resistance; Crafted Chaos Resistance is absent.
        Assert.Contains(fire.FilterVariants, v => v.Label == "Crafted");
        Assert.DoesNotContain(chaos.FilterVariants, v => v.Label == "Crafted");
        Assert.Equal("Pseudo", fire.SelectedFilterVariant?.Label);
        Assert.Equal("Pseudo", chaos.SelectedFilterVariant?.Label);
    }

    [Fact]
    public async Task CraftedFireChaosAlone_SharedElementalPseudoMirror_EmitsOnceWithMin13()
    {
        var fixture = await Fixture.CreateAsync(CraftedFireAndChaosAloneRing);
        var fire = fixture.Row("Fire Resistance");
        var chaos = fixture.Row("Chaos Resistance");
        Assert.DoesNotContain(
            chaos.Contributors,
            child => child.SharedCriterionId == SharedSearchCriterion.TotalElementalResistanceId);
        var fireMirror = Assert.Single(
            fire.Contributors,
            child => child.SharedCriterionId == SharedSearchCriterion.TotalElementalResistanceId);
        Assert.Equal("Pseudo", fireMirror.ModTypeLabel);
        Assert.True(fireMirror.HasSingleFilterVariant);
        Assert.True(fireMirror.HasFilterVariants);
        Assert.True(fireMirror.CanSelectFilterVariant);
        Assert.Equal(["Pseudo"], fireMirror.FilterVariants.Select(variant => variant.Label).ToArray());
        Assert.Equal("13", fireMirror.MinimumText);

        Assert.Equal(0, fixture.Window.CurrentSearchState!.SelectedStatsCount);

        fixture.Window.RaiseModifierExpansionChanged(fire.SourceIndex, isExpanded: true);
        fixture.Window.RaiseModifierContributorSelectionChanged(
            fire.SourceIndex,
            fireMirror.ContributorIndex,
            isSelected: true);
        Assert.Equal(1, fixture.Window.CurrentSearchState!.SelectedStatsCount);
        Assert.Equal(
            "Stats: 1 filter selected",
            PriceCheckerWindow.FormatStatsCount(fixture.Window.CurrentSearchState.SelectedStatsCount));

        // Mirror under Fire (and Cold/Lightning hosts when present) is one criterion.
        var coldHost = fixture.Window.CurrentSearchState.Modifiers.FirstOrDefault(row =>
            row.Text.Contains("Cold Resistance", StringComparison.Ordinal));
        if (coldHost is not null)
        {
            var coldMirror = coldHost.Contributors.FirstOrDefault(child =>
                child.SharedCriterionId == SharedSearchCriterion.TotalElementalResistanceId);
            if (coldMirror is not null)
            {
                Assert.True(coldMirror.IsSelected);
                Assert.True(coldMirror.CanSelectFilterVariant);
                Assert.Equal(1, fixture.Window.CurrentSearchState.SelectedStatsCount);
            }
        }

        await fixture.Controller.SearchAsync();

        AssertStatFilters(fixture.LastSearchJson(), (PseudoElemental, 13m));
    }

    // SYNTHETIC abyss arithmetic (GameData Soften/Exact by signature): Dex+Int 22 + all Attributes 12
    // + Dex 8 => Dex Pseudo 42 / Int 34 / Str 12.
    private const string DexIntPairPlusAllAttrPlusDexJewel = """
        Item Class: Abyss Jewels
        Rarity: Rare
        Synthetic Loop
        Murderous Eye Jewel
        --------
        Item Level: 85
        --------
        { Suffix Modifier "of Cunning" }
        +22(8-10) to Dexterity and Intelligence
        { Suffix Modifier "of Adaption" }
        +12(6-8) to all Attributes
        { Suffix Modifier "of Dexterity" }
        +8(12-16) to Dexterity
        """;

    [Fact]
    public async Task StandaloneDexterityPseudoTotal_PrefillsItemWide42_AndDedupesWithAllAttributesChild()
    {
        var fixture = await Fixture.CreateAsync(DexIntPairPlusAllAttrPlusDexJewel);
        var dex = fixture.Row("Dexterity");
        Assert.DoesNotContain(dex.Text, "all Attributes", StringComparison.Ordinal);
        var allAttr = fixture.Row("all Attributes");
        Assert.Equal(3, allAttr.Contributors.Count(child => !child.IsSharedCriterionMirror));
        var dexChild = Assert.Single(
            allAttr.Contributors,
            child => child.Text.Contains("Dexterity", StringComparison.Ordinal) &&
                !child.IsSharedCriterionMirror);

        Assert.Equal("42", dexChild.MinimumText);
        Assert.Equal(42m, fixture.Window.CurrentState!.Draft.ModifierFilters[dex.SourceIndex].RequestedMinimum);

        fixture.Window.RaiseModifierSelectionChanged(dex.SourceIndex, isSelected: true);
        dex = fixture.Row("Dexterity");
        Assert.Equal("42", dex.MinimumText);
        Assert.Equal(1, fixture.Window.CurrentSearchState!.SelectedStatsCount);

        // All Attributes Dex child also Pseudo Total Dexterity → one emitted filter.
        fixture.Window.RaiseModifierExpansionChanged(allAttr.SourceIndex, isExpanded: true);
        fixture.Window.RaiseModifierContributorSelectionChanged(
            allAttr.SourceIndex,
            dexChild.ContributorIndex,
            isSelected: true);
        Assert.Equal(1, fixture.Window.CurrentSearchState!.SelectedStatsCount);
        await fixture.Controller.SearchAsync();

        AssertStatFilters(fixture.LastSearchJson(), (PseudoDexterity, 42m));
    }

    [Fact]
    public async Task SelectingLoneChaosEmitsPseudoChaosWithoutSelectingFire()
    {
        var fixture = await Fixture.CreateAsync(CraftedFireAndChaosRing);
        var chaos = fixture.Row("Chaos Resistance");

        fixture.Window.RaiseModifierSelectionChanged(chaos.SourceIndex, isSelected: true);
        await fixture.Controller.SearchAsync();

        AssertStatFilters(fixture.LastSearchJson(), (PseudoChaos, 10m));
        Assert.False(fixture.Row("Fire Resistance").IsSelected);
        Assert.All(fixture.Row("Fire Resistance").Contributors, child => Assert.False(child.IsSelected));
    }

    [Fact]
    public async Task SelectingFireParentEmitsPseudoFireTotalIncludingProjectedComposite()
    {
        var fixture = await Fixture.CreateAsync(CraftedFireAndChaosRing);

        fixture.Window.RaiseModifierSelectionChanged(fixture.Row("Fire Resistance").SourceIndex, isSelected: true);
        await fixture.Controller.SearchAsync();

        AssertStatFilters(fixture.LastSearchJson(), (PseudoFire, 20m));
        Assert.False(fixture.Row("Chaos Resistance").IsSelected);
    }

    [Fact]
    public async Task SelectingFireAndChaosIndependentlyEmitsTwoDistinctPseudoTotals()
    {
        var fixture = await Fixture.CreateAsync(CraftedFireAndChaosRing);

        fixture.Window.RaiseModifierSelectionChanged(fixture.Row("Fire Resistance").SourceIndex, isSelected: true);
        fixture.Window.RaiseModifierSelectionChanged(fixture.Row("Chaos Resistance").SourceIndex, isSelected: true);
        await fixture.Controller.SearchAsync();

        AssertStatFilters(fixture.LastSearchJson(), (PseudoFire, 20m), (PseudoChaos, 10m));
    }

    [Fact]
    public async Task ExpandingAloneEmitsNoStatFilter()
    {
        var fixture = await Fixture.CreateAsync(CraftedFireAndChaosRing);
        var fire = fixture.Row("Fire Resistance");

        fixture.Window.RaiseModifierExpansionChanged(fire.SourceIndex, isExpanded: true);
        await fixture.Controller.SearchAsync();

        using var document = JsonDocument.Parse(fixture.LastSearchJson());
        var query = document.RootElement.GetProperty("query");
        Assert.False(query.TryGetProperty("stats", out var stats) && stats.GetArrayLength() > 0 &&
            stats[0].GetProperty("filters").GetArrayLength() > 0);
    }

    [Fact]
    public async Task CompactAllElementalFireChildEmitsPseudoFireItemWideTotal()
    {
        var fixture = await Fixture.CreateAsync(AllElementalRing);
        var compact = fixture.Row("all Elemental Resistances");
        Assert.Equal(3, compact.Contributors.Count(child => !child.IsSharedCriterionMirror));
        var fireChild = compact.Contributors.First(child =>
            child.Text.Contains("Fire", StringComparison.Ordinal) &&
            !child.IsSharedCriterionMirror);

        fixture.Window.RaiseModifierContributorSelectionChanged(
            compact.SourceIndex,
            fireChild.ContributorIndex,
            isSelected: true);
        compact = fixture.Row("all Elemental Resistances");
        fireChild = compact.Contributors.First(child =>
            child.Text.Contains("Fire", StringComparison.Ordinal) &&
            !child.IsSharedCriterionMirror);
        Assert.True(fireChild.IsSelected);
        Assert.All(
            compact.Contributors.Where(child =>
                !child.IsSharedCriterionMirror &&
                child.ContributorIndex != fireChild.ContributorIndex),
            child => Assert.False(child.IsSelected));
        Assert.False(compact.IsSelected);

        await fixture.Controller.SearchAsync();

        AssertStatFilters(fixture.LastSearchJson(), (PseudoFire, 14m));
        Assert.DoesNotContain(ExactAllElemental, fixture.LastSearchJson(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompactAllElementalParentOnlyEmitsExactCombinedFilter()
    {
        var fixture = await Fixture.CreateAsync(AllElementalRing);
        var compact = fixture.Row("all Elemental Resistances");

        fixture.Window.RaiseModifierSelectionChanged(compact.SourceIndex, isSelected: true);
        await fixture.Controller.SearchAsync();

        AssertStatFilters(fixture.LastSearchJson(), (ExactAllElemental, 4m));
        Assert.All(
            fixture.Row("all Elemental Resistances").Contributors,
            child => Assert.False(child.IsSelected));
    }

    [Fact]
    public async Task CraftedLightningAndChaos_ProjectsTwoLogicalRows_SelectingBothEmitsDistinctPseudoTotals()
    {
        var fixture = await Fixture.CreateAsync(CraftedLightningAndChaosRing);
        Assert.DoesNotContain(
            fixture.Window.CurrentSearchState!.Modifiers,
            row => row.Text.Contains("Lightning and Chaos", StringComparison.Ordinal));
        var lightning = fixture.Row("Lightning Resistance");
        var chaos = fixture.Row("Chaos Resistance");
        Assert.NotEqual(lightning.SourceIndex, chaos.SourceIndex);

        fixture.Window.RaiseModifierSelectionChanged(lightning.SourceIndex, isSelected: true);
        fixture.Window.RaiseModifierSelectionChanged(chaos.SourceIndex, isSelected: true);
        await fixture.Controller.SearchAsync();

        AssertStatFilters(fixture.LastSearchJson(), (PseudoLightning, 14m), (PseudoChaos, 14m));
    }

    [Fact]
    public async Task LiveArithmetic_AllAttributesPlusStrength_PseudoStrength19_Intelligence10()
    {
        var fixture = await Fixture.CreateAsync(AllAttributesWithStrengthJewel);
        var compact = fixture.Row("all Attributes");
        Assert.Equal(3, compact.Contributors.Count(child => !child.IsSharedCriterionMirror));
        AssertHumanReadableModTypes(compact);

        // Strength child (index 0) totals 10 all-attr + 9 Strength = 19; Intelligence alone = 10.
        var strengthChild = compact.Contributors.First(child =>
            child.Text.Contains("Strength", StringComparison.Ordinal) &&
            !child.IsSharedCriterionMirror);
        var intelligenceChild = compact.Contributors.First(child =>
            child.Text.Contains("Intelligence", StringComparison.Ordinal) &&
            !child.IsSharedCriterionMirror);
        fixture.Window.RaiseModifierContributorSelectionChanged(
            compact.SourceIndex,
            strengthChild.ContributorIndex,
            isSelected: true);
        fixture.Window.RaiseModifierContributorSelectionChanged(
            compact.SourceIndex,
            intelligenceChild.ContributorIndex,
            isSelected: true);
        Assert.Equal(2, fixture.Window.CurrentSearchState!.SelectedStatsCount);
        await fixture.Controller.SearchAsync();

        AssertStatFilters(fixture.LastSearchJson(), (PseudoStrength, 19m), (PseudoIntelligence, 10m));
    }

    [Fact]
    public async Task LiveArithmetic_AllAttributesAlone_PseudoStrength19()
    {
        var fixture = await Fixture.CreateAsync(AllAttributesAloneJewel);
        var compact = fixture.Row("all Attributes");
        AssertHumanReadableModTypes(compact);

        fixture.Window.RaiseModifierContributorSelectionChanged(compact.SourceIndex, 0, isSelected: true);
        await fixture.Controller.SearchAsync();

        AssertStatFilters(fixture.LastSearchJson(), (PseudoStrength, 19m));
    }

    [Fact]
    public async Task LiveArithmetic_AllElementalAlone_PseudoCold8_AndModTypeLabelsHumanReadable()
    {
        var fixture = await Fixture.CreateAsync(AllElementalAloneJewel);
        var compact = fixture.Row("all Elemental Resistances");
        Assert.Equal(3, compact.Contributors.Count(child => !child.IsSharedCriterionMirror));
        AssertHumanReadableModTypes(compact);
        var fireChild = compact.Contributors.First(child =>
            child.Text.Contains("Fire", StringComparison.Ordinal) &&
            !child.IsSharedCriterionMirror);
        Assert.Contains(
            fireChild.FilterVariants,
            variant => string.Equals(variant.Label, "Pseudo", StringComparison.Ordinal));
        Assert.Contains(
            fireChild.FilterVariants,
            variant => string.Equals(variant.Label, "Explicit", StringComparison.Ordinal));
        Assert.Equal("Pseudo", fireChild.SelectedFilterVariant?.Label);

        var coldChild = compact.Contributors.First(child =>
            child.Text.Contains("Cold", StringComparison.Ordinal) &&
            !child.IsSharedCriterionMirror);
        fixture.Window.RaiseModifierContributorSelectionChanged(
            compact.SourceIndex,
            coldChild.ContributorIndex,
            isSelected: true);
        await fixture.Controller.SearchAsync();

        AssertStatFilters(fixture.LastSearchJson(), (PseudoCold, 8m));
    }

    [Fact]
    public async Task CompactLogicalChild_SwitchingModTypeFromPseudoToExplicitChangesTradeStatId()
    {
        var fixture = await Fixture.CreateAsync(AllElementalAloneJewel);
        var compact = fixture.Row("all Elemental Resistances");
        var cold = compact.Contributors.First(child =>
            child.Text.Contains("Cold", StringComparison.Ordinal) &&
            !child.IsSharedCriterionMirror);
        var explicitVariant = Assert.Single(
            cold.FilterVariants,
            variant => string.Equals(variant.Label, "Explicit", StringComparison.Ordinal));

        fixture.Window.RaiseModifierContributorSelectionChanged(compact.SourceIndex, 1, isSelected: true);
        fixture.Window.RaiseModifierContributorFilterVariantChanged(
            compact.SourceIndex,
            1,
            explicitVariant.Identity);
        await fixture.Controller.SearchAsync();

        using var document = JsonDocument.Parse(fixture.LastSearchJson());
        var filter = Assert.Single(
            document.RootElement.GetProperty("query").GetProperty("stats")[0]
                .GetProperty("filters").EnumerateArray());
        var id = filter.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.StartsWith("explicit.", id, StringComparison.Ordinal);
        Assert.DoesNotContain("pseudo", id, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(8m, filter.GetProperty("value").GetProperty("min").GetDecimal());
    }

    // Live Viper Knuckle arithmetic: Implicit Fire+Lightning 15 + All Elemental 14.
    private const string ViperKnuckleShapeRing = """
        Item Class: Rings
        Rarity: Rare
        Synthetic Viper
        Two-Stone Ring
        --------
        Requirements:
        Level: 20
        --------
        Item Level: 85
        --------
        { Implicit Modifier }
        +15(12-16)% to Fire and Lightning Resistances
        --------
        { Suffix Modifier "of the Crystal" (Tier: 8) }
        +14(12-15)% to all Elemental Resistances
        """;

    // Live Dragon Finger Fire total 33 = Implicit Fire+Cold 16 + Fire contribution 17.
    // Ordinary Fire+Chaos is not a RePoE Explicit ring mod (crafted/veiled/unique only); the
    // positive ordinary split control remains Abyss Fire+Cold / Crafted Rank projection tests.
    private const string DragonFingerShapeRing = """
        Item Class: Rings
        Rarity: Rare
        Synthetic Dragon
        Two-Stone Ring
        --------
        Requirements:
        Level: 20
        --------
        Item Level: 85
        --------
        { Implicit Modifier }
        +16(12-16)% to Fire and Cold Resistances
        --------
        { Suffix Modifier "of the Whelpling" (Tier: 8) }
        +17(16-20)% to Fire Resistance
        """;

    // Live Pain Circle Rank-3 Crafted Fire+Chaos + ordinary Fire/Cold.
    private const string PainCircleShapeRing = """
        Item Class: Rings
        Rarity: Rare
        Synthetic Pain
        Two-Stone Ring
        --------
        Requirements:
        Level: 60
        --------
        Item Level: 85
        --------
        { Implicit Modifier }
        +13(12-16)% to Cold and Lightning Resistances
        --------
        { Suffix Modifier "of the Whelpling" (Tier: 8) }
        +20(16-20)% to Fire Resistance
        { Suffix Modifier "of the Whale" (Tier: 8) }
        +15(12-15)% to Cold Resistance
        { Master Crafted Suffix Modifier "of Craft" (Rank: 3) }
        +15(13-15)% to Fire and Chaos Resistances
        """;

    [Fact]
    public async Task ViperKnuckleShape_PseudoLightningTotalIs29IncludingImplicit()
    {
        var fixture = await Fixture.CreateAsync(ViperKnuckleShapeRing);
        var compact = fixture.Row("all Elemental Resistances");
        Assert.Equal(3, compact.Contributors.Count(child => !child.IsSharedCriterionMirror));
        var lightningChild = compact.Contributors.First(child =>
            child.Text.Contains("Lightning", StringComparison.Ordinal) &&
            !child.IsSharedCriterionMirror);
        var labels = lightningChild.FilterVariants.Select(variant => variant.Label).ToArray();
        Assert.Contains("Pseudo", labels);
        Assert.Contains("Explicit", labels);
        // Official catalog also has Fractured/Crafted/Implicit for +#% to Fire Resistance
        // (stat_3372524247 family). Discovery must expose every accepted kind; assert when present.
        foreach (var required in new[] { "Fractured", "Crafted", "Implicit" })
        {
            Assert.True(
                labels.Contains(required, StringComparer.Ordinal),
                $"Expected Mod Type '{required}' for logical Lightning/Fire child. Actual: [{string.Join(", ", labels)}]");
        }

        fixture.Window.RaiseModifierContributorSelectionChanged(
            compact.SourceIndex,
            lightningChild.ContributorIndex,
            isSelected: true);
        await fixture.Controller.SearchAsync();

        AssertStatFilters(fixture.LastSearchJson(), (PseudoLightning, 29m));
    }

    [Fact]
    public async Task DragonFingerShape_PseudoFireTotalIs33AfterOrdinaryPairSplit()
    {
        var fixture = await Fixture.CreateAsync(DragonFingerShapeRing);
        Assert.DoesNotContain(
            fixture.Window.CurrentSearchState!.Modifiers,
            row => row.Text.Contains("Fire and Chaos", StringComparison.Ordinal));
        var fire = fixture.Row("Fire Resistance");
        fixture.Window.RaiseModifierSelectionChanged(fire.SourceIndex, isSelected: true);
        fire = fixture.Row("Fire Resistance");
        var firePseudo = Assert.Single(
            fire.FilterVariants,
            variant => string.Equals(variant.Label, "Pseudo", StringComparison.Ordinal));
        fixture.Window.RaiseModifierFilterVariantChanged(fire.SourceIndex, firePseudo.Identity);
        await fixture.Controller.SearchAsync();

        AssertStatFilters(fixture.LastSearchJson(), (PseudoFire, 33m));
    }

    [Fact]
    public async Task PainCircleShape_CraftedRank3Splits_AndPseudoFire35Chaos15()
    {
        var fixture = await Fixture.CreateAsync(PainCircleShapeRing);
        Assert.DoesNotContain(
            fixture.Window.CurrentSearchState!.Modifiers,
            row => row.Text.Contains("Fire and Chaos", StringComparison.Ordinal));

        // Canonical Two-Stone implicit stays one Implicit row (IsCanonicalImplicit); item-wide
        // Cold/Lightning totals that include it are covered by LogicalEffectItemTotalCalculatorTests.
        Assert.Contains(
            fixture.Window.CurrentSearchState!.Modifiers,
            row => row.Text.Contains("Cold and Lightning", StringComparison.Ordinal) &&
                row.IsCanonicalImplicit);

        var fire = fixture.Row("Fire Resistance");
        var chaos = fixture.Row("Chaos Resistance");
        Assert.Equal("35", fire.MinimumText);
        Assert.Equal("Pseudo", fire.SelectedFilterVariant?.Label);
        fixture.Window.RaiseModifierSelectionChanged(fire.SourceIndex, isSelected: true);
        fixture.Window.RaiseModifierSelectionChanged(chaos.SourceIndex, isSelected: true);
        await fixture.Controller.SearchAsync();

        AssertStatFilters(fixture.LastSearchJson(), (PseudoFire, 35m), (PseudoChaos, 15m));
    }

    private static void AssertHumanReadableModTypes(PriceCheckerModifierViewModel compact)
    {
        Assert.All(
            compact.Contributors.Where(child => !child.IsSharedCriterionMirror),
            child =>
        {
            Assert.NotEmpty(child.FilterVariants);
            Assert.All(child.FilterVariants, variant =>
            {
                Assert.False(string.IsNullOrWhiteSpace(variant.Label));
                Assert.Equal(variant.Label, variant.ToString());
                Assert.DoesNotContain("PriceCheck", variant.ToString(), StringComparison.Ordinal);
                Assert.DoesNotContain("ViewModel", variant.DisplayToolTip, StringComparison.Ordinal);
                Assert.DoesNotContain("Identity =", variant.DisplayToolTip, StringComparison.Ordinal);
                Assert.True(variant.DisplayToolTip.Length <= 80);
            });
            Assert.Equal("Pseudo", child.SelectedFilterVariant?.Label);
            Assert.True(child.HasMultipleFilterVariants);
        });
    }

    private static void AssertStatFilters(string json, params (string Id, decimal Minimum)[] expected)
    {
        using var document = JsonDocument.Parse(json);
        var group = Assert.Single(document.RootElement.GetProperty("query").GetProperty("stats").EnumerateArray());
        Assert.Equal("and", group.GetProperty("type").GetString());
        var filters = group.GetProperty("filters").EnumerateArray().ToArray();
        Assert.Equal(expected.Length, filters.Length);
        foreach (var (id, minimum) in expected)
        {
            var match = Assert.Single(filters, filter => filter.GetProperty("id").GetString() == id);
            Assert.Equal(minimum, match.GetProperty("value").GetProperty("min").GetDecimal());
        }
    }

    private sealed class Fixture
    {
        private Fixture(
            FakeWindow window,
            PriceCheckerSearchController controller,
            FakeSearchClient searchClient)
        {
            Window = window;
            Controller = controller;
            SearchClient = searchClient;
        }

        public FakeWindow Window { get; }

        public PriceCheckerSearchController Controller { get; }

        public FakeSearchClient SearchClient { get; }

        public PriceCheckerModifierViewModel Row(string textFragment)
        {
            var allowCompactParent =
                textFragment.Contains("all Elemental", StringComparison.Ordinal) ||
                textFragment.Contains("all Attributes", StringComparison.Ordinal);
            return Assert.Single(
                Window.CurrentSearchState!.Modifiers,
                row => row.Text.Contains(textFragment, StringComparison.Ordinal) &&
                    (allowCompactParent ||
                        (!row.Text.Contains(" and ", StringComparison.Ordinal) &&
                            !row.Text.Contains("all Elemental", StringComparison.Ordinal) &&
                            !row.Text.Contains("all Attributes", StringComparison.Ordinal))));
        }

        public string LastSearchJson() =>
            PathOfExileTradeJson.SerializeSearchRequest(SearchClient.Requests.Last());

        public static async Task<Fixture> CreateAsync(string clipboard)
        {
            var gameData = await LoadGameDataCatalogAsync();
            var item = new ItemTextParser().Parse(clipboard);
            var baseResolution = new ParsedItemBaseResolver().Resolve(item, gameData);
            var resolutions = new ParsedItemModifierCandidateResolver().Resolve(item, gameData, baseResolution);
            var draftResult = new TradeSearchDraftMapper().CreateDraft(item, baseResolution, resolutions, gameData);
            Assert.True(draftResult.IsSuccess);

            var searchClient = new FakeSearchClient();
            var service = new PathOfExileTradePriceCheckService(
                new PathOfExileTradeQueryBuilder(),
                new PathOfExileTradeStatMatcher(),
                new FakeStatCatalogProvider(LoadOfficialStatCatalog()),
                new FakeItemCatalogProvider(),
                new PathOfExileTradeSelectedModifierMapper(),
                new FakeItemIdentityMapper(),
                searchClient,
                new FakeFetchClient(),
                new FakeFilterCatalogProvider(RingFilterCatalog()),
                gameDataCatalogProvider: () => gameData);
            var window = new FakeWindow();
            var controller = new PriceCheckerSearchController(
                service,
                global::PoEnhance.App.Infrastructure.Settings.ApplicationLeagueSetting.CreateTransient(League),
                new global::PoEnhance.App.Tests.TestTradeLeagueResolver());
            controller.AttachWindow(window);

            var prepared = await controller.PrepareDraftAsync(draftResult.Draft!);
            controller.UpdateCurrentDraft(prepared, new TradeSearchDraftValidator().Validate(prepared));
            return new Fixture(window, controller, searchClient);
        }
    }

    private static PathOfExileTradeStatCatalog LoadOfficialStatCatalog()
    {
        var json = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "Trade",
            "official-stats-2026-08-19.json"));
        return new PathOfExileTradeStatsResponseParser().ParseStatsResponse(json).Catalog!;
    }

    private static PathOfExileTradeFilterCatalog RingFilterCatalog()
    {
        var official = PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
        return new PathOfExileTradeFilterCatalog(
        [
            new PathOfExileTradeFilterOption
            {
                ProviderOrder = 0,
                GroupId = "type_filters",
                FilterId = "category",
                Id = "accessory.ring",
                Text = "Ring",
            },
            new PathOfExileTradeFilterOption
            {
                ProviderOrder = 1,
                GroupId = "type_filters",
                FilterId = "category",
                Id = "jewel.abyss",
                Text = "Abyss Jewel",
            },
        ],
        numericFilterDefinitions: official.NumericFilterDefinitions,
        optionFilterDefinitions: official.OptionFilterDefinitions);
    }

    private static async Task<GameDataCatalog> LoadGameDataCatalogAsync()
    {
        var result = await GameDataPackageLoader.LoadFromFileAsync(
            FindRepoFile("artifacts", "poenhance-game-data.json"));
        Assert.True(result.IsSuccess);
        return GameDataCatalog.FromPackage(result.Package!);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(Path.Combine(relativeParts));
    }

    private sealed class FakeStatCatalogProvider : IPathOfExileTradeStatCatalogProvider
    {
        private readonly PathOfExileTradeStatCatalog catalog;

        public FakeStatCatalogProvider(PathOfExileTradeStatCatalog catalog) => this.catalog = catalog;

        public bool TryGetCachedCatalog(out PathOfExileTradeStatCatalog cachedCatalog)
        {
            cachedCatalog = catalog;
            return true;
        }

        public Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PathOfExileTradeStatCatalogProviderResult.Success(catalog));
    }

    private sealed class FakeFilterCatalogProvider : IPathOfExileTradeFilterCatalogProvider
    {
        private readonly PathOfExileTradeFilterCatalog catalog;

        public FakeFilterCatalogProvider(PathOfExileTradeFilterCatalog catalog) => this.catalog = catalog;

        public bool TryGetCachedCatalog(out PathOfExileTradeFilterCatalog cachedCatalog)
        {
            cachedCatalog = catalog;
            return true;
        }

        public Task<PathOfExileTradeFilterCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PathOfExileTradeFilterCatalogProviderResult.Success(catalog));
    }

    private sealed class FakeSearchClient : IPathOfExileTradeSearchClient
    {
        public List<PathOfExileTradeSearchRequest> Requests { get; } = [];

        public Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
            PathOfExileTradeSearchRequest? request,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request!);
            return Task.FromResult(new PathOfExileTradeSearchExecutionResult
            {
                IsSuccess = true,
                Response = new PathOfExileTradeSearchResponse
                {
                    Id = $"query-{Requests.Count}",
                    Result = [],
                    Total = 0,
                },
            });
        }
    }

    private sealed class FakeFetchClient : IPathOfExileTradeFetchClient
    {
        public Task<PathOfExileTradeFetchExecutionResult> FetchAsync(
            string? queryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Fetch is not expected for zero-result fake Search responses.");
    }

    private sealed class FakeItemCatalogProvider : IPathOfExileTradeItemCatalogProvider
    {
        public Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Item catalog is not expected for ordinary Ring searches.");
    }

    private sealed class FakeItemIdentityMapper : IPathOfExileTradeItemIdentityMapper
    {
        public PathOfExileTradeItemIdentityMappingResult Map(
            TradeSearchDraft? draft,
            PathOfExileTradeItemCatalog? catalog) =>
            throw new InvalidOperationException("Item identity mapping is not expected for ordinary Ring searches.");
    }

#pragma warning disable CS0067
    private sealed class FakeWindow : IPriceCheckerWindow
    {
        public event EventHandler? Closed;
        public event EventHandler? PanelActivated;
        public event EventHandler? PanelDeactivated;
        public event EventHandler? PanelInteraction;
        public event EventHandler? SearchRequested;
        public event EventHandler? LoadMoreRequested;
        public event EventHandler? TradeRequested;
        public event EventHandler<PriceCheckerOfferCapacityChangedEventArgs>? OfferCapacityChanged;
        public event EventHandler<PriceCheckerItemPropertySelectionChangedEventArgs>? ItemPropertySelectionChanged;
        public event EventHandler<PriceCheckerItemPropertyBoundsChangedEventArgs>? ItemPropertyBoundsChanged;
        public event EventHandler<PriceCheckerItemPropertyExpansionChangedEventArgs>? ItemPropertyExpansionChanged;
        public event EventHandler<PriceCheckerModifierSelectionChangedEventArgs>? ModifierSelectionChanged;
        public event EventHandler<PriceCheckerModifierBoundsChangedEventArgs>? ModifierBoundsChanged;
        public event EventHandler<PriceCheckerModifierFilterVariantChangedEventArgs>? ModifierFilterVariantChanged;
        public event EventHandler<PriceCheckerModifierExpansionChangedEventArgs>? ModifierExpansionChanged;
        public event EventHandler? BaseCriterionToggleRequested;
        public event EventHandler<bool>? PinStateChanged;
        public event EventHandler<PriceCheckerHorizontalDragEventArgs>? HorizontalDragDelta;
        public event EventHandler? HorizontalDragCompleted;
        public event EventHandler? HorizontalResizeStarted;
        public event EventHandler<PriceCheckerHorizontalResizeEventArgs>? HorizontalResizeDelta;
        public event EventHandler? HorizontalResizeCompleted;
        public event EventHandler? ResetItemRequested;

        public bool IsClosed { get; private set; }

        public bool IsPinned { get; private set; }

        public PriceCheckerWindowState? CurrentState { get; private set; }

        public PriceCheckerPlacement? CurrentPlacement { get; private set; }

        public PriceCheckerSearchViewState? CurrentSearchState { get; private set; }

        public PriceCheckerPlacement? GetDisplayedPlacement() => CurrentPlacement;

        public void UpdateContent(PriceCheckerWindowState state) => CurrentState = state;

        public void UpdateSearch(PriceCheckerSearchViewState state) => CurrentSearchState = state;

        public void ApplyPlacement(PriceCheckerPlacement placement) => CurrentPlacement = placement;

        public void ShowInactive()
        {
        }

        public void Close()
        {
            IsClosed = true;
            Closed?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseModifierSelectionChanged(int modifierIndex, bool isSelected) =>
            ModifierSelectionChanged?.Invoke(
                this,
                new PriceCheckerModifierSelectionChangedEventArgs(modifierIndex, isSelected));

        public void RaiseModifierContributorSelectionChanged(
            int modifierIndex,
            int contributorIndex,
            bool isSelected) =>
            ModifierSelectionChanged?.Invoke(
                this,
                new PriceCheckerModifierSelectionChangedEventArgs(modifierIndex, isSelected, contributorIndex));

        public void RaiseModifierExpansionChanged(int modifierIndex, bool isExpanded) =>
            ModifierExpansionChanged?.Invoke(
                this,
                new PriceCheckerModifierExpansionChangedEventArgs(modifierIndex, isExpanded));

        public void RaiseModifierContributorFilterVariantChanged(
            int modifierIndex,
            int contributorIndex,
            string variantIdentity) =>
            ModifierFilterVariantChanged?.Invoke(
                this,
                new PriceCheckerModifierFilterVariantChangedEventArgs(
                    modifierIndex,
                    variantIdentity,
                    contributorIndex));

        public void RaiseModifierFilterVariantChanged(int modifierIndex, string variantIdentity) =>
            ModifierFilterVariantChanged?.Invoke(
                this,
                new PriceCheckerModifierFilterVariantChangedEventArgs(modifierIndex, variantIdentity));
    }
#pragma warning restore CS0067
}

