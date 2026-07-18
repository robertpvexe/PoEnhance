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
