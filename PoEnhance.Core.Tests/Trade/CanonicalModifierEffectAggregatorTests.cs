using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class CanonicalModifierEffectAggregatorTests
{
    [Fact]
    public void Aggregate_PureAndHybridPhysicalComponentsSumCanonicallyAndRetainBothSources()
    {
        var result = CanonicalModifierEffectAggregator.Aggregate(
        [
            Scalar("modifier:0:0", 0, "52% increased Physical Damage", 52m, "pure-physical"),
            Scalar("modifier:1:0", 1, "39% increased Physical Damage", 39m, "hybrid-physical-accuracy"),
            Scalar(
                "modifier:1:1",
                1,
                "+93 to Accuracy Rating",
                93m,
                "hybrid-physical-accuracy",
                signature: "+<number> to Accuracy Rating",
                statId: "local_accuracy_rating",
                sourceComponentIndex: 1),
        ]);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.Components.Count);
        var physical = Assert.Single(result.Components, component =>
            component.ResolvedStatIds.SequenceEqual(["local_physical_damage_+%"]));
        Assert.Equal("91% increased Physical Damage", physical.OriginalText);
        Assert.Equal([91m], physical.CanonicalNumericValues);
        Assert.Equal([91m], physical.ObservedNumericValues);
        Assert.Equal(91m, physical.RequestedMinimum);
        Assert.Null(physical.RequestedMaximum);
        Assert.Equal(2, physical.SourceCount);
        Assert.Equal(
            ["pure-physical", "hybrid-physical-accuracy"],
            physical.Sources.Select(source => source.ResolvedModifierId));
        Assert.Equal([52m, 39m], physical.Sources.Select(source => Assert.Single(source.CanonicalNumericValues)));
        Assert.Equal([false, true], physical.Sources.Select(source => source.IsHybrid));

        var accuracy = Assert.Single(result.Components, component =>
            component.ResolvedStatIds.SequenceEqual(["local_accuracy_rating"]));
        Assert.Equal("+93 to Accuracy Rating", accuracy.OriginalText);
        Assert.Equal(93m, accuracy.RequestedMinimum);
        Assert.Single(accuracy.Sources);
        Assert.True(accuracy.Sources[0].IsHybrid);
    }

    [Fact]
    public void Aggregate_CompatibleSignedScalarsUseCanonicalSignedSum()
    {
        var result = CanonicalModifierEffectAggregator.Aggregate(
        [
            Scalar("modifier:0:0", 0, "20 Test Value", 20m, "positive", signature: "<number> Test Value", statId: "test_value"),
            Scalar("modifier:1:0", 1, "-10 Test Value", -10m, "negative", signature: "<number> Test Value", statId: "test_value"),
        ]);

        var aggregate = Assert.Single(result.Components);
        Assert.Equal("10 Test Value", aggregate.OriginalText);
        Assert.Equal(10m, aggregate.RequestedMinimum);
        Assert.Equal([10m], aggregate.CanonicalNumericValues);
    }

    [Fact]
    public void Aggregate_HybridAndExplicitLifeSumsWithoutDoubleCountingSiblingHybridEffect()
    {
        // Synthetic Stage 03.10 Life pattern: +28 Hybrid Life + +117 Explicit Life = +145.
        // Sibling hybrid defence effect stays independent and is not folded into Life.
        var hybridLife = Scalar(
            "modifier:0:0",
            0,
            "+28 to maximum Life",
            28m,
            "hybrid-life-es",
            signature: "+<number> to maximum Life",
            statId: "base_maximum_life") with
        {
            Locality = ModifierLocality.Global,
            ParsedModifierName = "Prior's",
        };
        var hybridEs = Scalar(
            "modifier:0:1",
            0,
            "+15 to maximum Energy Shield",
            15m,
            "hybrid-life-es",
            signature: "+<number> to maximum Energy Shield",
            statId: "base_maximum_energy_shield",
            sourceComponentIndex: 1) with
        {
            Locality = ModifierLocality.Global,
            ParsedModifierName = "Prior's",
        };
        var explicitLife = Scalar(
            "modifier:1:0",
            1,
            "+117 to maximum Life",
            117m,
            "explicit-life",
            signature: "+<number> to maximum Life",
            statId: "base_maximum_life") with
        {
            Locality = ModifierLocality.Global,
            ParsedModifierName = "Athlete's",
        };

        var result = CanonicalModifierEffectAggregator.Aggregate([hybridLife, hybridEs, explicitLife]);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.Components.Count);
        var life = Assert.Single(result.Components, component =>
            component.ResolvedStatIds.SequenceEqual(["base_maximum_life"]));
        Assert.Equal("+145 to maximum Life", life.OriginalText);
        Assert.Equal(145m, life.RequestedMinimum);
        Assert.Equal([28m, 117m], life.Sources.Select(source => Assert.Single(source.CanonicalNumericValues)));
        Assert.Equal([true, false], life.Sources.Select(source => source.IsHybrid));
        Assert.Equal(2, life.Contributors.Count);

        var energyShield = Assert.Single(result.Components, component =>
            component.ResolvedStatIds.SequenceEqual(["base_maximum_energy_shield"]));
        Assert.Equal(15m, energyShield.RequestedMinimum);
        Assert.True(Assert.Single(energyShield.Sources).IsHybrid);
    }

    [Fact]
    public void Aggregate_FracturedAndExplicitLifeRetainDistinctSourcesAndDoNotMarkParentFractured()
    {
        // Synthetic regression fixture for Ctrl+D dual-source Life (+27 Fractured + +51 Explicit = +78).
        // Anchors are test-only; production must not hardcode these values.
        var fractured = Scalar(
            "modifier:0:0",
            0,
            "+27 to maximum Life",
            27m,
            "fractured-life",
            signature: "+<number> to maximum Life",
            statId: "base_maximum_life") with
        {
            IsFractured = true,
            Locality = ModifierLocality.Global,
            ParsedModifierName = "Virile",
        };
        var explicitLife = Scalar(
            "modifier:1:0",
            1,
            "+51 to maximum Life",
            51m,
            "explicit-life",
            signature: "+<number> to maximum Life",
            statId: "base_maximum_life") with
        {
            Locality = ModifierLocality.Global,
            ParsedModifierName = "Hale",
        };

        var result = CanonicalModifierEffectAggregator.Aggregate([fractured, explicitLife]);

        var aggregate = Assert.Single(result.Components);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("+78 to maximum Life", aggregate.OriginalText);
        Assert.Equal(78m, aggregate.RequestedMinimum);
        Assert.Equal([78m], aggregate.CanonicalNumericValues);
        Assert.False(aggregate.IsFractured);
        Assert.False(aggregate.IsCrafted);
        Assert.Equal(["Fractured", "Explicit"], aggregate.Sources.Select(source => source.ProviderDomain));
        Assert.Equal([27m, 51m], aggregate.Sources.Select(source => Assert.Single(source.CanonicalNumericValues)));
        Assert.Equal([true, false], aggregate.Sources.Select(source => source.IsFractured));
        Assert.Equal(2, aggregate.Contributors.Count);
        Assert.All(aggregate.Contributors, contributor => Assert.False(contributor.IsSelected));
        Assert.Equal(
            ["+27 to maximum Life", "+51 to maximum Life"],
            aggregate.Contributors.Select(contributor => contributor.DisplayText));
        Assert.DoesNotContain(aggregate.Sources, source =>
            source.IsFractured && Assert.Single(source.CanonicalNumericValues) == 78m);
    }

    [Fact]
    public void Aggregate_UnanimousFracturedSourcesRetainParentFracturedFlag()
    {
        var first = Scalar(
            "modifier:0:0",
            0,
            "+10 to maximum Life",
            10m,
            "fractured-life-a",
            signature: "+<number> to maximum Life",
            statId: "base_maximum_life") with
        {
            IsFractured = true,
            Locality = ModifierLocality.Global,
        };
        var second = Scalar(
            "modifier:1:0",
            1,
            "+20 to maximum Life",
            20m,
            "fractured-life-b",
            signature: "+<number> to maximum Life",
            statId: "base_maximum_life") with
        {
            IsFractured = true,
            Locality = ModifierLocality.Global,
        };

        var aggregate = Assert.Single(
            CanonicalModifierEffectAggregator.Aggregate([first, second]).Components);

        Assert.Equal("+30 to maximum Life", aggregate.OriginalText);
        Assert.True(aggregate.IsFractured);
        Assert.All(aggregate.Sources, source => Assert.True(source.IsFractured));
    }

    [Fact]
    public void Aggregate_NonAdditiveMoreModifierDoesNotSumWithIncreased()
    {
        var increased = Scalar(
            "modifier:0:0",
            0,
            "20% increased maximum Life",
            20m,
            "increased-life",
            signature: "<number>% increased maximum Life",
            statId: "maximum_life_+%");
        var more = Scalar(
            "modifier:1:0",
            1,
            "10% more maximum Life",
            10m,
            "more-life",
            signature: "<number>% more maximum Life",
            statId: "maximum_life_+%_more");

        var result = CanonicalModifierEffectAggregator.Aggregate([increased, more]);

        Assert.Equal(2, result.Components.Count);
        Assert.Equal(
            ["20% increased maximum Life", "10% more maximum Life"],
            result.Components.Select(component => component.OriginalText));
        Assert.DoesNotContain(result.Components, component =>
            component.OriginalText.Contains("30%", StringComparison.Ordinal));
    }

    [Fact]
    public void Aggregate_ExplicitAndCraftedContributorsShareCanonicalTotalAndRetainDomains()
    {
        var semantic = Semantic("weapon.physical-damage.increased-percent.local");
        var explicitComponent = Scalar(
            "modifier:0:0",
            0,
            "30% increased Physical Damage",
            30m,
            "explicit-hybrid") with
        {
            Tier = 3,
            StatMappingProof = ModifierStatMappingProofStatus.ProvenExact,
            ReviewedItemPropertySemantic = semantic,
        };
        var craftedComponent = Scalar(
            "modifier:1:0",
            1,
            "116% increased Physical Damage",
            116m,
            "crafted-physical") with
        {
            IsCrafted = true,
            Rank = 4,
            StatMappingProof = ModifierStatMappingProofStatus.WholeVector,
            ReviewedItemPropertySemantic = semantic,
        };

        var result = CanonicalModifierEffectAggregator.Aggregate([explicitComponent, craftedComponent]);

        var aggregate = Assert.Single(result.Components);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("146% increased Physical Damage", aggregate.OriginalText);
        Assert.Equal(146m, aggregate.RequestedMinimum);
        Assert.Equal([30m, 116m], aggregate.Sources.Select(source => Assert.Single(source.CanonicalNumericValues)));
        Assert.Equal(["Explicit", "Crafted"], aggregate.Sources.Select(source => source.ProviderDomain));
        Assert.Same(semantic, aggregate.ReviewedItemPropertySemantic);
        Assert.Equal(ModifierStatMappingProofStatus.Unknown, aggregate.StatMappingProof);
        Assert.Null(aggregate.Tier);
        Assert.Null(aggregate.Rank);
        Assert.Equal([3, null], aggregate.Sources.Select(source => source.Tier));
        Assert.Equal([null, 4], aggregate.Sources.Select(source => source.Rank));
        Assert.All(aggregate.Sources, source => Assert.Same(semantic, source.ReviewedItemPropertySemantic));
        Assert.Equal(
            [ModifierStatMappingProofStatus.ProvenExact, ModifierStatMappingProofStatus.WholeVector],
            aggregate.Sources.Select(source => source.StatMappingProof));
        Assert.Equal(SearchComponentContributorProjection.Additive, aggregate.ContributorProjection);
        Assert.Collection(
            aggregate.Contributors,
            contributor =>
            {
                Assert.False(contributor.IsSelected);
                Assert.Equal("30% increased Physical Damage", contributor.DisplayText);
                Assert.Equal("Explicit", contributor.Source.ProviderDomain);
                Assert.Equal(3, contributor.Source.Tier);
                Assert.Null(contributor.Source.Rank);
                Assert.Same(semantic, contributor.Source.ReviewedItemPropertySemantic);
                Assert.Equal(30m, contributor.RequestedMinimum);
                Assert.Null(contributor.RequestedMaximum);
            },
            contributor =>
            {
                Assert.False(contributor.IsSelected);
                Assert.Equal("116% increased Physical Damage", contributor.DisplayText);
                Assert.Equal("Crafted", contributor.Source.ProviderDomain);
                Assert.Null(contributor.Source.Tier);
                Assert.Equal(4, contributor.Source.Rank);
                Assert.Same(semantic, contributor.Source.ReviewedItemPropertySemantic);
                Assert.Equal(116m, contributor.RequestedMinimum);
                Assert.Null(contributor.RequestedMaximum);
            });
        Assert.All(aggregate.Contributors, contributor => Assert.NotEqual(aggregate.ComponentId, contributor.ContributorId));
    }

    [Fact]
    public void Aggregate_ImplicitAndNonImplicitOriginsRemainIndependent()
    {
        var explicitComponent = Scalar(
            "modifier:0:0",
            0,
            "20% increased Physical Damage",
            20m,
            "explicit");
        var implicitComponent = Scalar(
            "modifier:1:0",
            1,
            "10% increased Physical Damage",
            10m,
            "implicit") with
        {
            ParsedKind = ParsedModifierKind.Implicit,
            GenerationType = ModifierGenerationType.Implicit,
        };

        var result = CanonicalModifierEffectAggregator.Aggregate([explicitComponent, implicitComponent]);

        Assert.Equal(2, result.Components.Count);
        Assert.Equal([20m, 10m], result.Components.Select(component => component.RequestedMinimum));
        Assert.Equal(
            [ParsedModifierKind.Prefix, ParsedModifierKind.Implicit],
            result.Components.Select(component => component.ParsedKind));
    }

    [Fact]
    public void Aggregate_EnchantmentAndExplicitTextCollisionRemainIndependent()
    {
        var explicitComponent = Scalar(
            "modifier:0:0",
            0,
            "12% increased Movement Speed",
            12m,
            "explicit-movement",
            "<number>% increased Movement Speed",
            "movement_speed_+%");
        var enchantment = explicitComponent with
        {
            ComponentId = "modifier:1:0",
            SourceModifierIndex = 1,
            ParsedKind = ParsedModifierKind.Enchantment,
            GenerationType = ModifierGenerationType.Enchantment,
            ResolvedModifierId = "enchantment-movement",
        };

        var result = CanonicalModifierEffectAggregator.Aggregate([explicitComponent, enchantment]);

        Assert.Equal(2, result.Components.Count);
        Assert.Equal(
            [ParsedModifierKind.Prefix, ParsedModifierKind.Enchantment],
            result.Components.Select(component => component.ParsedKind));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("enchantment source provenance", StringComparison.Ordinal));
    }

    [Fact]
    public void Aggregate_UniqueSourceWithImplicitMechanicalGeneration_RemainsIndependentFromItemImplicit()
    {
        var uniqueComponent = Scalar(
            "modifier:0:0",
            0,
            "58% increased Minion Damage",
            58m,
            "unique-minion-damage") with
        {
            ParsedKind = ParsedModifierKind.Unique,
            GenerationType = ModifierGenerationType.Implicit,
        };
        var implicitComponent = Scalar(
            "modifier:1:0",
            1,
            "14% increased Minion Damage",
            14m,
            "implicit-minion-damage") with
        {
            ParsedKind = ParsedModifierKind.Implicit,
            GenerationType = ModifierGenerationType.Implicit,
        };

        var result = CanonicalModifierEffectAggregator.Aggregate([uniqueComponent, implicitComponent]);

        Assert.Equal(2, result.Components.Count);
        Assert.Equal([58m, 14m], result.Components.Select(component => component.RequestedMinimum));
        Assert.Equal("Unique", CanonicalModifierEffectAggregator.ProviderDomainFor(uniqueComponent));
        Assert.Equal("Implicit", CanonicalModifierEffectAggregator.ProviderDomainFor(implicitComponent));
    }

    [Fact]
    public void Aggregate_NormalAndFoulbornUniqueRows_NeverConstructsHybridDefinition()
    {
        var normal = Scalar(
            "modifier:0:0",
            0,
            "20% increased Effect",
            20m,
            "shared-stat") with
        {
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
        };
        var foulborn = Scalar(
            "modifier:1:0",
            1,
            "10% increased Effect",
            10m,
            "shared-stat") with
        {
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = ParsedUniqueModifierOrigin.Foulborn,
            UniqueFoulbornRelationshipIds = ["foulborn-relationship:test"],
            UniqueNormalCounterpartModifierIds = ["normal.modifier.test"],
        };

        var result = CanonicalModifierEffectAggregator.Aggregate([normal, foulborn]);

        Assert.Equal(2, result.Components.Count);
        Assert.Equal(ParsedUniqueModifierOrigin.Ordinary, result.Components[0].UniqueOrigin);
        Assert.Equal(ParsedUniqueModifierOrigin.Foulborn, result.Components[1].UniqueOrigin);
        Assert.Equal(
            ["foulborn-relationship:test"],
            result.Components[1].UniqueFoulbornRelationshipIds);
        var source = Assert.Single(result.Components[1].Sources);
        Assert.Equal(["foulborn-relationship:test"], source.UniqueFoulbornRelationshipIds);
        Assert.Equal(["normal.modifier.test"], source.UniqueNormalCounterpartModifierIds);
    }

    [Fact]
    public void Aggregate_EldritchImplicitOriginsRemainIndependent()
    {
        var eater = Scalar(
            "modifier:0:0",
            0,
            "10% increased Effect",
            10m,
            "eater") with
        {
            ParsedKind = ParsedModifierKind.Implicit,
            ImplicitOrigin = ParsedImplicitModifierOrigin.EaterOfWorlds,
            GenerationType = ModifierGenerationType.Implicit,
        };
        var exarch = eater with
        {
            ComponentId = "modifier:1:0",
            SourceModifierIndex = 1,
            OriginalText = "20% increased Effect",
            ImplicitOrigin = ParsedImplicitModifierOrigin.SearingExarch,
            ResolvedModifierId = "exarch",
            RequestedMinimum = 20m,
            ObservedNumericValues = [20m],
            CanonicalNumericValues = [20m],
        };

        var result = CanonicalModifierEffectAggregator.Aggregate([eater, exarch]);

        Assert.Equal(2, result.Components.Count);
        Assert.Equal(
            [ParsedImplicitModifierOrigin.EaterOfWorlds, ParsedImplicitModifierOrigin.SearingExarch],
            result.Components.Select(component => component.ImplicitOrigin));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("different implicit source provenance", StringComparison.Ordinal));
    }

    [Fact]
    public void Aggregate_HybridAndSuffixStunRecoverySumsObservedTotalAndRetainsDistinctSources()
    {
        // Synthetic Stage 03.10 fixture: Elephant's-family Hybrid 14% + independent Suffix 27% = 41%.
        // Values and affix family names are test-only; production uses GameData/Trade identities.
        var hybrid = Scalar(
            "modifier:0:0",
            0,
            "14% increased Stun and Block Recovery",
            14m,
            "hybrid-stun-recovery",
            "<number>% increased Stun and Block Recovery",
            "base_stun_recovery_+%") with
        {
            ParsedKind = ParsedModifierKind.Prefix,
            GenerationType = ModifierGenerationType.Prefix,
            ParsedModifierName = "Elephant's",
            Locality = ModifierLocality.Global,
        };
        var hybridDefence = Scalar(
            "modifier:0:1",
            0,
            "37% increased Armour and Evasion",
            37m,
            "hybrid-stun-recovery",
            "<number>% increased Armour and Evasion",
            "local_armour_and_evasion_+%",
            sourceComponentIndex: 1) with
        {
            ParsedKind = ParsedModifierKind.Prefix,
            GenerationType = ModifierGenerationType.Prefix,
            ParsedModifierName = "Elephant's",
        };
        var suffix = Scalar(
            "modifier:1:0",
            1,
            "27% increased Stun and Block Recovery",
            27m,
            "suffix-stun-recovery",
            "<number>% increased Stun and Block Recovery",
            "base_stun_recovery_+%") with
        {
            ParsedKind = ParsedModifierKind.Suffix,
            GenerationType = ModifierGenerationType.Suffix,
            ParsedModifierName = "of the Whelpling",
            Locality = ModifierLocality.Global,
        };

        var result = CanonicalModifierEffectAggregator.Aggregate([hybrid, hybridDefence, suffix]);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.Components.Count);
        var stun = Assert.Single(result.Components, component =>
            component.ResolvedStatIds.SequenceEqual(["base_stun_recovery_+%"]));
        Assert.Equal("41% increased Stun and Block Recovery", stun.OriginalText);
        Assert.Equal(41m, stun.RequestedMinimum);
        Assert.Equal([41m], stun.ObservedNumericValues);
        Assert.Equal([41m], stun.CanonicalNumericValues);
        Assert.Equal(SearchComponentContributorProjection.Additive, stun.ContributorProjection);
        Assert.Equal(2, stun.SourceCount);
        Assert.Equal(["Explicit", "Explicit"], stun.Sources.Select(source => source.ProviderDomain));
        Assert.Equal([true, false], stun.Sources.Select(source => source.IsHybrid));
        Assert.Equal(
            [ParsedModifierKind.Prefix, ParsedModifierKind.Suffix],
            stun.Sources.Select(source => source.ParsedKind));
        Assert.Equal([14m, 27m], stun.Sources.Select(source => Assert.Single(source.CanonicalNumericValues)));
        Assert.Equal(
            ["modifier:0:0", "modifier:1:0"],
            stun.Sources.Select(source => source.ComponentId));
        Assert.Equal(2, stun.Contributors.Count);
        Assert.Equal(
            ["14% increased Stun and Block Recovery", "27% increased Stun and Block Recovery"],
            stun.Contributors.Select(contributor => contributor.DisplayText));
        Assert.Equal([14m, 27m], stun.Contributors.Select(contributor => contributor.RequestedMinimum));
        Assert.All(stun.Contributors, contributor => Assert.False(contributor.IsSelected));
        Assert.False(stun.IsFractured);

        var defence = Assert.Single(result.Components, component =>
            component.ResolvedStatIds.SequenceEqual(["local_armour_and_evasion_+%"]));
        Assert.Equal(37m, defence.RequestedMinimum);
        Assert.True(Assert.Single(defence.Sources).IsHybrid);
        Assert.Equal(1, defence.SourceCount);
    }

    [Fact]
    public void Aggregate_IdenticalTextImplicitsRemainDistinctIncludingCorrupted()
    {
        var baseImplicit = Scalar(
            "modifier:0:0",
            0,
            "14% increased Stun and Block Recovery",
            14m,
            "implicit-recovery",
            "<number>% increased Stun and Block Recovery",
            "base_stun_recovery_+%") with
        {
            ParsedKind = ParsedModifierKind.Implicit,
            GenerationType = ModifierGenerationType.Implicit,
            ImplicitOrigin = ParsedImplicitModifierOrigin.Unspecified,
        };
        var secondBaseImplicit = baseImplicit with
        {
            ComponentId = "modifier:1:0",
            SourceModifierIndex = 1,
            ResolvedModifierId = "implicit-recovery-2",
        };
        var corruptedImplicit = baseImplicit with
        {
            ComponentId = "modifier:2:0",
            SourceModifierIndex = 2,
            ResolvedModifierId = "corrupted-recovery",
            ImplicitOrigin = ParsedImplicitModifierOrigin.Corrupted,
        };
        var matchingExplicit = baseImplicit with
        {
            ComponentId = "modifier:3:0",
            SourceModifierIndex = 3,
            ParsedKind = ParsedModifierKind.Suffix,
            GenerationType = ModifierGenerationType.Suffix,
            ImplicitOrigin = ParsedImplicitModifierOrigin.Unspecified,
            ResolvedModifierId = "explicit-recovery",
        };

        var result = CanonicalModifierEffectAggregator.Aggregate(
            [baseImplicit, secondBaseImplicit, corruptedImplicit, matchingExplicit]);

        Assert.Equal(4, result.Components.Count);
        Assert.Equal(
            [
                ParsedModifierKind.Implicit,
                ParsedModifierKind.Implicit,
                ParsedModifierKind.Implicit,
                ParsedModifierKind.Suffix,
            ],
            result.Components.Select(component => component.ParsedKind));
        Assert.Equal(
            [
                ParsedImplicitModifierOrigin.Unspecified,
                ParsedImplicitModifierOrigin.Unspecified,
                ParsedImplicitModifierOrigin.Corrupted,
                ParsedImplicitModifierOrigin.Unspecified,
            ],
            result.Components.Select(component => component.ImplicitOrigin));
        Assert.Equal(
            [14m, 14m, 14m, 14m],
            result.Components.Select(component => component.RequestedMinimum));
        Assert.All(result.Components, component => Assert.Equal(1, component.SourceCount));
    }

    [Fact]
    public void Aggregate_DifferentReviewedSemanticsRemainSeparate()
    {
        var first = Scalar("modifier:0:0", 0, "30% increased Physical Damage", 30m, "first") with
        {
            StatMappingProof = ModifierStatMappingProofStatus.ProvenExact,
            ReviewedItemPropertySemantic = Semantic("semantic.first"),
        };
        var second = Scalar("modifier:1:0", 1, "40% increased Physical Damage", 40m, "second") with
        {
            StatMappingProof = ModifierStatMappingProofStatus.ProvenExact,
            ReviewedItemPropertySemantic = Semantic("semantic.second"),
        };

        var result = CanonicalModifierEffectAggregator.Aggregate([first, second]);

        Assert.Equal(2, result.Components.Count);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("different reviewed item-property semantics", StringComparison.Ordinal));
    }

    [Fact]
    public void Aggregate_ReviewedSemanticPresentAndAbsentRemainSeparate()
    {
        var withSemantic = Scalar("modifier:0:0", 0, "30% increased Physical Damage", 30m, "with-semantic") with
        {
            StatMappingProof = ModifierStatMappingProofStatus.ProvenExact,
            ReviewedItemPropertySemantic = Semantic("semantic.physical"),
        };
        var withoutSemantic = Scalar("modifier:1:0", 1, "40% increased Physical Damage", 40m, "without-semantic") with
        {
            StatMappingProof = ModifierStatMappingProofStatus.ProvenExact,
        };

        var result = CanonicalModifierEffectAggregator.Aggregate([withSemantic, withoutSemantic]);

        Assert.Equal(2, result.Components.Count);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("different reviewed item-property semantics", StringComparison.Ordinal));
    }

    [Fact]
    public void Aggregate_AbsentReviewedSemanticsPreservesExistingCompatibility()
    {
        var result = CanonicalModifierEffectAggregator.Aggregate(
        [
            Scalar("modifier:0:0", 0, "30% increased Physical Damage", 30m, "first") with
            {
                StatMappingProof = ModifierStatMappingProofStatus.ProvenExact,
            },
            Scalar("modifier:1:0", 1, "40% increased Physical Damage", 40m, "second") with
            {
                StatMappingProof = ModifierStatMappingProofStatus.PositionalFallback,
            },
        ]);

        var aggregate = Assert.Single(result.Components);
        Assert.Equal(70m, aggregate.RequestedMinimum);
        Assert.Null(aggregate.ReviewedItemPropertySemantic);
        Assert.Equal(ModifierStatMappingProofStatus.Unknown, aggregate.StatMappingProof);
        Assert.Equal(
            [ModifierStatMappingProofStatus.ProvenExact, ModifierStatMappingProofStatus.PositionalFallback],
            aggregate.Sources.Select(source => source.StatMappingProof));
    }

    [Fact]
    public void Aggregate_CompatibleDamageTuplesSumComponentWiseBeforeProjection()
    {
        var result = CanonicalModifierEffectAggregator.Aggregate(
        [
            DamageRange("modifier:0:0", 0, 10m, 20m),
            DamageRange("modifier:1:0", 1, 5m, 8m),
        ]);

        var aggregate = Assert.Single(result.Components);
        Assert.Equal("Adds 15 to 28 Cold Damage", aggregate.OriginalText);
        Assert.Equal([15m, 28m], aggregate.ObservedNumericValues);
        Assert.Equal([15m, 28m], aggregate.CanonicalNumericValues);
        Assert.Equal(ModifierBoundShape.ArithmeticMeanRange, aggregate.ValueBoundShape);
        Assert.Null(aggregate.RequestedMinimum);
    }

    [Fact]
    public void Aggregate_DifferentLocalityRemainsSeparateWhileLocalDomainsShareOneTotal()
    {
        var local = Scalar("modifier:0:0", 0, "20% increased Attack Speed", 20m, "local") with
        {
            CanonicalSignature = "<number>% increased Attack Speed",
            ResolvedStatIds = ["attack_speed_+%"],
        };
        var global = local with
        {
            ComponentId = "modifier:1:0",
            SourceModifierIndex = 1,
            Locality = ModifierLocality.Global,
        };
        var crafted = local with
        {
            ComponentId = "modifier:2:0",
            SourceModifierIndex = 2,
            IsCrafted = true,
        };
        var fractured = local with
        {
            ComponentId = "modifier:3:0",
            SourceModifierIndex = 3,
            IsFractured = true,
        };

        var result = CanonicalModifierEffectAggregator.Aggregate([local, global, crafted, fractured]);

        Assert.Equal(2, result.Components.Count);
        var localAggregate = Assert.Single(result.Components, component => component.Locality == ModifierLocality.Local);
        Assert.Equal(60m, localAggregate.RequestedMinimum);
        Assert.Equal(["Explicit", "Crafted", "Fractured"], localAggregate.Sources.Select(source => source.ProviderDomain));
        Assert.False(localAggregate.IsFractured);
        Assert.False(localAggregate.IsCrafted);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("different locality", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Message.Contains("provider domain", StringComparison.Ordinal));
    }

    [Fact]
    public void Aggregate_DifferentLogicalSemanticsAndTransformsRemainSeparate()
    {
        var increased = Scalar("modifier:0:0", 0, "20% increased Damage", 20m, "increased", signature: "<number>% increased Damage", statId: "damage_modifier");
        var more = increased with
        {
            ComponentId = "modifier:1:0",
            SourceModifierIndex = 1,
            OriginalText = "20% more Damage",
            CanonicalSignature = "<number>% more Damage",
        };
        var transformed = increased with
        {
            ComponentId = "modifier:2:0",
            SourceModifierIndex = 2,
            ValueBoundTranslationHandlers = [["divide_by_two_0dp"]],
        };
        var flatUnit = increased with
        {
            ComponentId = "modifier:3:0",
            SourceModifierIndex = 3,
            OriginalText = "20 increased Damage",
            CanonicalSignature = "<number> increased Damage",
        };

        var result = CanonicalModifierEffectAggregator.Aggregate([increased, more, transformed, flatUnit]);

        Assert.Equal(4, result.Components.Count);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("non-additive logical semantics", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("incompatible translation transforms", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("incompatible numeric units", StringComparison.Ordinal));
    }

    [Fact]
    public void Aggregate_IncompatibleNumericShapesRemainSeparateAndProduceDiagnostic()
    {
        var scalar = Scalar("modifier:0:0", 0, "10 Test Value", 10m, "scalar", signature: "<number> Test Value", statId: "test_value");
        var tuple = DamageRange("modifier:1:0", 1, 5m, 8m) with
        {
            OriginalText = "5 Test Value",
            CanonicalSignature = "<number> Test Value",
            ResolvedStatIds = ["test_value"],
        };
        var presence = scalar with
        {
            ComponentId = "modifier:2:0",
            SourceModifierIndex = 2,
            OriginalText = "Test Value is present",
            CanonicalSignature = "<number> Test Value",
            SupportsValueBounds = false,
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            ObservedNumericValues = [],
            CanonicalNumericValues = [],
            RequestedMinimum = null,
        };

        var result = CanonicalModifierEffectAggregator.Aggregate([scalar, tuple, presence]);

        Assert.Equal(3, result.Components.Count);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == TradeSearchDraftDiagnosticCodes.ModifierAggregationSkipped &&
            diagnostic.Message.Contains("numeric shape", StringComparison.Ordinal));
    }

    private static ResolvedSearchComponent Scalar(
        string componentId,
        int sourceModifierIndex,
        string text,
        decimal value,
        string modifierId,
        string signature = "<number>% increased Physical Damage",
        string statId = "local_physical_damage_+%",
        int sourceComponentIndex = 0)
    {
        return new ResolvedSearchComponent
        {
            ComponentId = componentId,
            SourceModifierIndex = sourceModifierIndex,
            SourceLineIndex = sourceComponentIndex,
            SourceComponentIndex = sourceComponentIndex,
            OriginalText = text,
            CanonicalSignature = signature,
            ParsedKind = ParsedModifierKind.Prefix,
            GenerationType = ModifierGenerationType.Prefix,
            Locality = ModifierLocality.Local,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = modifierId,
            ResolvedModifierName = modifierId,
            ResolvedStatIds = [statId],
            IsSearchable = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = [value],
            CanonicalNumericValues = [value],
            ValueBoundTranslationHandlers = [[]],
            ValueBoundTranslationIdentity = $"translation:{statId}",
            DefaultBoundDirection = ModifierBoundDirection.Minimum,
            RequestedMinimum = value,
        };
    }

    private static ResolvedSearchComponent DamageRange(
        string componentId,
        int sourceModifierIndex,
        decimal minimum,
        decimal maximum)
    {
        return new ResolvedSearchComponent
        {
            ComponentId = componentId,
            SourceModifierIndex = sourceModifierIndex,
            OriginalText = $"Adds {minimum} to {maximum} Cold Damage",
            CanonicalSignature = "Adds <number> to <number> Cold Damage",
            ParsedKind = ParsedModifierKind.Prefix,
            GenerationType = ModifierGenerationType.Prefix,
            Locality = ModifierLocality.Local,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = $"range-{sourceModifierIndex}",
            ResolvedStatIds = ["local_minimum_added_cold_damage", "local_maximum_added_cold_damage"],
            IsSearchable = true,
            SupportsValueBounds = false,
            ValueBoundShape = ModifierBoundShape.ArithmeticMeanRange,
            ObservedNumericValues = [minimum, maximum],
            CanonicalNumericValues = [minimum, maximum],
            ValueBoundTranslationHandlers = [[], []],
            ValueBoundTranslationIdentity = "translation:cold-range",
            DefaultBoundDirection = ModifierBoundDirection.Minimum,
        };
    }

    private static ItemPropertySemanticDescriptor Semantic(string id)
    {
        return new ItemPropertySemanticDescriptor
        {
            Id = id,
            OrderedStatIds = ["local_physical_damage_+%"],
            Contributions =
            [
                new ItemPropertyContribution
                {
                    Targets = [ItemPropertyTarget.PhysicalDamage],
                    Operation = ItemPropertyOperation.IncreasedPercent,
                },
            ],
            Applicability = ItemPropertyApplicability.UnconditionalDisplayedLocal,
            Evidence =
            [
                new ItemPropertySemanticEvidence
                {
                    Method = ItemPropertySemanticEvidenceMethod.ReviewedOverride,
                    SourceId = "test",
                    ReviewVersion = "test-v1",
                    ReviewReference = "test-review",
                },
            ],
        };
    }
}
