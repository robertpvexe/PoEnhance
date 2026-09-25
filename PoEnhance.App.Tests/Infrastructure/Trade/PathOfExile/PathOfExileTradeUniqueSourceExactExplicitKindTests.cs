using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// TRADE.4 — Unique Source-Exact provider-domain evidence maps to Trade kind <c>explicit</c>,
/// so Explicit/Scourge text siblings do not falsely Ambiguous under Foulborn Unique ownership.
/// </summary>
public sealed class PathOfExileTradeUniqueSourceExactExplicitKindTests
{
    private readonly PathOfExileTradeStatMatcher matcher = new();

    [Fact]
    public void Match_FoulbornUniqueSourceExact_PrefersExplicitOverScourgeSibling()
    {
        var catalog = Catalog(
            Entry("explicit.stat_life_regen", "#% increased Life Regeneration rate", "explicit", 0),
            Entry("scourge.stat_life_regen", "#% increased Life Regeneration rate", "scourge", 1));

        var result = matcher.Match(
            FoulbornUniqueSourceExact(
                original: "17% increased Life Regeneration rate",
                signature: "<number>% increased Life Regeneration rate",
                statId: "life_regeneration_rate_+%",
                uniqueDomainStrength: 1000,
                projectedExplicitStrength: 90,
                projectedScourgeStrength: 90),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_life_regen", result.ExactCandidate?.StatId);
        Assert.Equal("explicit", result.ExactCandidate?.ProviderKind);
        Assert.Contains(
            result.RejectedCandidates,
            candidate => candidate.StatId == "scourge.stat_life_regen");
    }

    [Fact]
    public void Match_GenericUniqueSourceExact_PrefersExplicitWithoutItemIdentity()
    {
        var catalog = Catalog(
            Entry("explicit.shared", "#% increased Effect Magnitude", "explicit", 0),
            Entry("scourge.shared", "#% increased Effect Magnitude", "scourge", 1));

        var result = matcher.Match(
            FoulbornUniqueSourceExact(
                original: "10% increased Effect Magnitude",
                signature: "<number>% increased Effect Magnitude",
                statId: "effect_magnitude_+%",
                uniqueDomainStrength: 1000,
                projectedExplicitStrength: 90,
                projectedScourgeStrength: 90),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.shared", result.ExactCandidate?.StatId);
        Assert.DoesNotContain("Null", result.ExactCandidate!.StatId, StringComparison.Ordinal);
        Assert.DoesNotContain("Void", result.ExactCandidate.StatId, StringComparison.Ordinal);
    }

    [Fact]
    public void Match_ExplicitScourgePairWithoutSourceExactUnique_RemainsAmbiguous()
    {
        var catalog = Catalog(
            Entry("explicit.shared", "#% increased Life Regeneration rate", "explicit", 0),
            Entry("scourge.shared", "#% increased Life Regeneration rate", "scourge", 1));

        var result = matcher.Match(
            FoulbornUniqueWithoutSourceExact(
                original: "17% increased Life Regeneration rate",
                signature: "<number>% increased Life Regeneration rate",
                projectedExplicitStrength: 90,
                projectedScourgeStrength: 90),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Ambiguous, result.Status);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Contains(result.Candidates, candidate => candidate.StatId == "explicit.shared");
        Assert.Contains(result.Candidates, candidate => candidate.StatId == "scourge.shared");
    }

    [Fact]
    public void Match_SourceExactScourgeEvidence_IsNotOverwrittenToExplicit()
    {
        var catalog = Catalog(
            Entry("explicit.shared", "#% increased Life Regeneration rate", "explicit", 0),
            Entry("scourge.shared", "#% increased Life Regeneration rate", "scourge", 1));

        var result = matcher.Match(
            FoulbornWithSourceExactDomain(
                original: "17% increased Life Regeneration rate",
                signature: "<number>% increased Life Regeneration rate",
                sourceExactDomain: "Scourge",
                sourceExactStrength: 1000,
                projectedExplicitStrength: 90),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("scourge.shared", result.ExactCandidate?.StatId);
        Assert.Equal("scourge", result.ExactCandidate?.ProviderKind);
    }

    [Fact]
    public void Match_NonUniquePrefixDomain_UnchangedAgainstScourgeSibling()
    {
        var catalog = Catalog(
            Entry("explicit.shared", "#% increased Life Regeneration rate", "explicit", 0),
            Entry("scourge.shared", "#% increased Life Regeneration rate", "scourge", 1));

        var result = matcher.Match(
            new ResolvedSearchComponent
            {
                ComponentId = "modifier:0:0",
                SourceModifierIndex = 0,
                OriginalText = "17% increased Life Regeneration rate",
                CanonicalSignature = "<number>% increased Life Regeneration rate",
                ProviderSearchSignatures = ["<number>% increased Life Regeneration rate"],
                ParsedKind = ParsedModifierKind.Prefix,
                UniqueOrigin = ParsedUniqueModifierOrigin.Unspecified,
                ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                ResolvedModifierId = "LifeRegenerationRate1",
                ResolvedStatIds = ["life_regeneration_rate_+%"],
                IsSearchable = true,
                SupportsValueBounds = true,
                ValueBoundShape = ModifierBoundShape.Scalar,
                ProviderDomainEvidence =
                [
                    Evidence("Explicit", "LifeRegenerationRate1", 1000, isSourceExact: true),
                    Evidence("Scourge", "HellscapeUpsideLifeRegenerationRate2", 90, isSourceExact: false),
                ],
            },
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.shared", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_DuplicateEquivalentExplicitCandidates_RemainExactEquivalentSet()
    {
        var catalog = Catalog(
            Entry("explicit.one", "#% increased Life Regeneration rate", "explicit", 0),
            Entry("explicit.two", "#% increased Life Regeneration rate", "explicit", 1),
            Entry("scourge.shared", "#% increased Life Regeneration rate", "scourge", 2));

        var result = matcher.Match(
            FoulbornUniqueSourceExact(
                original: "17% increased Life Regeneration rate",
                signature: "<number>% increased Life Regeneration rate",
                statId: "life_regeneration_rate_+%",
                uniqueDomainStrength: 1000,
                projectedExplicitStrength: 90,
                projectedScourgeStrength: 90),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.ExactEquivalentSet, result.Status);
        Assert.Equal(
            ["explicit.one", "explicit.two"],
            result.ExactEquivalentCandidates.Select(candidate => candidate.StatId).OrderBy(id => id, StringComparer.Ordinal));
        Assert.DoesNotContain(
            result.ExactEquivalentCandidates,
            candidate => candidate.StatId.StartsWith("scourge.", StringComparison.Ordinal));
    }

    private static ResolvedSearchComponent FoulbornUniqueSourceExact(
        string original,
        string signature,
        string statId,
        int uniqueDomainStrength,
        int projectedExplicitStrength,
        int projectedScourgeStrength) =>
        new()
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            OriginalText = original,
            CanonicalSignature = signature,
            ProviderCanonicalSignature = signature,
            ProviderSearchSignatures = [signature],
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = ParsedUniqueModifierOrigin.Foulborn,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "unique-mod:test-source-exact",
            ResolvedStatIds = [statId],
            UniqueCatalogBlockIds = ["unique-block:test"],
            UniqueSourceObservationIds = ["pob-observation:test"],
            UniqueFoulbornRelationshipIds = ["foulborn-rel:test"],
            IsSearchable = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ProviderDomainEvidence =
            [
                Evidence("Unique", "unique-mod:test-source-exact", uniqueDomainStrength, isSourceExact: true, sourceGenerationType: "unique"),
                Evidence("Explicit", "LifeRegenerationRate1", projectedExplicitStrength, isSourceExact: false, sourceGenerationType: "suffix"),
                Evidence("Scourge", "HellscapeUpsideLifeRegenerationRate2", projectedScourgeStrength, isSourceExact: false, sourceGenerationType: "scourge_benefit"),
            ],
        };

    private static ResolvedSearchComponent FoulbornUniqueWithoutSourceExact(
        string original,
        string signature,
        int projectedExplicitStrength,
        int projectedScourgeStrength) =>
        new()
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            OriginalText = original,
            CanonicalSignature = signature,
            ProviderCanonicalSignature = signature,
            ProviderSearchSignatures = [signature],
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = ParsedUniqueModifierOrigin.Foulborn,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "unique-mod:test-projected-only",
            ResolvedStatIds = ["life_regeneration_rate_+%"],
            UniqueCatalogBlockIds = ["unique-block:test"],
            UniqueSourceObservationIds = ["pob-observation:test"],
            UniqueFoulbornRelationshipIds = ["foulborn-rel:test"],
            IsSearchable = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ProviderDomainEvidence =
            [
                Evidence("Explicit", "LifeRegenerationRate1", projectedExplicitStrength, isSourceExact: false, sourceGenerationType: "suffix"),
                Evidence("Scourge", "HellscapeUpsideLifeRegenerationRate2", projectedScourgeStrength, isSourceExact: false, sourceGenerationType: "scourge_benefit"),
            ],
        };

    private static ResolvedSearchComponent FoulbornWithSourceExactDomain(
        string original,
        string signature,
        string sourceExactDomain,
        int sourceExactStrength,
        int projectedExplicitStrength) =>
        new()
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            OriginalText = original,
            CanonicalSignature = signature,
            ProviderCanonicalSignature = signature,
            ProviderSearchSignatures = [signature],
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = ParsedUniqueModifierOrigin.Foulborn,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "unique-mod:test-scourge-source",
            ResolvedStatIds = ["life_regeneration_rate_+%"],
            UniqueCatalogBlockIds = ["unique-block:test"],
            UniqueSourceObservationIds = ["pob-observation:test"],
            UniqueFoulbornRelationshipIds = ["foulborn-rel:test"],
            IsSearchable = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ProviderDomainEvidence =
            [
                Evidence(sourceExactDomain, "HellscapeUpsideLifeRegenerationRate2", sourceExactStrength, isSourceExact: true, sourceGenerationType: "scourge_benefit"),
                Evidence("Explicit", "LifeRegenerationRate1", projectedExplicitStrength, isSourceExact: false, sourceGenerationType: "suffix"),
            ],
        };

    private static SearchComponentProviderDomainEvidence Evidence(
        string domain,
        string modifierId,
        int strength,
        bool isSourceExact,
        string? sourceGenerationType = null) =>
        new()
        {
            ProviderDomain = domain,
            ModifierId = modifierId,
            GenerationType = ModifierGenerationType.Unknown,
            Locality = ModifierLocality.Global,
            SourceGenerationType = sourceGenerationType,
            IsSourceExact = isSourceExact,
            IsProjectedDomain = !isSourceExact,
            EvidenceStrength = strength,
            ApplicabilityReasonCode = isSourceExact ? "SOURCE_EXACT" : "MODIFIER_ELIGIBLE_FOR_BASE",
            ApplicabilityReason = isSourceExact
                ? "The copied modifier resolved exactly to this GameData source family."
                : "Projected eligibility for the item base.",
        };

    private static PathOfExileTradeStatCatalog Catalog(params PathOfExileTradeStatEntry[] entries) =>
        new(entries);

    private static PathOfExileTradeStatEntry Entry(
        string id,
        string text,
        string groupId,
        int providerOrder = 0) =>
        new()
        {
            ProviderOrder = providerOrder,
            GroupId = groupId,
            GroupLabel = groupId,
            Id = id,
            Text = text,
            Type = groupId,
        };
}
