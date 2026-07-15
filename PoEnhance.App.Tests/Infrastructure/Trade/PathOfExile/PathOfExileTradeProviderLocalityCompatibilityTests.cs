using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeProviderLocalityCompatibilityTests
{
    [Fact]
    public void EvaluateVariant_UnmarkedWithoutGameDataEvidenceIsNotAssumedGlobal()
    {
        var candidate = Candidate("explicit.stat_test", "+# to an Attribute", "Explicit");

        var decision = PathOfExileTradeProviderLocalityCompatibility.EvaluateVariant(
            Component(),
            candidate,
            candidate);

        Assert.Equal(PathOfExileTradeProviderLocalityDecisionStatus.InsufficientEvidence, decision.Status);
        Assert.Equal(ModifierLocality.Unknown, decision.EffectiveLocality);
        Assert.Equal(PathOfExileTradeProviderLocalityCompatibility.InsufficientLocalityEvidence, decision.ReasonCode);
    }

    [Theory]
    [InlineData(ModifierLocality.Local)]
    [InlineData(ModifierLocality.Global)]
    public void EvaluateVariant_ExactRetainedSourceIdentityAcceptsUnmarkedProvider(
        ModifierLocality locality)
    {
        var candidate = Candidate("explicit.stat_test", "+# to Accuracy Rating", "Explicit");
        var component = Component() with
        {
            Sources = [Source(locality, candidate)],
        };

        var decision = PathOfExileTradeProviderLocalityCompatibility.EvaluateVariant(
            component,
            candidate,
            candidate);

        Assert.True(decision.IsCompatible);
        Assert.Equal(locality, decision.EffectiveLocality);
        Assert.Equal("ExactRetainedSourceIdentity", decision.EvidenceSource);
    }

    [Fact]
    public void EvaluateVariant_ExplicitLocalMarkerRejectsProvenGlobalSource()
    {
        var candidate = Candidate("explicit.stat_test", "+# to Accuracy Rating (Local)", "Explicit");
        var component = Component() with
        {
            Sources = [Source(ModifierLocality.Global, candidate)],
        };

        var decision = PathOfExileTradeProviderLocalityCompatibility.EvaluateVariant(
            component,
            candidate,
            candidate);

        Assert.Equal(PathOfExileTradeProviderLocalityDecisionStatus.Incompatible, decision.Status);
        Assert.Equal(PathOfExileTradeProviderLocalityCompatibility.ExplicitLocalityConflict, decision.ReasonCode);
    }

    [Fact]
    public void EvaluateVariant_ConflictingContextualFamiliesWithoutExactIdentityAreAmbiguous()
    {
        var candidate = Candidate("explicit.stat_test", "+# to Accuracy Rating", "Explicit");
        var component = Component() with
        {
            ProviderDomainEvidence =
            [
                Evidence(ModifierLocality.Local),
                Evidence(ModifierLocality.Global),
            ],
        };

        var decision = PathOfExileTradeProviderLocalityCompatibility.EvaluateVariant(
            component,
            candidate,
            candidate);

        Assert.Equal(PathOfExileTradeProviderLocalityDecisionStatus.Ambiguous, decision.Status);
        Assert.Equal(PathOfExileTradeProviderLocalityCompatibility.AmbiguousLocalityEvidence, decision.ReasonCode);
    }

    [Fact]
    public void EvaluateVariant_ExactSourceEvidencePrecedesConflictingContextualFamilies()
    {
        var candidate = Candidate("explicit.stat_test", "+# to Accuracy Rating", "Explicit");
        var component = Component() with
        {
            Sources = [Source(ModifierLocality.Local, candidate)],
            ProviderDomainEvidence =
            [
                Evidence(ModifierLocality.Local),
                Evidence(ModifierLocality.Global),
            ],
        };

        var decision = PathOfExileTradeProviderLocalityCompatibility.EvaluateVariant(
            component,
            candidate,
            candidate);

        Assert.True(decision.IsCompatible);
        Assert.Equal(ModifierLocality.Local, decision.EffectiveLocality);
        Assert.Equal("ExactRetainedSourceIdentity", decision.EvidenceSource);
    }

    [Fact]
    public void Resolver_HidesAmbiguousUnmarkedVariantAndRetainsPreciseDiagnostic()
    {
        var entry = new PathOfExileTradeStatEntry
        {
            ProviderOrder = 0,
            GroupId = "explicit",
            GroupLabel = "Explicit",
            Id = "explicit.stat_test",
            Text = "+# to Accuracy Rating",
            Type = "explicit",
        };
        var catalog = new PathOfExileTradeStatCatalog([entry]);
        var candidate = PathOfExileTradeStatCandidateClassifier.ToCandidate(entry);
        var component = Component() with
        {
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "mod.test",
            ResolvedStatIds = ["stat.test"],
            IsSearchable = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ProviderDomainEvidence =
            [
                Evidence(ModifierLocality.Local),
                Evidence(ModifierLocality.Global),
            ],
            SelectedFilterVariantIdentity = PathOfExileTradeProviderIdentity.Create(entry.Id),
        };

        var directDecision = PathOfExileTradeProviderLocalityCompatibility.EvaluateVariant(
            component,
            candidate,
            candidate);
        Assert.Equal(PathOfExileTradeProviderLocalityDecisionStatus.Ambiguous, directDecision.Status);

        var resolved = PathOfExileTradeModifierVariantResolver.Apply(component, catalog, candidate);

        Assert.Empty(resolved.FilterVariants);
        Assert.Equal(SearchComponentProviderResolutionStatus.Ambiguous, resolved.ProviderResolutionStatus);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantLocalityAmbiguous,
            resolved.ProviderDiagnosticCode);
        Assert.Contains("both Local and Global", resolved.ProviderDiagnosticMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void EvaluateRetainedSourceIdentity_UsesTheSameDecisionAsVariantDiscovery()
    {
        var candidate = Candidate("crafted.stat_803737631", "+# to Accuracy Rating", "Crafted");
        var source = Source(ModifierLocality.Local, candidate);
        var component = Component() with { Sources = [source] };

        var discovery = PathOfExileTradeProviderLocalityCompatibility.EvaluateVariant(
            component,
            candidate,
            candidate);
        var finalMapping = PathOfExileTradeProviderLocalityCompatibility.EvaluateRetainedSourceIdentity(
            source,
            candidate);

        Assert.True(discovery.IsCompatible);
        Assert.True(finalMapping.IsCompatible);
        Assert.Equal(discovery.EffectiveLocality, finalMapping.EffectiveLocality);
        Assert.Equal(discovery.ReasonCode, finalMapping.ReasonCode);
    }

    [Fact]
    public void EvaluateRetainedSourceIdentity_UnsupportedBoundsDoNotInvalidateTheStatIdentity()
    {
        var candidate = Candidate("explicit.stat_803737631", "+# to Accuracy Rating", "Explicit");
        var source = Source(ModifierLocality.Local, candidate) with
        {
            ValueBoundShape = ModifierBoundShape.Unsupported,
            TranslationHandlers = [],
        };

        var decision = PathOfExileTradeProviderLocalityCompatibility.EvaluateRetainedSourceIdentity(
            source,
            candidate);

        Assert.True(decision.IsCompatible);
        Assert.Equal(ModifierLocality.Local, decision.EffectiveLocality);
    }

    [Fact]
    public void Classifier_DistinguishesExplicitGlobalMarkerFromUnmarked()
    {
        var explicitlyGlobal = Candidate("explicit.global", "+# to Accuracy Rating (Global)", "Explicit");
        var unmarked = Candidate("explicit.unmarked", "+# to Accuracy Rating", "Explicit");

        Assert.Equal(PathOfExileTradeProviderStatLocality.Global, explicitlyGlobal.ProviderLocality);
        Assert.Equal(PathOfExileTradeProviderStatLocality.Unmarked, unmarked.ProviderLocality);
        Assert.Equal(explicitlyGlobal.LookupTemplate, unmarked.LookupTemplate);
    }

    private static ResolvedSearchComponent Component()
    {
        return new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            OriginalText = "+10 to Accuracy Rating",
            CanonicalSignature = "+<number> to Accuracy Rating",
            ParsedKind = ParsedModifierKind.Prefix,
            Locality = ModifierLocality.Unknown,
        };
    }

    private static SearchComponentSourceProvenance Source(
        ModifierLocality locality,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        return new SearchComponentSourceProvenance
        {
            ComponentId = "modifier:0:0",
            OriginalText = "+10 to Accuracy Rating",
            CanonicalSignature = "+<number> to Accuracy Rating",
            ParsedKind = ParsedModifierKind.Prefix,
            Locality = locality,
            ProviderDomain = candidate.ProviderKind,
            ResolvedModifierId = "mod.test",
            ResolvedStatIds = ["stat.test"],
            ValueBoundShape = ModifierBoundShape.Scalar,
            TranslationHandlers = [[]],
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            ProviderIdentity = PathOfExileTradeProviderIdentity.Create(candidate.StatId),
        };
    }

    private static SearchComponentProviderDomainEvidence Evidence(ModifierLocality locality)
    {
        return new SearchComponentProviderDomainEvidence
        {
            ProviderDomain = "Explicit",
            ModifierId = $"mod.{locality}",
            Locality = locality,
            ApplicabilityReason = "Applicable fixture family.",
        };
    }

    private static PathOfExileTradeStatMatchCandidate Candidate(
        string id,
        string text,
        string kind)
    {
        return PathOfExileTradeStatCandidateClassifier.ToCandidate(new PathOfExileTradeStatEntry
        {
            ProviderOrder = 0,
            GroupId = kind.ToLowerInvariant(),
            GroupLabel = kind,
            Id = id,
            Text = text,
            Type = kind.ToLowerInvariant(),
        });
    }
}
