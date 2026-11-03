using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

/// <summary>
/// Stage 03.11: reliable multi-effect modifier projections with shared physical-source
/// provenance. Clipboard fixtures are SYNTHETIC (assembled to mirror the Advanced Copy shape of
/// real GameData modifiers; no live item was captured) and resolve against the real repository
/// GameData package so composite stat ids, translations and localities are authentic.
/// </summary>
public sealed class MultiEffectModifierProjectorTests
{
    private const string FireResistanceSignature = "+<number>% to Fire Resistance";
    private const string ChaosResistanceSignature = "+<number>% to Chaos Resistance";

    private static readonly Lazy<Task<GameDataCatalog>> CatalogLoad = new(LoadRepoCatalogAsync);

    // ---------------------------------------------------------------------------------------
    // T1 - Fire + Fire&Chaos (crafted one-line, and non-crafted abyss jewel pair).
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task CraftedFireAndChaos_WithPureFire_ProjectsFireAggregateAndChaosFromOnePhysicalSource()
    {
        var draft = await DraftAsync(Ring(
            """
            { Suffix Modifier "of the Whelpling" (Tier: 8) }
            +10(6-11)% to Fire Resistance
            { Master Crafted Suffix Modifier "of Craft" (Rank: 2) }
            +10(9-10)% to Fire and Chaos Resistances
            """));

        Assert.DoesNotContain(
            draft.ModifierFilters,
            component => component.OriginalText.Contains("Fire and Chaos", StringComparison.Ordinal));
        var fire = Assert.Single(draft.ModifierFilters, component =>
            component.CanonicalSignature == FireResistanceSignature);
        var chaos = Assert.Single(draft.ModifierFilters, component =>
            component.CanonicalSignature == ChaosResistanceSignature);

        Assert.Equal(2, fire.Sources.Count);
        Assert.Equal([20m], fire.ObservedNumericValues);
        Assert.Equal("+20% to Fire Resistance", fire.OriginalText);
        Assert.Equal([10m], chaos.ObservedNumericValues);
        Assert.Equal("+10% to Chaos Resistance", chaos.OriginalText);

        var sharedSourceInFire = Assert.Single(fire.Contributors, contributor =>
            contributor.Source.CanonicalSignature.Contains("Fire and Chaos", StringComparison.Ordinal))
            .Source;
        var sharedSourceInChaos = Assert.Single(chaos.Sources);
        Assert.False(string.IsNullOrEmpty(sharedSourceInFire.PhysicalSourceIdentity));
        Assert.Equal(sharedSourceInFire.PhysicalSourceIdentity, sharedSourceInChaos.PhysicalSourceIdentity);
        Assert.StartsWith(sharedSourceInChaos.PhysicalSourceIdentity + ":logic:", chaos.PhysicalSourceIdentity, StringComparison.Ordinal);
        Assert.Equal("+<number>% to Fire and Chaos Resistances", sharedSourceInChaos.CanonicalSignature);
        Assert.Equal(["fire_and_chaos_damage_resistance_%"], sharedSourceInChaos.ResolvedStatIds);
        Assert.Equal(["base_chaos_damage_resistance_%"], chaos.ResolvedStatIds);
        Assert.True(sharedSourceInChaos.IsCrafted);
        // Logical criterion clears Crafted so Trade discovery uses Pseudo/Explicit totals;
        // physical Sources retain Crafted provenance.
        Assert.False(chaos.IsCrafted);
        // Logical Chaos row discovers Pseudo/Explicit for Chaos, not the combined affix.
        Assert.Null(chaos.ProviderCanonicalSignature);
        // Fire aggregate may keep ordinary Fire provider evidence, never Fire-and-Chaos combined.
        Assert.DoesNotContain("Chaos", fire.ProviderCanonicalSignature ?? string.Empty, StringComparison.Ordinal);
        // Aggregate parents describe several physical sources and never carry one identity.
        Assert.Equal(string.Empty, fire.PhysicalSourceIdentity);
    }

    [Fact]
    public async Task AbyssJewelFireAndCold_WithPureCold_ProjectsColdAggregateAndFireFromOnePhysicalSource()
    {
        var draft = await DraftAsync(AbyssJewel(
            """
            { Suffix Modifier "of the Hearth" }
            +11(10-12)% to Fire and Cold Resistances
            { Suffix Modifier "of the Beast" }
            +14(12-15)% to Cold Resistance
            """));

        var fire = Assert.Single(draft.ModifierFilters, component =>
            component.CanonicalSignature == FireResistanceSignature);
        var cold = Assert.Single(draft.ModifierFilters, component =>
            component.CanonicalSignature == "+<number>% to Cold Resistance");
        Assert.Equal([11m], fire.ObservedNumericValues);
        Assert.Equal([25m], cold.ObservedNumericValues);
        Assert.Equal(2, cold.Sources.Count);
        Assert.False(fire.IsCrafted);
        var sharedSource = Assert.Single(cold.Contributors, contributor =>
                contributor.Source.ResolvedStatIds.SequenceEqual(["fire_and_cold_damage_resistance_%"]))
            .Source;
        Assert.Equal(sharedSource.PhysicalSourceIdentity, Assert.Single(fire.Sources).PhysicalSourceIdentity);
        Assert.StartsWith(sharedSource.PhysicalSourceIdentity + ":logic:", fire.PhysicalSourceIdentity, StringComparison.Ordinal);
        Assert.DoesNotContain(
            draft.ModifierFilters,
            component => component.OriginalText.Contains("Fire and Cold", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------
    // T2 - table-driven pair coverage (real GameData stat ids, crafted and non-crafted).
    // ---------------------------------------------------------------------------------------

    public static TheoryData<string, string, string, string, string> RealCraftedPairCases => new()
    {
        // copied line, logical A signature, B signature, stat A, stat B
        { "+10(9-10)% to Cold and Chaos Resistances",
            "+<number>% to Cold Resistance", ChaosResistanceSignature,
            "base_cold_damage_resistance_%", "base_chaos_damage_resistance_%" },
        { "+10(9-10)% to Lightning and Chaos Resistances",
            "+<number>% to Lightning Resistance", ChaosResistanceSignature,
            "base_lightning_damage_resistance_%", "base_chaos_damage_resistance_%" },
        { "+11(10-12)% to Fire and Cold Resistances",
            FireResistanceSignature, "+<number>% to Cold Resistance",
            "base_fire_damage_resistance_%", "base_cold_damage_resistance_%" },
        { "+11(10-12)% to Fire and Lightning Resistances",
            FireResistanceSignature, "+<number>% to Lightning Resistance",
            "base_fire_damage_resistance_%", "base_lightning_damage_resistance_%" },
        { "+11(10-12)% to Cold and Lightning Resistances",
            "+<number>% to Cold Resistance", "+<number>% to Lightning Resistance",
            "base_cold_damage_resistance_%", "base_lightning_damage_resistance_%" },
        { "+12(10-15) to Dexterity and Intelligence",
            "+<number> to Dexterity", "+<number> to Intelligence",
            "additional_dexterity", "additional_intelligence" },
    };

    [Theory]
    [MemberData(nameof(RealCraftedPairCases))]
    public async Task CraftedGroupAPairs_SplitIntoTwoLogicalRowsWithoutCombinedTopLevelRow(
        string line,
        string firstSignature,
        string secondSignature,
        string firstStat,
        string secondStat)
    {
        var draft = await DraftAsync(Ring(
            $"{{ Master Crafted Suffix Modifier \"of Craft\" (Rank: 1) }}\n{line}"));

        var rows = OrdinaryRows(draft);
        Assert.Equal(2, rows.Length);
        var first = Assert.Single(rows, component => component.CanonicalSignature == firstSignature);
        var second = Assert.Single(rows, component => component.CanonicalSignature == secondSignature);
        Assert.Equal([firstStat], first.ResolvedStatIds);
        Assert.Equal([secondStat], second.ResolvedStatIds);
        Assert.Equal(first.ObservedNumericValues, second.ObservedNumericValues);
        Assert.False(string.IsNullOrEmpty(first.PhysicalSourceIdentity));
        Assert.NotEqual(first.PhysicalSourceIdentity, second.PhysicalSourceIdentity);
        Assert.NotEqual(first.ComponentId, second.ComponentId);
        Assert.NotEqual(first.SourceComponentIndex, second.SourceComponentIndex);
        var sharedPhysical = Assert.Single(first.Sources).PhysicalSourceIdentity;
        Assert.All(rows, component =>
        {
            // Logical row clears Crafted for Pseudo/Explicit discovery; Sources keep it.
            Assert.False(component.IsCrafted);
            Assert.Null(component.ReviewedItemPropertySemantic);
            Assert.Null(component.ProviderCanonicalSignature);
            var source = Assert.Single(component.Sources);
            Assert.True(source.IsCrafted);
            Assert.Equal(sharedPhysical, source.PhysicalSourceIdentity);
            Assert.StartsWith(sharedPhysical + ":logic:", component.PhysicalSourceIdentity, StringComparison.Ordinal);
            Assert.Contains(" to ", source.OriginalText, StringComparison.Ordinal);
            Assert.Contains(" and ", source.OriginalText, StringComparison.Ordinal);
            Assert.NotNull(component.ResolvedModifierId);
            Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
        });
    }

    [Theory]
    [InlineData("of Insulation", "+11(10-12)% to Fire and Lightning Resistances", "+<number>% to Fire Resistance", "+<number>% to Lightning Resistance")]
    [InlineData("of Shelter", "+11(10-12)% to Cold and Lightning Resistances", "+<number>% to Cold Resistance", "+<number>% to Lightning Resistance")]
    [InlineData("of Athletics", "+9(8-10) to Strength and Dexterity", "+<number> to Strength", "+<number> to Dexterity")]
    [InlineData("of Spirit", "+9(8-10) to Strength and Intelligence", "+<number> to Strength", "+<number> to Intelligence")]
    [InlineData("of Cunning", "+9(8-10) to Dexterity and Intelligence", "+<number> to Dexterity", "+<number> to Intelligence")]
    public async Task NonCraftedAbyssPairs_SplitAndKeepNonCraftedProvenance(
        string modifierName,
        string line,
        string firstSignature,
        string secondSignature)
    {
        var draft = await DraftAsync(AbyssJewel($"{{ Suffix Modifier \"{modifierName}\" }}\n{line}"));

        Assert.Equal(2, draft.ModifierFilters.Count);
        Assert.Equal(
            [firstSignature, secondSignature],
            draft.ModifierFilters.Select(component => component.CanonicalSignature));
        Assert.All(draft.ModifierFilters, component =>
        {
            Assert.False(component.IsCrafted);
            Assert.Equal("Explicit", Assert.Single(component.Sources).ProviderDomain);
        });
        Assert.Equal(2, draft.ModifierFilters.Select(component => component.PhysicalSourceIdentity).Distinct().Count());
        Assert.Single(draft.ModifierFilters.SelectMany(component => component.Sources)
            .Select(source => source.PhysicalSourceIdentity)
            .Distinct());
    }

    // ---------------------------------------------------------------------------------------
    // T3 - compact parents; no double count with ordinary Fire / Strength.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task AllAttributes_StaysOneCompactParentAndNeverFoldsIntoStrength()
    {
        var draft = await DraftAsync(AbyssJewel(
            """
            { Suffix Modifier "of Adaption" }
            +7(6-8) to all Attributes
            { Suffix Modifier "of Strength" }
            +14(12-16) to Strength
            """));

        Assert.Equal(2, draft.ModifierFilters.Count);
        var parent = Assert.Single(draft.ModifierFilters, component =>
            component.ResolvedStatIds.SequenceEqual(["additional_all_attributes"]));
        var strength = Assert.Single(draft.ModifierFilters, component =>
            component.ResolvedStatIds.SequenceEqual(["additional_strength"]));
        Assert.Equal(
            ["additional_strength", "additional_dexterity", "additional_intelligence"],
            parent.LogicalContributions.Select(logical => logical.LogicalStatId));
        Assert.Equal(
            ["+7 to Strength", "+7 to Dexterity", "+7 to Intelligence"],
            parent.LogicalContributions.Select(logical => logical.DisplayText));
        Assert.All(parent.LogicalContributions, logical =>
        {
            Assert.StartsWith(
                parent.PhysicalSourceIdentity + ":logic:",
                logical.PhysicalSourceIdentity,
                StringComparison.Ordinal);
            Assert.Equal([7m], logical.CanonicalNumericValues);
            Assert.False(logical.IsSelected);
        });
        // Strength is 14, not 21: the compact parent is never folded into the ordinary aggregate.
        Assert.Equal([14m], strength.ObservedNumericValues);
        Assert.Single(strength.Sources);
        Assert.Empty(strength.LogicalContributions);
    }

    [Fact]
    public async Task AllElementalResistances_StaysOneCompactParentAndNeverFoldsIntoFire()
    {
        var draft = await DraftAsync(AbyssJewel(
            """
            { Suffix Modifier "of Resistance" }
            +9(8-10)% to all Elemental Resistances
            { Suffix Modifier "of the Dragon" }
            +14(12-15)% to Fire Resistance
            """));

        Assert.Equal(2, draft.ModifierFilters.Count);
        var allElemental = Assert.Single(draft.ModifierFilters, component =>
            component.ResolvedStatIds.SequenceEqual(["base_resist_all_elements_%"]));
        var fire = Assert.Single(draft.ModifierFilters, component =>
            component.ResolvedStatIds.SequenceEqual(["base_fire_damage_resistance_%"]));

        Assert.Equal("+9(8-10)% to all Elemental Resistances", allElemental.OriginalText);
        Assert.Equal([9m], allElemental.ObservedNumericValues);
        Assert.Equal(
            ["+9% to Fire Resistance", "+9% to Cold Resistance", "+9% to Lightning Resistance"],
            allElemental.LogicalContributions.Select(logical => logical.DisplayText));
        Assert.Equal(
            [
                "base_fire_damage_resistance_%",
                "base_cold_damage_resistance_%",
                "base_lightning_damage_resistance_%",
            ],
            allElemental.LogicalContributions.Select(logical => logical.LogicalStatId));
        // Fire is 14, not 23: the compact parent is never folded into the ordinary Fire aggregate.
        Assert.Equal([14m], fire.ObservedNumericValues);
        Assert.Single(fire.Sources);
        Assert.Empty(fire.LogicalContributions);
        Assert.NotEqual(allElemental.PhysicalSourceIdentity, fire.PhysicalSourceIdentity);
    }

    // ---------------------------------------------------------------------------------------
    // Projector-level synthetic contract (SYNTHETIC components, no catalog needed).
    // ---------------------------------------------------------------------------------------

    public static TheoryData<string, string, string, string, string, string> SyntheticSplitCases => new()
    {
        { "fire_and_chaos_damage_resistance_%", "+<number>% to Fire and Chaos Resistances",
            "base_fire_damage_resistance_%", "base_chaos_damage_resistance_%", FireResistanceSignature, ChaosResistanceSignature },
        { "cold_and_chaos_damage_resistance_%", "+<number>% to Cold and Chaos Resistances",
            "base_cold_damage_resistance_%", "base_chaos_damage_resistance_%", "+<number>% to Cold Resistance", ChaosResistanceSignature },
        { "lightning_and_chaos_damage_resistance_%", "+<number>% to Lightning and Chaos Resistances",
            "base_lightning_damage_resistance_%", "base_chaos_damage_resistance_%", "+<number>% to Lightning Resistance", ChaosResistanceSignature },
        { "fire_and_cold_damage_resistance_%", "+<number>% to Fire and Cold Resistances",
            "base_fire_damage_resistance_%", "base_cold_damage_resistance_%", FireResistanceSignature, "+<number>% to Cold Resistance" },
        { "fire_and_lightning_damage_resistance_%", "+<number>% to Fire and Lightning Resistances",
            "base_fire_damage_resistance_%", "base_lightning_damage_resistance_%", FireResistanceSignature, "+<number>% to Lightning Resistance" },
        { "cold_and_lightning_damage_resistance_%", "+<number>% to Cold and Lightning Resistances",
            "base_cold_damage_resistance_%", "base_lightning_damage_resistance_%", "+<number>% to Cold Resistance", "+<number>% to Lightning Resistance" },
        { "additional_strength_and_dexterity", "+<number> to Strength and Dexterity",
            "additional_strength", "additional_dexterity", "+<number> to Strength", "+<number> to Dexterity" },
        { "additional_strength_and_intelligence", "+<number> to Strength and Intelligence",
            "additional_strength", "additional_intelligence", "+<number> to Strength", "+<number> to Intelligence" },
        { "additional_dexterity_and_intelligence", "+<number> to Dexterity and Intelligence",
            "additional_dexterity", "additional_intelligence", "+<number> to Dexterity", "+<number> to Intelligence" },
    };

    [Theory]
    [MemberData(nameof(SyntheticSplitCases))]
    public void Project_SyntheticApprovedPair_SplitsPreservingPhysicalProvenanceAndFlags(
        string compositeStatId,
        string compositeSignature,
        string firstStat,
        string secondStat,
        string firstSignature,
        string secondSignature)
    {
        foreach (var crafted in new[] { false, true })
        {
            var composite = Synthetic(compositeStatId, compositeSignature, 15m) with
            {
                IsCrafted = crafted,
                IsFractured = false,
            };

            var projected = MultiEffectModifierProjector.Project([composite]);

            Assert.Equal(2, projected.Count);
            Assert.Equal([firstStat, secondStat], projected.Select(component => Assert.Single(component.ResolvedStatIds)));
            Assert.Equal([firstSignature, secondSignature], projected.Select(component => component.CanonicalSignature));
            Assert.All(projected, component =>
            {
                Assert.False(component.IsCrafted);
                Assert.Equal(component.CanonicalSignature, component.CanonicalSignature.Trim());
                Assert.Equal(composite.ResolvedModifierId, component.ResolvedModifierId);
                Assert.Equal(composite.ResolutionStatus, component.ResolutionStatus);
                Assert.Equal([15m], component.ObservedNumericValues);
                Assert.Equal([15m], component.CanonicalNumericValues);
                Assert.Equal(15m, component.RequestedMinimum);
                Assert.Null(component.ProviderCanonicalSignature);
                Assert.Null(component.ReviewedItemPropertySemantic);
                var source = Assert.Single(component.Sources);
                Assert.Equal(compositeSignature, source.CanonicalSignature);
                Assert.Equal(composite.OriginalText, source.OriginalText);
                Assert.Equal([compositeStatId], source.ResolvedStatIds);
                Assert.Equal(crafted, source.IsCrafted);
                Assert.StartsWith(
                    source.PhysicalSourceIdentity + ":logic:",
                    component.PhysicalSourceIdentity,
                    StringComparison.Ordinal);
            });
            Assert.Equal(2, projected.Select(component => component.PhysicalSourceIdentity).Distinct().Count());
            Assert.Single(projected.SelectMany(component => component.Sources)
                .Select(source => source.PhysicalSourceIdentity)
                .Distinct());
            Assert.Equal(2, projected.Select(component => component.ComponentId).Distinct().Count());
            Assert.Equal(2, projected.Select(component => component.SourceComponentIndex).Distinct().Count());
        }
    }

    [Theory]
    [InlineData("base_resist_all_elements_%", "+<number>% to all Elemental Resistances", 3)]
    [InlineData("additional_all_attributes", "+<number> to all Attributes", 3)]
    public void Project_SyntheticCompactGroups_KeepOneParentWithCompositeStatsAndThreeChildren(
        string compositeStatId,
        string signature,
        int expectedChildren)
    {
        var composite = Synthetic(compositeStatId, signature, 9m);

        var projected = MultiEffectModifierProjector.Project([composite]);

        var parent = Assert.Single(projected);
        Assert.Equal([compositeStatId], parent.ResolvedStatIds);
        Assert.Equal(signature, parent.CanonicalSignature);
        Assert.Equal(composite.OriginalText, parent.OriginalText);
        Assert.Equal(expectedChildren, parent.LogicalContributions.Count);
        Assert.All(parent.LogicalContributions, logical =>
        {
            Assert.StartsWith(
                parent.PhysicalSourceIdentity + ":logic:",
                logical.PhysicalSourceIdentity,
                StringComparison.Ordinal);
            Assert.Equal([9m], logical.CanonicalNumericValues);
        });
        Assert.Empty(parent.Sources);
    }

    [Fact]
    public void Project_SyntheticAllResistancesIncludingChaosAndUnapprovedStats_AreKeptUntouched()
    {
        var allResistances = Synthetic("resist_all_%", "<number>% reduced Enemy Resistances", 5m);
        var unrelated = Synthetic("base_maximum_life", "+<number> to maximum Life", 40m);

        var projected = MultiEffectModifierProjector.Project([allResistances, unrelated]);

        Assert.Equal([allResistances, unrelated], projected);
        Assert.All(projected, component => Assert.Equal(string.Empty, component.PhysicalSourceIdentity));
    }

    [Fact]
    public void Project_CraftedLightningAndChaos_WithEmptyResolvedStatIds_FallsBackToCanonicalSignature()
    {
        var composite = Synthetic(
            "lightning_and_chaos_damage_resistance_%",
            "+<number>% to Lightning and Chaos Resistances",
            14m) with
        {
            IsCrafted = true,
            ResolvedStatIds = [],
        };

        var projected = MultiEffectModifierProjector.Project([composite]);

        Assert.Equal(2, projected.Count);
        Assert.Equal(
            ["base_lightning_damage_resistance_%", "base_chaos_damage_resistance_%"],
            projected.Select(component => Assert.Single(component.ResolvedStatIds)));
        Assert.All(projected, component =>
        {
            Assert.False(component.IsCrafted);
            Assert.True(Assert.Single(component.Sources).IsCrafted);
            Assert.Contains(":logic:", component.PhysicalSourceIdentity, StringComparison.Ordinal);
        });
        Assert.Single(projected.SelectMany(component => component.Sources)
            .Select(source => source.PhysicalSourceIdentity)
            .Distinct());
    }

    [Fact]
    public async Task CraftedLightningAndChaos_OneLine_SplitsIntoTwoLogicalRowsWithoutCombinedTopLevel()
    {
        var draft = await DraftAsync(Ring(
            """
            { Master Crafted Suffix Modifier "of Craft" (Rank: 1) }
            +14(9-10)% to Lightning and Chaos Resistances
            """));

        var rows = OrdinaryRows(draft);
        Assert.Equal(2, rows.Length);
        Assert.DoesNotContain(
            rows,
            component => component.OriginalText.Contains("Lightning and Chaos", StringComparison.Ordinal));
        var lightning = Assert.Single(rows, component =>
            component.CanonicalSignature == "+<number>% to Lightning Resistance");
        var chaos = Assert.Single(rows, component =>
            component.CanonicalSignature == ChaosResistanceSignature);
        Assert.Equal([14m], lightning.ObservedNumericValues);
        Assert.Equal([14m], chaos.ObservedNumericValues);
        Assert.False(lightning.IsCrafted);
        Assert.False(chaos.IsCrafted);
        Assert.True(Assert.Single(lightning.Sources).IsCrafted);
        Assert.Equal(
            Assert.Single(lightning.Sources).PhysicalSourceIdentity,
            Assert.Single(chaos.Sources).PhysicalSourceIdentity);
    }

    [Theory]
    [InlineData(true, ModifierCandidateResolutionStatus.Exact)]
    [InlineData(false, ModifierCandidateResolutionStatus.Probable)]
    public void Project_SyntheticBaseImplicitAndUnresolvedComposites_AreNotProjected(
        bool isBaseImplicit,
        ModifierCandidateResolutionStatus status)
    {
        var composite = Synthetic(
            "fire_and_chaos_damage_resistance_%",
            "+<number>% to Fire and Chaos Resistances",
            15m) with
        {
            IsBaseImplicit = isBaseImplicit,
            ResolutionStatus = status,
        };

        var projected = MultiEffectModifierProjector.Project([composite]);

        Assert.Equal([composite], projected);
    }

    [Fact]
    public void Project_SyntheticMultiLineSiblingsKeepDistinctComponentIndexes()
    {
        var composite = Synthetic(
            "fire_and_chaos_damage_resistance_%",
            "+<number>% to Fire and Chaos Resistances",
            15m) with
        {
            SourceModifierIndex = 3,
            SourceLineIndex = 0,
            SourceComponentIndex = 0,
        };
        var sibling = Synthetic("base_maximum_life", "+<number> to maximum Life", 40m) with
        {
            ComponentId = "modifier:3:1",
            SourceModifierIndex = 3,
            SourceLineIndex = 1,
            SourceComponentIndex = 1,
        };

        var projected = MultiEffectModifierProjector.Project([composite, sibling]);

        Assert.Equal(3, projected.Count);
        Assert.Equal(3, projected.Select(component => component.SourceComponentIndex).Distinct().Count());
        Assert.Equal(3, projected.Select(component => component.ComponentId).Distinct().Count());
        Assert.StartsWith("phys:3:0:modifier:0:0:logic:", projected[0].PhysicalSourceIdentity, StringComparison.Ordinal);
    }

    [Fact]
    public void Aggregate_SyntheticProjectedFireAndPureFire_SumsToOneParentWithBothSourcesAndNoProviderLeak()
    {
        var pureFire = Synthetic(
            "base_fire_damage_resistance_%",
            FireResistanceSignature,
            30m) with
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            ProviderCanonicalSignature = FireResistanceSignature,
        };
        var composite = Synthetic(
            "fire_and_chaos_damage_resistance_%",
            "+<number>% to Fire and Chaos Resistances",
            15m) with
        {
            ComponentId = "modifier:1:0",
            SourceModifierIndex = 1,
        };

        // Projected component first: the aggregate must not inherit the composite provider evidence.
        var projected = MultiEffectModifierProjector.Project([composite, pureFire]);
        var aggregation = CanonicalModifierEffectAggregator.Aggregate(projected);

        Assert.Equal(2, aggregation.Components.Count);
        var fire = Assert.Single(aggregation.Components, component =>
            component.CanonicalSignature == FireResistanceSignature);
        var chaos = Assert.Single(aggregation.Components, component =>
            component.CanonicalSignature == ChaosResistanceSignature);
        Assert.Equal([45m], fire.ObservedNumericValues);
        Assert.Equal(2, fire.Contributors.Count);
        Assert.NotEqual(FireAndChaosProviderSignature, fire.ProviderCanonicalSignature);
        Assert.Empty(fire.ProviderSearchSignatures);
        Assert.Equal([15m], chaos.ObservedNumericValues);
        var shared = Assert.Single(fire.Contributors, contributor => contributor.Source.ResolvedStatIds
            .SequenceEqual(["fire_and_chaos_damage_resistance_%"])).Source;
        Assert.Equal(shared.PhysicalSourceIdentity, Assert.Single(chaos.Sources).PhysicalSourceIdentity);
        Assert.StartsWith(shared.PhysicalSourceIdentity + ":logic:", chaos.PhysicalSourceIdentity, StringComparison.Ordinal);
        Assert.Null(chaos.ProviderCanonicalSignature);
    }

    private const string FireAndChaosProviderSignature = "+<number>% to Fire and Chaos Resistances";

    private static ResolvedSearchComponent Synthetic(
        string statId,
        string signature,
        decimal value)
    {
        return new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            SourceComponentIndex = 0,
            OriginalText = CanonicalModifierEffectAggregator.RenderAggregateText(signature, [value]),
            CanonicalSignature = signature,
            ParsedKind = ParsedModifierKind.Suffix,
            GenerationType = ModifierGenerationType.Suffix,
            Locality = ModifierLocality.Global,
            StatMappingProof = ModifierStatMappingProofStatus.ProvenExact,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = $"synthetic.{statId}",
            ResolvedStatIds = [statId],
            IsSearchable = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = [value],
            CanonicalNumericValues = [value],
            RequestedMinimum = value,
            DefaultBoundDirection = ModifierBoundDirection.Minimum,
        };
    }

    /// <summary>The Unset Ring base carries a base-implicit socket row that is not under test.</summary>
    private static ResolvedSearchComponent[] OrdinaryRows(TradeSearchDraft draft) =>
        draft.ModifierFilters.Where(component => !component.IsBaseImplicit).ToArray();

    private static string Ring(string modifiers) => $$"""
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
        {{modifiers}}
        """;

    private static string AbyssJewel(string modifiers) => $$"""
        Item Class: Abyss Jewels
        Rarity: Rare
        Synthetic Eye
        Murderous Eye Jewel
        --------
        Item Level: 85
        --------
        {{modifiers}}
        """;

    private static async Task<TradeSearchDraft> DraftAsync(string clipboard)
    {
        var catalog = await CatalogLoad.Value;
        var item = new ItemTextParser().Parse(clipboard);
        var baseResolution = new ParsedItemBaseResolver().Resolve(item, catalog);
        var resolutions = new ParsedItemModifierCandidateResolver().Resolve(item, catalog, baseResolution);
        var result = new TradeSearchDraftMapper().CreateDraft(item, baseResolution, resolutions, catalog);

        Assert.True(result.IsSuccess);
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }

    private static async Task<GameDataCatalog> LoadRepoCatalogAsync()
    {
        var load = await GameDataPackageLoader.LoadFromFileAsync(
            FindRepoFile("artifacts", "poenhance-game-data.json"));
        Assert.True(load.IsSuccess);
        return GameDataCatalog.FromPackage(load.Package!);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(string.Join("/", relativeParts));
    }
}
