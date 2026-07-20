using System.Text.RegularExpressions;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.App.Infrastructure.PathOfExile;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerProductionPathCorpusTests
{
    [Fact]
    public void ShowOrUpdate_PureAndHybridPhysicalDamageAggregateAndPreserveSourceProvenance()
    {
        using var harness = ProductionPathHarness.Create();

        var snapshot = harness.OpenText("""
Item Class: One Hand Axes
Rarity: Rare
Armageddon Thirst
Reaver Axe
--------
One Handed Axe
Physical Damage: 85-254 (augmented)
Critical Strike Chance: 5.00%
Attacks per Second: 1.50 (augmented)
Weapon Range: 1.1 metres
--------
Requirements:
Level: 61
Str: 167
Dex: 57
--------
Item Level: 85
--------
{ Prefix Modifier "Serrated" (Tier: 7) - Damage, Physical, Attack }
52(50-64)% increased Physical Damage
{ Prefix Modifier "Mercenary's" (Tier: 5) - Damage, Physical, Attack }
39(35-44)% increased Physical Damage
+93(73-97) to Accuracy Rating
{ Suffix Modifier "of Thirst" (Tier: 1) - Attack, Physical, Mana }
2.83(2.6-3.2)% of Physical Attack Damage Leeched as Mana
{ Master Crafted Suffix Modifier "of Craft" (Rank: 3) - Attack, Speed }
20(16-20)% increased Attack Speed
""");
        harness.ApplyResolvedDraft(WithExactTestProviderVariants(snapshot.Draft));

        var physicalComponents = snapshot.Draft.ModifierFilters
            .Where(component => component.OriginalText.Contains("increased Physical Damage", StringComparison.Ordinal))
            .ToArray();

        var physical = Assert.Single(physicalComponents);
        Assert.Equal("91% increased Physical Damage", physical.OriginalText);
        Assert.Equal("<number>% increased Physical Damage", physical.CanonicalSignature);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, physical.ResolutionStatus);
        Assert.Equal(["local_physical_damage_+%"], physical.ResolvedStatIds);
        Assert.Equal(ModifierLocality.Local, physical.Locality);
        Assert.True(physical.IsSearchable);
        Assert.True(physical.SupportsValueBounds);
        Assert.Equal(91m, physical.RequestedMinimum);
        Assert.Null(physical.RequestedMaximum);
        Assert.Equal([91m], physical.CanonicalNumericValues);
        Assert.Equal(2, physical.SourceCount);
        Assert.Equal([0, 1], physical.Sources.Select(source => source.SourceModifierIndex));
        Assert.Equal([0, 0], physical.Sources.Select(source => source.SourceComponentIndex));
        Assert.Equal([52m, 39m], physical.Sources.Select(source => Assert.Single(source.CanonicalNumericValues)));
        Assert.Equal(2, physical.Sources.Select(source => source.ResolvedModifierId).Distinct().Count());
        Assert.Equal(SearchComponentContributorProjection.Additive, physical.ContributorProjection);
        Assert.Equal(
            ["52% increased Physical Damage", "39% increased Physical Damage"],
            physical.Contributors.Select(contributor => contributor.DisplayText));
        Assert.Equal([52m, 39m], physical.Contributors.Select(contributor => contributor.RequestedMinimum));
        Assert.All(physical.Contributors, contributor =>
        {
            Assert.False(contributor.IsSelected);
            Assert.Equal("Explicit", contributor.Source.ProviderDomain);
        });

        var accuracy = Assert.Single(snapshot.Draft.ModifierFilters, component =>
            component.OriginalText.Contains("Accuracy Rating", StringComparison.Ordinal));
        Assert.Equal(1, accuracy.SourceModifierIndex);
        Assert.Equal(1, accuracy.SourceComponentIndex);
        Assert.Equal(["local_accuracy_rating"], accuracy.ResolvedStatIds);
        Assert.Equal(physical.Sources[1].ResolvedModifierId, accuracy.ResolvedModifierId);
        Assert.Equal(ModifierLocality.Local, accuracy.Locality);
        Assert.Equal(93m, accuracy.RequestedMinimum);

        var expandedState = harness.ExpandProperty(TradeSearchItemPropertyKind.PhysicalDps);
        var physicalProperty = Assert.Single(expandedState.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.PhysicalDps);
        Assert.False(Assert.Single(expandedState.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.TotalDps).HasChildren);
        var physicalRow = Assert.Single(physicalProperty.Children, row =>
            row.Text.Contains("increased Physical Damage", StringComparison.Ordinal));
        Assert.Equal("91% increased Physical Damage", physicalRow.Text);
        Assert.Equal("91", physicalRow.MinimumText);
        Assert.Equal(2, physicalRow.SourceCount);
        Assert.Contains("2 sources", physicalRow.SectionLabel, StringComparison.Ordinal);
        Assert.Contains("52(50-64)% increased Physical Damage", physicalRow.SourceBreakdown, StringComparison.Ordinal);
        Assert.Contains("39(35-44)% increased Physical Damage", physicalRow.SourceBreakdown, StringComparison.Ordinal);
        Assert.Equal(2, physicalRow.Contributors.Count);
        Assert.Equal(
            ["Explicit Prefix", "Hybrid Prefix"],
            physicalRow.Contributors.Select(contributor => contributor.ProvenanceLabel));
        Assert.DoesNotContain(expandedState.Modifiers, row => row.SourceIndex == physicalRow.SourceIndex);

        var craftedAttackSpeed = Assert.Single(snapshot.Draft.ModifierFilters, component =>
            component.OriginalText.Contains("increased Attack Speed", StringComparison.Ordinal));
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, craftedAttackSpeed.ResolutionStatus);
        Assert.Equal("EinharMasterLocalIncreasedAttackSpeed3", craftedAttackSpeed.ResolvedModifierId);
        Assert.Equal(["local_attack_speed_+%"], craftedAttackSpeed.ResolvedStatIds);
        Assert.True(craftedAttackSpeed.IsCrafted);
        Assert.True(craftedAttackSpeed.SupportsValueBounds);
        Assert.Equal(20m, craftedAttackSpeed.RequestedMinimum);

        var manaLeech = Assert.Single(snapshot.Draft.ModifierFilters, component =>
            component.OriginalText.Contains("Leeched as Mana", StringComparison.Ordinal));
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, manaLeech.ResolutionStatus);
        Assert.Equal(["local_mana_leech_from_physical_damage_permyriad"], manaLeech.ResolvedStatIds);
        Assert.True(manaLeech.SupportsValueBounds);
        Assert.Equal(2.83m, manaLeech.RequestedMinimum);
        Assert.Null(manaLeech.RequestedMaximum);
    }

    [Fact]
    public void ShowOrUpdate_HorrorManglerExplicitAndCraftedPhysicalDamageAggregateTo146()
    {
        using var harness = ProductionPathHarness.Create();

        var snapshot = harness.OpenText(HorrorManglerExplicitAndCraftedPhysicalDamageText);
        harness.ApplyResolvedDraft(WithExactTestProviderVariants(snapshot.Draft));

        var physical = Assert.Single(snapshot.Draft.ModifierFilters, component =>
            component.OriginalText.Contains("increased Physical Damage", StringComparison.Ordinal));
        Assert.Equal("146% increased Physical Damage", physical.OriginalText);
        Assert.Equal(146m, physical.RequestedMinimum);
        Assert.Null(physical.RequestedMaximum);
        Assert.Equal(2, physical.SourceCount);
        Assert.Equal([30m, 116m], physical.Sources.Select(source => Assert.Single(source.CanonicalNumericValues)));
        Assert.Equal(["Explicit", "Crafted"], physical.Sources.Select(source => source.ProviderDomain));
        Assert.Equal([false, true], physical.Sources.Select(source => source.IsCrafted));
        Assert.Equal(SearchComponentContributorProjection.Additive, physical.ContributorProjection);
        Assert.Equal(2, physical.Contributors.Count);
        Assert.Equal(
            ["30% increased Physical Damage", "116% increased Physical Damage"],
            physical.Contributors.Select(contributor => contributor.DisplayText));
        Assert.Equal([30m, 116m], physical.Contributors.Select(contributor => contributor.RequestedMinimum));
        Assert.All(physical.Contributors, contributor => Assert.False(contributor.IsSelected));
        Assert.Equal(physical.Sources, physical.Contributors.Select(contributor => contributor.Source));
        Assert.DoesNotContain(snapshot.Draft.ModifierAggregationDiagnostics, diagnostic =>
            diagnostic.Message.Contains("provider domain", StringComparison.OrdinalIgnoreCase));
        var physicalGroup = Assert.Single(snapshot.Draft.ItemPropertyContributionGroups);
        Assert.Equal(TradeSearchItemPropertyKind.PhysicalDps, physicalGroup.ParentKind);
        var physicalContribution = Assert.Single(physicalGroup.Contributions);
        Assert.Same(physical, snapshot.Draft.ModifierFilters[physicalContribution.ModifierFilterIndex]);
        Assert.Equal(ItemPropertyTarget.PhysicalDamage, physicalContribution.Target);
        Assert.Equal(ItemPropertyOperation.IncreasedPercent, physicalContribution.Operation);

        var accuracy = Assert.Single(snapshot.Draft.ModifierFilters, component =>
            component.OriginalText.Contains("Accuracy Rating", StringComparison.Ordinal));
        Assert.Equal("+60(47-72) to Accuracy Rating", accuracy.OriginalText);
        Assert.Equal(60m, accuracy.RequestedMinimum);
        Assert.Single(accuracy.Sources);

        var expandedState = harness.ExpandProperty(TradeSearchItemPropertyKind.PhysicalDps);
        var physicalProperty = Assert.Single(expandedState.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.PhysicalDps);
        var physicalRow = Assert.Single(physicalProperty.Children, row =>
            row.Text.Contains("increased Physical Damage", StringComparison.Ordinal));
        Assert.Equal("146% increased Physical Damage", physicalRow.Text);
        Assert.Equal("146", physicalRow.MinimumText);
        Assert.Equal(2, physicalRow.SourceCount);
        Assert.Contains("2 sources", physicalRow.SectionLabel, StringComparison.Ordinal);
        Assert.False(physicalRow.IsExpanded);
        Assert.False(physicalRow.ShowsExpansionControl);
        Assert.True(physicalRow.ContributorsVisible);
        Assert.Equal(2, physicalRow.Contributors.Count);
        Assert.Equal(["30", "116"], physicalRow.Contributors.Select(contributor => contributor.MinimumText));
        Assert.Equal(
            ["Hybrid Prefix", "Crafted Prefix"],
            physicalRow.Contributors.Select(contributor => contributor.ProvenanceLabel));
        Assert.DoesNotContain(expandedState.Modifiers, row =>
            row.Text.StartsWith("30% increased Physical Damage", StringComparison.Ordinal) ||
            row.Text.StartsWith("116% increased Physical Damage", StringComparison.Ordinal));
        Assert.DoesNotContain(expandedState.Modifiers, row => row.SourceIndex == physicalRow.SourceIndex);
    }

    [Fact]
    public void ShowOrUpdate_CataclysmLeagueAggregatesStunRecoveryAndRetainsFixedSourceProvenance()
    {
        using var harness = ProductionPathHarness.Create();

        var snapshot = harness.OpenText("""
Item Class: Boots
Rarity: Rare
Cataclysm League
Slink Boots
--------
Quality: +10% (augmented)
Evasion Rating: 326 (augmented)
--------
Requirements:
Level: 69
Dex: 120
--------
Sockets: G-G-R-G
--------
Item Level: 84
--------
{ Prefix Modifier "Moth's" (Tier: 5) - Defences, Evasion }
14(14-20)% increased Evasion Rating
9(8-9)% increased Stun and Block Recovery
{ Suffix Modifier "of the Troll" (Tier: 3) - Life }
Regenerate 46.8(32.1-48) Life per second
{ Suffix Modifier "of the Whelpling" (Tier: 8) - Elemental, Fire, Resistance }
+6(6-11)% to Fire Resistance
{ Suffix Modifier "of Steel Skin" (Tier: 3) }
22(20-22)% increased Stun and Block Recovery
""");
        var providerReadyState = harness.ApplyResolvedDraft(
            WithExactTestProviderVariants(snapshot.Draft));

        var stunRecovery = Assert.Single(snapshot.Draft.ModifierFilters, component =>
            component.OriginalText.Contains("Stun and Block Recovery", StringComparison.Ordinal));
        Assert.Equal(
            TradeSearchItemPropertyKind.EvasionRating,
            Assert.Single(snapshot.Draft.ItemPropertyContributionGroups).ParentKind);
        Assert.Equal("31% increased Stun and Block Recovery", stunRecovery.OriginalText);
        Assert.Equal(31m, stunRecovery.RequestedMinimum);
        Assert.Equal(ModifierStatMappingProofStatus.ProvenExact, stunRecovery.StatMappingProof);
        Assert.Null(stunRecovery.ReviewedItemPropertySemantic);
        Assert.Equal(SearchComponentContributorProjection.Additive, stunRecovery.ContributorProjection);
        Assert.Equal([9m, 22m], stunRecovery.Contributors.Select(contributor => contributor.RequestedMinimum));
        Assert.Equal(stunRecovery.Sources, stunRecovery.Contributors.Select(contributor => contributor.Source));
        Assert.All(stunRecovery.Sources, source =>
        {
            Assert.Equal(ModifierStatMappingProofStatus.ProvenExact, source.StatMappingProof);
            Assert.Null(source.ReviewedItemPropertySemantic);
        });

        var row = Assert.Single(providerReadyState.Modifiers, modifier =>
            modifier.Text.Contains("Stun and Block Recovery", StringComparison.Ordinal));
        Assert.Equal(
            TradeSearchItemPropertyKind.EvasionRating,
            Assert.Single(providerReadyState.ItemProperties).Kind);
        Assert.Equal(4, providerReadyState.Stats.Count);
        Assert.True(row.ShowsExpansionControl);
        Assert.True(row.HasContributors);
        Assert.Equal("31", row.MinimumText);
        Assert.Equal(["9", "22"], row.Contributors.Select(contributor => contributor.MinimumText));
        Assert.Equal(
            ["Hybrid Prefix", "Explicit Suffix"],
            row.Contributors.Select(contributor => contributor.ProvenanceLabel));
        Assert.All(
            providerReadyState.Modifiers.Where(modifier => modifier.SourceIndex != row.SourceIndex),
            modifier => Assert.False(modifier.ShowsExpansionControl));
    }

    [Fact]
    public void ShowOrUpdate_MorbidBiteReaverAxe_PreservesCopiedRareIdentityAndBothModifiers()
    {
        using var harness = ProductionPathHarness.Create();

        var snapshot = harness.OpenFixture(3);

        Assert.Equal("Rare", snapshot.AfterParse.Rarity);
        Assert.Equal("Morbid Bite", snapshot.AfterParse.DisplayName);
        Assert.Equal("Reaver Axe", snapshot.AfterParse.BaseType);
        Assert.Equal(2, snapshot.AfterParse.Modifiers.Count);
        Assert.Equal(2, snapshot.AfterParse.Modifiers.Sum(modifier => modifier.Effects.Count));
        Assert.Contains(
            snapshot.AfterParse.Modifiers.SelectMany(modifier => modifier.Effects),
            effect => effect.Text.Contains("Physical Damage", StringComparison.Ordinal));
        Assert.Contains(
            snapshot.AfterParse.Modifiers.SelectMany(modifier => modifier.Effects),
            effect => effect.Text.Contains("increased Attack Speed", StringComparison.Ordinal));

        if (snapshot.BaseResolution.Status is ItemBaseResolutionStatus.Exact or ItemBaseResolutionStatus.Probable)
        {
            Assert.Equal("Reaver Axe", snapshot.BaseResolution.ResolvedBaseName);
        }

        Assert.Equal("Rare", snapshot.Draft.Rarity);
        Assert.Equal("Morbid Bite", snapshot.Draft.DisplayName);
        Assert.Equal("Reaver Axe", snapshot.Draft.ParsedBaseType);
        Assert.Equal("Reaver Axe", snapshot.Draft.Base.Observed?.ExactBaseName);
        Assert.Equal("One Hand Axe", snapshot.Draft.Base.ActiveCriterion?.Category);
        Assert.Equal(BaseSearchMode.Category, snapshot.Draft.Base.ActiveCriterion?.Mode);
        Assert.Equal(2, snapshot.Draft.ModifierFilters.Count);

        Assert.Equal("Rare", snapshot.WindowState.Draft.Rarity);
        Assert.Equal("Morbid Bite", snapshot.WindowState.Draft.DisplayName);
        Assert.Equal("Reaver Axe", snapshot.WindowState.Draft.ParsedBaseType);
        Assert.Empty(snapshot.SearchState.Modifiers);
        Assert.DoesNotContain(
            snapshot.SearchState.Modifiers,
            row => string.Equals(row.Text, "Reaver Axe of Celebration", StringComparison.Ordinal));
        Assert.DoesNotContain(
            snapshot.SearchState.Modifiers,
            row => row.Text.Contains("Unscalable Value", StringComparison.Ordinal));
        Assert.Contains(
            snapshot.Draft.ModifierFilters,
            modifier => modifier.OriginalText.Contains("Physical Damage", StringComparison.Ordinal));
        Assert.Equal(2, snapshot.Draft.ItemPropertyContributionGroups.Count());
        var physicalGroup = Assert.Single(snapshot.Draft.ItemPropertyContributionGroups, group =>
            group.ParentKind == TradeSearchItemPropertyKind.PhysicalDps);
        Assert.Equal(TradeSearchItemPropertyKind.PhysicalDps, physicalGroup.ParentKind);
        var physicalContribution = Assert.Single(physicalGroup.Contributions);
        Assert.Contains(
            "Physical Damage",
            snapshot.Draft.ModifierFilters[physicalContribution.ModifierFilterIndex].OriginalText,
            StringComparison.Ordinal);
        var expandedState = harness.ExpandProperty(TradeSearchItemPropertyKind.PhysicalDps);
        var physicalProperty = Assert.Single(expandedState.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.PhysicalDps);
        var physicalRow = Assert.Single(physicalProperty.Children);
        Assert.Contains("Physical Damage", physicalRow.Text, StringComparison.Ordinal);
        Assert.Equal(physicalContribution.ModifierFilterIndex, physicalRow.SourceIndex);
        Assert.DoesNotContain(expandedState.Modifiers, row => row.SourceIndex == physicalRow.SourceIndex);

        var attackSpeedGroup = Assert.Single(snapshot.Draft.ItemPropertyContributionGroups, group =>
            group.ParentKind == TradeSearchItemPropertyKind.AttacksPerSecond);
        var attackSpeedContribution = Assert.Single(attackSpeedGroup.Contributions);
        var attackSpeedState = harness.ExpandProperty(TradeSearchItemPropertyKind.AttacksPerSecond);
        var attackSpeedProperty = Assert.Single(attackSpeedState.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.AttacksPerSecond);
        var attackSpeedRow = Assert.Single(attackSpeedProperty.Children);
        Assert.Equal(attackSpeedContribution.ModifierFilterIndex, attackSpeedRow.SourceIndex);
        Assert.Contains("increased Attack Speed", attackSpeedRow.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(attackSpeedState.Modifiers, row => row.SourceIndex == attackSpeedRow.SourceIndex);
        Assert.Equal(
            1,
            attackSpeedState.Modifiers
                .Concat(attackSpeedState.ItemProperties.SelectMany(property => property.Children))
                .Count(row => row.SourceIndex == attackSpeedRow.SourceIndex));
    }

    [Fact]
    public void ShowOrUpdate_RealCopiedGroupsStayCanonicalAndExcludeUnsafeOrNonWeaponModifiers()
    {
        using var harness = ProductionPathHarness.Create();

        var golem = harness.OpenFixture(2).Draft;
        var golemGroup = Assert.Single(golem.ItemPropertyContributionGroups);
        Assert.Equal(TradeSearchItemPropertyKind.ElementalDps, golemGroup.ParentKind);
        Assert.Equal(
            ["Cold Damage", "Fire Damage", "Lightning Damage"],
            golemGroup.Contributions.Select(contribution =>
                DamageSuffix(golem.ModifierFilters[contribution.ModifierFilterIndex].OriginalText)));

        var wrath = harness.OpenFixture(4).Draft;
        var wrathGroup = Assert.Single(wrath.ItemPropertyContributionGroups);
        var wrathContribution = Assert.Single(wrathGroup.Contributions);
        Assert.Contains(
            "Cold Damage",
            wrath.ModifierFilters[wrathContribution.ModifierFilterIndex].OriginalText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(wrathGroup.Contributions, contribution =>
        {
            var text = wrath.ModifierFilters[contribution.ModifierFilterIndex].OriginalText;
            return text.Contains("Lightning Damage to Spells", StringComparison.Ordinal) ||
                text.Contains("increased Lightning Damage", StringComparison.Ordinal);
        });

        var eagle = harness.OpenFixture(7).Draft;
        Assert.Empty(eagle.ItemPropertyContributionGroups);
    }

    [Fact]
    public void ShowOrUpdate_RealCopiedGroupedRowsAppearOnlyUnderTheirCanonicalParents()
    {
        using var harness = ProductionPathHarness.Create();

        var golemSnapshot = harness.OpenFixture(2);
        var golemState = harness.ExpandProperty(TradeSearchItemPropertyKind.ElementalDps);
        var elemental = Assert.Single(golemState.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.ElementalDps);
        Assert.Equal(
            ["Cold Damage", "Fire Damage", "Lightning Damage"],
            elemental.Children.Select(child => DamageSuffix(child.Text)));
        Assert.Single(golemState.Modifiers);
        Assert.Contains("Dexterity", golemState.Modifiers[0].Text, StringComparison.Ordinal);
        Assert.All(elemental.Children, child =>
            Assert.DoesNotContain(golemState.Modifiers, row => row.SourceIndex == child.SourceIndex));

        var wrathSnapshot = harness.OpenFixture(4);
        var wrathState = harness.ExpandProperty(TradeSearchItemPropertyKind.ElementalDps);
        var wrathElemental = Assert.Single(wrathState.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.ElementalDps);
        var cold = Assert.Single(wrathElemental.Children);
        Assert.Contains("Cold Damage", cold.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(wrathState.Modifiers, row => row.SourceIndex == cold.SourceIndex);
        Assert.Contains(wrathState.Modifiers, row =>
            row.Text.Contains("Lightning Damage to Spells", StringComparison.Ordinal));
        Assert.Contains(wrathState.Modifiers, row =>
            row.Text.Contains("increased Lightning Damage", StringComparison.Ordinal));

        Assert.NotEmpty(golemSnapshot.Draft.ItemPropertyContributionGroups);
        Assert.NotEmpty(wrathSnapshot.Draft.ItemPropertyContributionGroups);
    }

    [Fact]
    public void ShowOrUpdate_ReaverAxeOfCelebration_ResolvesMagicSuffixBaseAndPreservesCopiedIdentity()
    {
        using var harness = ProductionPathHarness.Create();

        var snapshot = harness.OpenText("""
Item Class: One Hand Axes
Rarity: Magic
Reaver Axe of Celebration
--------
One Handed Axe
Physical Damage: 38-114
Critical Strike Chance: 5.00%
Attacks per Second: 1.51 (augmented)
Weapon Range: 1.1 metres
--------
Requirements:
Level: 61
Str: 167
Dex: 57
--------
Sockets: B B-R
--------
Item Level: 85
--------
{ Suffix Modifier "of Celebration" (Tier: 1) - Attack, Speed }
26(26-27)% increased Attack Speed
""");

        Assert.Equal("Magic", snapshot.AfterParse.Rarity);
        Assert.Equal("Reaver Axe of Celebration", snapshot.AfterParse.DisplayName);
        Assert.Null(snapshot.AfterParse.BaseType);
        Assert.Equal("Reaver Axe", snapshot.BaseResolution.ResolvedBaseName);
        Assert.Equal(ItemBaseResolutionStatus.Probable, snapshot.BaseResolution.Status);
        Assert.Equal(1, snapshot.AfterParse.Modifiers.Sum(modifier => modifier.Effects.Count));

        Assert.Equal("Magic", snapshot.Draft.Rarity);
        Assert.Equal("Reaver Axe of Celebration", snapshot.Draft.DisplayName);
        Assert.Null(snapshot.Draft.ParsedBaseType);
        Assert.Equal("Reaver Axe", snapshot.Draft.Base.ResolvedBaseName);
        Assert.Equal("Reaver Axe", snapshot.Draft.Base.Observed?.ExactBaseName);
        Assert.Equal("One Hand Axe", snapshot.Draft.Base.ActiveCriterion?.Category);
        Assert.Equal(BaseSearchMode.Category, snapshot.Draft.Base.ActiveCriterion?.Mode);
        Assert.Single(snapshot.Draft.ModifierFilters);

        Assert.Equal("Magic", snapshot.WindowState.Draft.Rarity);
        Assert.Equal("Reaver Axe of Celebration", snapshot.WindowState.Draft.DisplayName);
        Assert.Empty(snapshot.SearchState.Modifiers);
        var attackSpeedGroup = Assert.Single(snapshot.Draft.ItemPropertyContributionGroups, group =>
            group.ParentKind == TradeSearchItemPropertyKind.AttacksPerSecond);
        var attackSpeedContribution = Assert.Single(attackSpeedGroup.Contributions);
        var expandedState = harness.ExpandProperty(TradeSearchItemPropertyKind.AttacksPerSecond);
        var attackSpeedProperty = Assert.Single(expandedState.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.AttacksPerSecond);
        var attackSpeedRow = Assert.Single(attackSpeedProperty.Children);
        Assert.Equal(attackSpeedContribution.ModifierFilterIndex, attackSpeedRow.SourceIndex);
        Assert.Contains("increased Attack Speed", attackSpeedRow.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(expandedState.Modifiers, row => row.SourceIndex == attackSpeedRow.SourceIndex);
        Assert.Equal(
            1,
            expandedState.Modifiers
                .Concat(expandedState.ItemProperties.SelectMany(property => property.Children))
                .Count(row => row.SourceIndex == attackSpeedRow.SourceIndex));
    }

    [Fact]
    public void ShowOrUpdate_WaspsSupremeSpikedShield_AggregatesEquivalentExplicitRecoveryEffects()
    {
        using var harness = ProductionPathHarness.Create();

        var snapshot = harness.OpenText("""
Item Class: Shields
Rarity: Magic
Wasp's Supreme Spiked Shield of Thick Skin
--------
Chance to Block: 24%
Evasion Rating: 362 (augmented)
Energy Shield: 73 (augmented)
--------
Requirements:
Level: 70
Dex: 85
Int: 85
--------
Sockets: B-B-G
--------
Item Level: 84
--------
{ Implicit Modifier }
+5% chance to Suppress Spell Damage
(40% of Damage from Suppressed Hits and Ailments they inflict is prevented)
--------
{ Prefix Modifier "Wasp's" (Tier: 3) - Defences, Evasion, Energy Shield }
31(27-32)% increased Evasion and Energy Shield
13(12-13)% increased Stun and Block Recovery
{ Suffix Modifier "of Thick Skin" (Tier: 6) }
11(11-13)% increased Stun and Block Recovery
""");
        var providerReadyState = harness.ApplyResolvedDraft(
            WithExactTestProviderVariants(snapshot.Draft));

        Assert.Equal("Magic", snapshot.AfterParse.Rarity);
        Assert.Equal("Wasp's Supreme Spiked Shield of Thick Skin", snapshot.AfterParse.DisplayName);
        Assert.Null(snapshot.AfterParse.BaseType);
        Assert.Equal("Supreme Spiked Shield", snapshot.BaseResolution.ResolvedBaseName);
        Assert.Equal(ItemBaseResolutionStatus.Probable, snapshot.BaseResolution.Status);
        Assert.Equal(4, snapshot.AfterParse.Modifiers.Sum(modifier => modifier.Effects.Count));

        Assert.Equal("Magic", snapshot.Draft.Rarity);
        Assert.Equal("Wasp's Supreme Spiked Shield of Thick Skin", snapshot.Draft.DisplayName);
        Assert.Null(snapshot.Draft.ParsedBaseType);
        Assert.Equal("Supreme Spiked Shield", snapshot.Draft.Base.ResolvedBaseName);
        Assert.Equal("Supreme Spiked Shield", snapshot.Draft.Base.Observed?.ExactBaseName);
        Assert.Equal("Shield", snapshot.Draft.Base.ActiveCriterion?.Category);
        Assert.Equal(BaseSearchMode.Category, snapshot.Draft.Base.ActiveCriterion?.Mode);
        Assert.Equal(3, snapshot.Draft.ModifierFilters.Count);

        Assert.Equal("Magic", snapshot.WindowState.Draft.Rarity);
        Assert.Equal("Wasp's Supreme Spiked Shield of Thick Skin", snapshot.WindowState.Draft.DisplayName);
        Assert.Equal(2, providerReadyState.Modifiers.Count);
        Assert.Contains(
            providerReadyState.Modifiers,
            row => row.Text.Contains("chance to Suppress Spell Damage", StringComparison.Ordinal));
        Assert.All(
            providerReadyState.ItemProperties.Where(property => property.Kind is
                TradeSearchItemPropertyKind.EnergyShield or TradeSearchItemPropertyKind.EvasionRating),
            property => Assert.Contains(property.Children,
                row => row.Text.Contains("increased Evasion and Energy Shield", StringComparison.Ordinal)));
        Assert.Contains(
            providerReadyState.Modifiers,
            row => row.Text == "24% increased Stun and Block Recovery" &&
                row.SourceCount == 2 &&
                row.MinimumText == "24");
    }

    public static IEnumerable<object[]> OpeningStateFixtures()
    {
        yield return
        [
            0,
            "Necrotic Armour",
            "Normal",
            "Necrotic Armour",
            "Necrotic Armour",
            "Body Armour",
            Array.Empty<string>(),
        ];
        yield return
        [
            2,
            "Golem Fletch Ranger Bow",
            "Rare",
            "Golem Fletch",
            "Ranger Bow",
            "Bow",
            new[]
            {
                "Cold Damage",
                "Fire Damage",
                "Lightning Damage",
                "Dexterity",
            },
        ];
        yield return
        [
            4,
            "Wrath Cry Blasting Wand",
            "Rare",
            "Wrath Cry",
            "Blasting Wand",
            "Wand",
            new[]
            {
                "Cannot roll Caster Modifiers",
                "Lightning Damage to Spells",
                "Cold Damage",
                "chance to Shock",
                "increased Lightning Damage",
                "Gain 17(12-18) Life per Enemy Killed",
            },
        ];
        yield return
        [
            7,
            "Eagle Spiral Organic Ring",
            "Rare",
            "Eagle Spiral",
            "Organic Ring",
            "Ring",
            new[]
            {
                "additional Physical Damage Reduction",
                "Cannot roll Modifiers of Non-Physical Damage Types",
                "Adds 6(5-7) to 12(11-12) Physical Damage to Attacks",
                "Evasion Rating",
                "maximum Mana",
                "Dexterity",
            },
        ];
    }

    [Theory]
    [MemberData(nameof(OpeningStateFixtures))]
    public void ShowOrUpdate_RealCopiedItems_UseProductionOpeningState(
        int fixtureIndex,
        string label,
        string expectedRarity,
        string expectedName,
        string expectedBase,
        string expectedCategory,
        IReadOnlyList<string> expectedRowFragments)
    {
        using var harness = ProductionPathHarness.Create();

        var snapshot = harness.OpenFixture(fixtureIndex);

        Assert.Equal(expectedRarity, snapshot.Draft.Rarity);
        Assert.Equal(expectedName, snapshot.Draft.DisplayName);
        Assert.Equal(expectedBase, snapshot.Draft.ParsedBaseType);
        Assert.Equal(expectedBase, snapshot.Draft.Base.Observed?.ExactBaseName);
        Assert.Equal(expectedCategory, snapshot.Draft.Base.ActiveCriterion?.Category);
        Assert.Equal(BaseSearchMode.Category, snapshot.Draft.Base.ActiveCriterion?.Mode);
        var searchState = !snapshot.Draft.ItemPropertyContributionGroups.Any()
            ? snapshot.SearchState
            : harness.ExpandProperty(Assert.Single(snapshot.Draft.ItemPropertyContributionGroups).ParentKind);
        var projectedRows = searchState.Modifiers
            .Concat(searchState.ItemProperties.SelectMany(property => property.Children))
            .ToArray();
        Assert.Equal(expectedRowFragments.Count, projectedRows.Length);
        foreach (var expectedRowFragment in expectedRowFragments)
        {
            Assert.Contains(
                projectedRows,
                row => row.Text.Contains(expectedRowFragment, StringComparison.Ordinal));
        }

        Assert.All(projectedRows, row =>
            Assert.DoesNotContain("Unscalable Value", row.Text, StringComparison.Ordinal));
        Assert.DoesNotContain(
            projectedRows,
            row => row.Text.StartsWith("(", StringComparison.Ordinal) ||
                string.Equals(row.Text, "Our flesh longs to move as one.", StringComparison.Ordinal));
        Assert.Equal(label, label);
    }

    [Fact]
    public void DefensiveCorpus_UsesQ20ParentsAndSharedCanonicalHybridProjection()
    {
        using var harness = ProductionPathHarness.Create();

        var dusk = harness.OpenFixture(8);
        var armour = Assert.Single(dusk.Draft.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.Armour);
        Assert.Equal(2047m, armour.ObservedValue);
        Assert.Equal("Q20", armour.CalculationBasisLabel);

        var gale = harness.OpenFixture(11);
        Assert.Collection(
            gale.Draft.ItemProperties.Where(property => property.Kind is
                TradeSearchItemPropertyKind.Armour or TradeSearchItemPropertyKind.EvasionRating),
            property =>
            {
                Assert.Equal(TradeSearchItemPropertyKind.Armour, property.Kind);
                Assert.Equal(2330m, property.ObservedValue);
                Assert.Equal("Q20", property.CalculationBasisLabel);
            },
            property =>
            {
                Assert.Equal(TradeSearchItemPropertyKind.EvasionRating, property.Kind);
                Assert.Equal(2349m, property.ObservedValue);
                Assert.Equal("Q20", property.CalculationBasisLabel);
            });
        harness.ApplyResolvedDraft(WithExactTestProviderVariants(gale.Draft));

        harness.ExpandProperty(TradeSearchItemPropertyKind.Armour);
        var expanded = harness.ExpandProperty(TradeSearchItemPropertyKind.EvasionRating);
        var armourRow = Assert.Single(expanded.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.Armour);
        var evasionRow = Assert.Single(expanded.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.EvasionRating);
        var armourProjection = Assert.Single(armourRow.Children, child =>
            child.Text.Contains("105% increased Armour and Evasion", StringComparison.Ordinal));
        var evasionProjection = Assert.Single(evasionRow.Children, child =>
            child.Text.Contains("105% increased Armour and Evasion", StringComparison.Ordinal));
        Assert.Equal(armourProjection.SourceIndex, evasionProjection.SourceIndex);
        Assert.Equal("Shared with Evasion Rating", armourProjection.SectionLabel);
        Assert.Equal("Shared with Armour", evasionProjection.SectionLabel);

        var selected = harness.SelectModifier(armourProjection.SourceIndex, true);
        Assert.True(Assert.Single(selected.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.Armour).Children.Single(child =>
                child.SourceIndex == armourProjection.SourceIndex).IsSelected);
        Assert.True(Assert.Single(selected.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.EvasionRating).Children.Single(child =>
                child.SourceIndex == armourProjection.SourceIndex).IsSelected);
        Assert.Equal(1, selected.SelectedModifierCount);
        Assert.Equal(0, harness.SearchCount);
    }

    [Fact]
    public void MiracleBastion_UsesSeparateQ20DefencesAndNonQ20BaseBlock()
    {
        using var harness = ProductionPathHarness.Create();

        var snapshot = harness.OpenFixture(13);

        var energyShield = Assert.Single(snapshot.Draft.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.EnergyShield);
        var evasion = Assert.Single(snapshot.Draft.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.EvasionRating);
        var block = Assert.Single(snapshot.Draft.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.ChanceToBlock);
        Assert.Equal((149m, "Q20"), (energyShield.ObservedValue, energyShield.CalculationBasisLabel));
        Assert.Equal((515m, "Q20"), (evasion.ObservedValue, evasion.CalculationBasisLabel));
        Assert.Equal(24m, block.ObservedValue);
        Assert.Null(block.CalculationBasisLabel);
    }

    [Fact]
    public void GoldenBuckler_ReconstructsFlooredLocalBlockAndKeepsParentAndChildSelectable()
    {
        using var harness = ProductionPathHarness.Create();

        var snapshot = harness.OpenText(GoldenBucklerText);

        var block = Assert.Single(snapshot.Draft.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.ChanceToBlock);
        Assert.Equal(40m, block.ObservedValue);
        Assert.Null(block.DerivationUnsupportedReason);
        var resolved = new PathOfExileTradeItemPropertyResolver().Resolve(
            snapshot.Draft,
            PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog());
        AssertExactProperty(resolved, TradeSearchItemPropertyKind.ChanceToBlock);
        harness.ApplyResolvedDraft(WithExactTestProviderVariants(resolved));
        var child = Assert.Single(snapshot.Draft.ModifierFilters, component =>
            component.OriginalText.Contains("increased Chance to Block", StringComparison.Ordinal));
        Assert.True(child.IsSearchable);
        Assert.Equal(62m, child.RequestedMinimum);

        var expanded = harness.ExpandProperty(TradeSearchItemPropertyKind.ChanceToBlock);
        var blockRow = Assert.Single(expanded.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.ChanceToBlock);
        var childRow = Assert.Single(blockRow.Children, row =>
            row.Text.Contains("increased Chance to Block", StringComparison.Ordinal));

        var selectedParent = harness.SelectProperty(TradeSearchItemPropertyKind.ChanceToBlock, true);
        Assert.True(Assert.Single(selectedParent.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.ChanceToBlock).IsSelected);
        var selectedChild = harness.SelectModifier(childRow.SourceIndex, true);
        Assert.True(Assert.Single(selectedChild.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.ChanceToBlock).Children.Single(row =>
                row.SourceIndex == childRow.SourceIndex).IsSelected);
        Assert.Equal(0, harness.SearchCount);
    }

    [Fact]
    public void RuntimeCatalogResolution_GenuineDefensiveFixturesUseOnlyReviewedEvasionAndBlockEntries()
    {
        using var harness = ProductionPathHarness.Create();
        var officialCatalog = PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
        var resolver = new PathOfExileTradeItemPropertyResolver();

        var necrotic = resolver.Resolve(harness.OpenFixture(0).Draft, officialCatalog);
        AssertExactProperty(necrotic, TradeSearchItemPropertyKind.EvasionRating);

        var gale = resolver.Resolve(harness.OpenFixture(11).Draft, officialCatalog);
        AssertExactProperty(gale, TradeSearchItemPropertyKind.EvasionRating);

        var golden = resolver.Resolve(harness.OpenText(GoldenBucklerText).Draft, officialCatalog);
        AssertExactProperty(golden, TradeSearchItemPropertyKind.EvasionRating);
        AssertExactProperty(golden, TradeSearchItemPropertyKind.ChanceToBlock);

        var miracle = resolver.Resolve(harness.OpenFixture(13).Draft, officialCatalog);
        AssertExactProperty(miracle, TradeSearchItemPropertyKind.EvasionRating);
        AssertExactProperty(miracle, TradeSearchItemPropertyKind.ChanceToBlock);
        Assert.DoesNotContain(
            miracle.ItemProperties,
            property => property.NotSearchableReason?.Contains(
                "catalog entry is incompatible",
                StringComparison.Ordinal) == true);

        var invalidDefinitions = officialCatalog.NumericFilterDefinitions.Select(definition =>
            definition.FilterId is "ev" or "block"
                ? definition with { SupportsMinMax = false }
                : definition);
        var invalidCatalog = new PathOfExileTradeFilterCatalog(
            officialCatalog.CategoryOptions,
            numericFilterDefinitions: invalidDefinitions);
        var invalid = resolver.Resolve(harness.OpenFixture(13).Draft, invalidCatalog);
        Assert.All(
            invalid.ItemProperties.Where(property => property.Kind is
                TradeSearchItemPropertyKind.EvasionRating or TradeSearchItemPropertyKind.ChanceToBlock),
            property =>
            {
                Assert.False(property.IsSearchable);
                Assert.Contains("catalog entry is incompatible", property.NotSearchableReason, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void ShowOrUpdate_DemonIdolNamedUnveiledBlockKeepsOneSourceAndTwoExactLineComponents()
    {
        using var harness = ProductionPathHarness.Create();

        var snapshot = harness.OpenText(DemonIdolText);

        var chosenPair = Assert.Single(snapshot.AfterParse.Modifiers
            .Select((modifier, index) => new { modifier, index }), pair => pair.modifier.Name == "Chosen");
        var chosen = chosenPair.modifier;
        Assert.Equal(ParsedModifierKind.Prefix, chosen.Kind);
        Assert.Equal(2, chosen.ValueLines.Count);
        Assert.False(chosen.IsUnrevealedVeiledPlaceholder);

        var resolution = Assert.Single(snapshot.ModifierResolutions, candidate =>
            candidate.ParsedModifierIndex == chosenPair.index);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, resolution.Status);
        Assert.Equal("unveiled", Assert.Single(resolution.Candidates).Domain);

        var components = snapshot.Draft.ModifierFilters
            .Where(component => component.SourceModifierIndex == resolution.ParsedModifierIndex)
            .OrderBy(component => component.SourceLineIndex)
            .ToArray();
        Assert.Equal(2, components.Length);
        Assert.All(components, component =>
        {
            Assert.True(component.IsUnveiled);
            Assert.False(component.IsVeiled);
            Assert.Equal("Chosen", component.ParsedModifierName);
            Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
            Assert.Equal(ModifierStatMappingProofStatus.ProvenExact, component.StatMappingProof);
            Assert.Equal(ModifierBoundShape.ArithmeticMeanRange, component.ValueBoundShape);
            Assert.NotNull(component.ProviderCanonicalSignature);
        });
        Assert.Equal([16m, 21m], components[0].ObservedNumericValues);
        Assert.Equal([15m, 20m], components[1].ObservedNumericValues);
        Assert.Equal(
            [new ModifierSourceRollRange(14m, 16m), new ModifierSourceRollRange(20m, 22m)],
            components[0].OriginalSourceRollRanges);
        Assert.Equal(
            [new ModifierSourceRollRange(14m, 16m), new ModifierSourceRollRange(20m, 22m)],
            components[1].OriginalSourceRollRanges);
        Assert.All(components, component => Assert.Equal(resolution.ParsedModifierIndex, component.SourceModifierIndex));
    }

    [Fact]
    public void ShowOrUpdate_OnslaughtTwirlTransformedAffixesUseOriginalRangesForIdentityAndDisplayedValuesForBounds()
    {
        using var harness = ProductionPathHarness.Create();

        var snapshot = harness.OpenText(OnslaughtTwirlText);

        var life = Assert.Single(snapshot.Draft.ModifierFilters, component =>
            component.OriginalText.Contains("maximum Life", StringComparison.Ordinal));
        AssertTransformedScalar(life, -215m, 100m, 114m);

        var rarity = Assert.Single(snapshot.Draft.ModifierFilters, component =>
            component.OriginalText.Contains("Rarity of Items", StringComparison.Ordinal));
        AssertTransformedScalar(rarity, -52m, -25m, -28m);
        Assert.Equal(
            "<number>% increased Rarity of Items found",
            rarity.ProviderCanonicalSignature);

        var recoup = Assert.Single(snapshot.Draft.ModifierFilters, component =>
            component.OriginalText.Contains("Recouped as Life", StringComparison.Ordinal));
        AssertTransformedScalar(recoup, -29m, 13m, 15m);

        Assert.True(snapshot.AfterParse.IsMirrored);
        Assert.All([life, rarity, recoup], component =>
        {
            Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
            Assert.True(component.StatMappingProof is
                ModifierStatMappingProofStatus.ProvenExact or
                ModifierStatMappingProofStatus.WholeVector);
            Assert.False(component.IsVeiled);
            Assert.False(component.IsUnveiled);
            Assert.NotNull(component.ProviderCanonicalSignature);
        });
    }

    private static void AssertTransformedScalar(
        ResolvedSearchComponent component,
        decimal providerValue,
        decimal sourceMinimum,
        decimal sourceMaximum)
    {
        Assert.True(
            component.IsSearchable,
            $"Resolution={component.ResolutionStatus}; Id={component.ResolvedModifierId}; " +
            $"Reason={component.NotSearchableReason}; Bounds={component.ValueBoundsUnsupportedReason}; " +
            $"Shape={component.ValueBoundShape}; Signature={component.ProviderCanonicalSignature}");
        Assert.True(component.SupportsValueBounds, component.ValueBoundsUnsupportedReason);
        Assert.Equal([providerValue], component.CanonicalNumericValues);
        Assert.Equal(ModifierBoundDirection.Maximum, component.DefaultBoundDirection);
        Assert.Null(component.RequestedMinimum);
        Assert.Equal(providerValue, component.RequestedMaximum);
        Assert.Equal(
            [new ModifierSourceRollRange(sourceMinimum, sourceMaximum)],
            component.OriginalSourceRollRanges);
    }

    private static TradeSearchDraft WithExactTestProviderVariants(TradeSearchDraft draft)
    {
        return draft with
        {
            ModifierFilters = draft.ModifierFilters
                .Select((component, index) =>
                {
                    if (!component.IsSearchable ||
                        component.ParsedKind is not (ParsedModifierKind.Prefix or ParsedModifierKind.Suffix) ||
                        component.IsFractured ||
                        component.IsVeiled)
                    {
                        return component;
                    }

                    var providerStatId = $"explicit.test-provider.{index}";
                    var identity = PathOfExileTradeProviderIdentity.Create(providerStatId);
                    return component with
                    {
                        ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
                        ProviderStatId = providerStatId,
                        ProviderStatText = component.ProviderCanonicalSignature ?? component.CanonicalSignature,
                        FilterVariants =
                        [
                            new SearchFilterVariant
                            {
                                Identity = identity,
                                Label = "Explicit",
                                Description = "Exact test provider variant",
                                ProviderKind = "explicit",
                                SupportsValueBounds = component.SupportsValueBounds,
                            },
                        ],
                        SelectedFilterVariantIdentity = identity,
                        ProviderDiagnosticCode = null,
                        ProviderDiagnosticMessage = null,
                    };
                })
                .ToArray(),
        };
    }

    internal const string DemonIdolText = """
Item Class: Amulets
Rarity: Rare
Demon Idol
Lapis Amulet
--------
Requirements:
Level: 48
--------
Item Level: 85
--------
Allocates Sentinel (enchant)
--------
{ Implicit Modifier — Attribute }
+26(20-30) to Intelligence
--------
{ Prefix Modifier "Chosen" (Tier: 1) — Damage, Elemental, Cold, Lightning }
Adds 16(14-16) to 21(20-22) Cold Damage
Adds 15(14-16) to 20(20-22) Lightning Damage
{ Prefix Modifier "Sapphire" (Tier: 10) — Mana }
+30(30-34) to maximum Mana
{ Suffix Modifier "of Coals" (Tier: 4) — Damage, Elemental, Fire }
15(13-15)% increased Fire Damage
{ Suffix Modifier "of Joy" (Tier: 5) — Mana }
27(20-29)% increased Mana Regeneration Rate
""";

    internal const string OnslaughtTwirlText = """
Item Class: Rings
Rarity: Rare
Onslaught Twirl
Manifold Ring
--------
Quality (Life and Mana Modifiers): +20% (augmented)
--------
Requirements:
Level: 67
--------
Item Level: 84
--------
{ Implicit Modifier }
+1 Prefix Modifier allowed
-2 Suffix Modifiers allowed
Implicit Modifiers Cannot Be Changed — Unscalable Value
50% increased Prefix Modifier magnitudes — Unscalable Value
--------
{ Fractured Prefix Modifier "Glowing" (Tier: 8) — Defences, Energy Shield  — 50% Increased }
+29(13-15) to maximum Energy Shield
{ Prefix Modifier "Virile" (Tier: 1) — Life  — 70% Increased }
-215(100-114) to maximum Life
{ Prefix Modifier "Beryl" (Tier: 13) — Mana  — 70% Increased }
+31(15-19) to maximum Mana
{ Prefix Modifier "Perandus'" (Tier: 1) — Drop  — 50% Increased }
52(-25--28)% reduced Rarity of Items found
{ Suffix Modifier "of Fleshbinding" (Tier: 1) — Life  — 20% Increased }
-29(13-15)% of Damage taken Recouped as Life
(Only Damage from Hits can be Recouped, over 4 seconds following the Hit. Recouping negative amounts will cause loss)
--------
Mirrored
--------
Fractured Item
""";

    private sealed record ProductionPathSnapshot(
        ParsedItem AfterParse,
        ItemBaseResolutionResult BaseResolution,
        IReadOnlyList<ModifierCandidateResolutionResult> ModifierResolutions,
        TradeSearchDraft Draft,
        PriceCheckerWindowState WindowState,
        PriceCheckerSearchViewState SearchState);

    private static string DamageSuffix(string text)
    {
        return text.Contains("Cold Damage", StringComparison.Ordinal)
            ? "Cold Damage"
            : text.Contains("Fire Damage", StringComparison.Ordinal)
                ? "Fire Damage"
                : text.Contains("Lightning Damage", StringComparison.Ordinal)
                    ? "Lightning Damage"
                    : text;
    }

    internal const string HorrorManglerExplicitAndCraftedPhysicalDamageText = """
Item Class: One Hand Axes
Rarity: Rare
Horror Mangler
Reaver Axe
--------
One Handed Axe
Physical Damage: 94-283 (augmented)
Critical Strike Chance: 5.00%
Attacks per Second: 1.30
Weapon Range: 1.1 metres
--------
Requirements:
Level: 61
Str: 167
Dex: 57
--------
Item Level: 85
--------
{ Prefix Modifier "Reaver's" (Tier: 3) - Damage, Physical, Attack }
30(25-34)% increased Physical Damage
+60(47-72) to Accuracy Rating
{ Master Crafted Prefix Modifier "Upgraded" (Rank: 4) - Damage, Physical, Attack }
116(100-129)% increased Physical Damage
""";

    internal const string GoldenBucklerText = """
Item Class: Shields
Rarity: Normal
Golden Buckler
--------
Chance to Block: 40%
Evasion Rating: 354
--------
Requirements:
Level: 54
Dex: 130
--------
Item Level: 84
--------
{ Prefix Modifier "Warded" (Tier: 4) - Block }
62(58-63)% increased Chance to Block
""";

    private static void AssertExactProperty(
        TradeSearchDraft draft,
        TradeSearchItemPropertyKind kind)
    {
        var property = Assert.Single(draft.ItemProperties, property => property.Kind == kind);
        Assert.Equal(TradeSearchItemPropertyProviderResolutionStatus.Exact, property.ProviderResolutionStatus);
        Assert.True(property.IsSearchable);
        Assert.Null(property.NotSearchableReason);
    }

    private sealed class ProductionPathHarness : IDisposable
    {
        private readonly TempDirectory tempDirectory;
        private readonly ParsedItemGameDataDisplayService gameDataDisplayService = new();
        private readonly ItemTextParser parser = new();
        private readonly FakeWindowFactory windowFactory;
        private readonly PriceCheckerSearchController searchController;
        private readonly FakePriceCheckService priceCheckService;

        private ProductionPathHarness(
            TempDirectory tempDirectory,
            GameDataCatalog catalog,
            PriceCheckerWindowController controller,
            FakeWindowFactory windowFactory,
            PriceCheckerSearchController searchController,
            FakePriceCheckService priceCheckService)
        {
            this.tempDirectory = tempDirectory;
            Catalog = catalog;
            Controller = controller;
            this.windowFactory = windowFactory;
            this.searchController = searchController;
            this.priceCheckService = priceCheckService;
        }

        private GameDataCatalog Catalog { get; }

        private PriceCheckerWindowController Controller { get; }

        public int SearchCount => priceCheckService.SearchCount;

        public static ProductionPathHarness Create()
        {
            var tempDirectory = TempDirectory.Create();
            var windowFactory = new FakeWindowFactory();
            var priceCheckService = new FakePriceCheckService();
            var searchController = new PriceCheckerSearchController(
                priceCheckService,
                global::PoEnhance.App.Infrastructure.Settings.ApplicationLeagueSetting.CreateTransient("Mirage"));
            var controller = new PriceCheckerWindowController(
                new FakeBoundsProvider(),
                new PriceCheckerPlacementCalculator(),
                new PriceCheckerPlacementStore(Path.Combine(tempDirectory.Path, "placement.json")),
                windowFactory,
                new CoreTradeSearchDraftMapperAdapter(),
                new CoreTradeSearchDraftValidatorAdapter(),
                new FakeForegroundWindowDetector(),
                new FakeDeferredActionScheduler(),
                searchController);

            return new ProductionPathHarness(
                tempDirectory,
                LoadGameDataCatalog(),
                controller,
                windowFactory,
                searchController,
                priceCheckService);
        }

        public ProductionPathSnapshot OpenFixture(int fixtureIndex)
        {
            return OpenText(CopiedItemCorpus.LoadItems()[fixtureIndex]);
        }

        public ProductionPathSnapshot OpenText(string itemText)
        {
            var parsed = parser.Parse(itemText);
            var baseResolution = gameDataDisplayService.ResolveItemBase(parsed, Catalog).Result;
            Assert.NotNull(baseResolution);
            var modifierResolutions = gameDataDisplayService
                .ResolveModifierCandidates(parsed, Catalog, baseResolution)
                .Results
                .Select(display => display.Result)
                .OfType<ModifierCandidateResolutionResult>()
                .ToArray();

            var updateResult = Controller.ShowOrUpdate(
                parsed,
                baseResolution,
                modifierResolutions,
                Catalog);

            Assert.True(updateResult.IsSuccess, updateResult.Diagnostic);
            var window = Assert.Single(windowFactory.CreatedWindows);
            Assert.NotNull(window.CurrentState);
            Assert.NotNull(window.CurrentSearchState);

            return new ProductionPathSnapshot(
                parsed,
                baseResolution,
                modifierResolutions,
                window.CurrentState!.Draft,
                window.CurrentState,
                window.CurrentSearchState!);
        }

        public PriceCheckerSearchViewState ExpandProperty(TradeSearchItemPropertyKind kind)
        {
            var window = Assert.Single(windowFactory.CreatedWindows);
            var property = Assert.Single(window.CurrentSearchState!.ItemProperties, candidate =>
                candidate.Kind == kind);
            searchController.UpdateItemPropertyExpansion(property.SourceIndex, isExpanded: true);
            return window.CurrentSearchState!;
        }

        public PriceCheckerSearchViewState SelectModifier(int sourceIndex, bool selected)
        {
            searchController.UpdateModifierSelection(sourceIndex, selected);
            return Assert.Single(windowFactory.CreatedWindows).CurrentSearchState!;
        }

        public PriceCheckerSearchViewState ApplyResolvedDraft(TradeSearchDraft draft)
        {
            searchController.UpdateCurrentDraft(draft, new TradeSearchDraftValidator().Validate(draft));
            return Assert.Single(windowFactory.CreatedWindows).CurrentSearchState!;
        }

        public PriceCheckerSearchViewState SelectProperty(
            TradeSearchItemPropertyKind kind,
            bool selected)
        {
            var window = Assert.Single(windowFactory.CreatedWindows);
            var property = Assert.Single(window.CurrentSearchState!.ItemProperties, candidate =>
                candidate.Kind == kind);
            searchController.UpdateItemPropertySelection(property.SourceIndex, selected);
            return window.CurrentSearchState!;
        }

        public void Dispose()
        {
            tempDirectory.Dispose();
        }
    }

    private static GameDataCatalog LoadGameDataCatalog()
    {
        var packagePath = FindRepoFile("artifacts", "poenhance-game-data.json");
        var result = GameDataPackageLoader
            .LoadFromFileAsync(packagePath)
            .GetAwaiter()
            .GetResult();

        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Code)));
        Assert.NotNull(result.Package);
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

        throw new FileNotFoundException($"Could not find repo file: {Path.Combine(relativeParts)}");
    }

    private static class CopiedItemCorpus
    {
        private static readonly Regex ItemBoundary = new(
            @"\r?\n\s*\r?\n(?=Item Class:)",
            RegexOptions.CultureInvariant);

        public static IReadOnlyList<string> LoadItems()
        {
            var corpusPath = FindRepoFile("PoEnhance.Core.Tests", "TestData", "Items", "advanced-real-items-corpus.txt");
            var corpus = File.ReadAllText(corpusPath);
            var items = ItemBoundary
                .Split(corpus.TrimEnd('\r', '\n'))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();

            Assert.Equal(15, items.Length);
            return items;
        }
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

        public void UpdateContent(PriceCheckerWindowState state)
        {
            CurrentState = state;
        }

        public void UpdateSearch(PriceCheckerSearchViewState state)
        {
            CurrentSearchState = state;
        }

        public void ApplyPlacement(PriceCheckerPlacement placement)
        {
            CurrentPlacement = placement;
        }

        public void ShowInactive()
        {
        }

        public void Close()
        {
            IsClosed = true;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
#pragma warning restore CS0067

    private sealed class FakeWindowFactory : IPriceCheckerWindowFactory
    {
        public List<FakeWindow> CreatedWindows { get; } = [];

        public IPriceCheckerWindow CreateWindow()
        {
            var window = new FakeWindow();
            CreatedWindows.Add(window);
            return window;
        }
    }

    private sealed class FakeBoundsProvider : IPathOfExileClientBoundsProvider
    {
        public bool TryGetClientBounds(out PathOfExileClientBounds clientBounds)
        {
            clientBounds = new PathOfExileClientBounds(
                Left: 100,
                Top: 50,
                Width: 1000,
                Height: 800,
                DisplayDeviceName: @"\\.\DISPLAY1",
                DpiScaleX: 1,
                DpiScaleY: 1);
            return true;
        }
    }

    private sealed class FakeForegroundWindowDetector : IPathOfExileForegroundWindowDetector
    {
        public bool IsPathOfExileForegroundWindow() => true;
    }

    private sealed class FakeDeferredActionScheduler : IPriceCheckerDeferredActionScheduler
    {
        public void Schedule(Action action)
        {
        }
    }

    private sealed class FakePriceCheckService : IPathOfExileTradePriceCheckService
    {
        public int SearchCount { get; private set; }

        public Task<PathOfExileTradeFilterCatalogProviderResult> InitializeFilterCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PathOfExileTradeFilterCatalogProviderResult());

        public TradeSearchDraft ResolveEffectiveDraft(TradeSearchDraft draft) => draft;

        public Task<string?> LoadCategoryDisplayLabelAsync(
            TradeSearchDraft draft,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<PathOfExileTradePriceCheckResult> CheckAsync(
            TradeSearchDraft? draft,
            TradeSearchValidationResult? validationResult,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default)
        {
            SearchCount++;
            return Task.FromResult(new PathOfExileTradePriceCheckResult
            {
                IsSuccess = true,
                Stage = PathOfExileTradePriceCheckStage.Completed,
            });
        }

        public Task<PathOfExileTradePriceCheckResult> FetchMoreAsync(
            string? searchQueryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Load More is not expected in this corpus test.");
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"poenhance-production-path-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
