using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradeModifierVariantCatalogAuditor
{
    public static PathOfExileTradeModifierVariantCatalogAuditReport Audit(
        PathOfExileTradeStatCatalog catalog,
        IReadOnlyList<PathOfExileTradeModifierVariantAuditSource> sources)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(sources);

        var effects = sources.Select(source =>
        {
            var discovery = PathOfExileTradeModifierVariantResolver.DiscoverForAudit(
                source.Component,
                catalog,
                source.SourceExactCandidate);
            return new PathOfExileTradeModifierVariantEffectAudit
            {
                Label = source.Label,
                SourceProviderStatId = source.SourceExactCandidate.StatId,
                SourceText = source.Component.OriginalText,
                RawProviderCandidateCount = discovery.Trace.Count,
                RejectedSemanticMismatchCount = discovery.Trace.Count(trace =>
                    trace.RejectionReason.StartsWith(
                        PathOfExileTradeModifierVariantDiscovery.SemanticMismatch,
                        StringComparison.Ordinal)),
                RejectedItemApplicabilityCount = discovery.Trace.Count(trace =>
                    trace.RejectionReason ==
                        PathOfExileTradeModifierVariantDiscovery.ItemApplicabilityUnproven),
                RejectedDuplicateIdentityCount = discovery.Trace.Count(trace =>
                    trace.RejectionReason is
                        PathOfExileTradeModifierVariantDiscovery.DuplicateProviderStatId or
                        PathOfExileTradeModifierVariantDiscovery.DuplicateCanonicalIdentity or
                        PathOfExileTradeModifierVariantDiscovery.WeakerSemanticProvenance),
                RejectedSameKindAmbiguityCount = discovery.Trace.Count(trace =>
                    trace.RejectionReason == PathOfExileTradeModifierVariantDiscovery.SameKindAmbiguous),
                FinalOptionCount = discovery.Candidates.Count,
                FinalProviderKinds = discovery.Candidates
                    .Select(PathOfExileTradeStatCandidateClassifier.GetProviderKind)
                    .Select(DisplayKind)
                    .ToArray(),
                Trace = discovery.Trace,
                Diagnostics = discovery.Diagnostics,
            };
        }).ToArray();

        return new PathOfExileTradeModifierVariantCatalogAuditReport
        {
            RawProviderCandidateCount = effects.Sum(effect => effect.RawProviderCandidateCount),
            RejectedSemanticMismatchCount = effects.Sum(effect => effect.RejectedSemanticMismatchCount),
            RejectedItemApplicabilityCount = effects.Sum(effect => effect.RejectedItemApplicabilityCount),
            RejectedDuplicateIdentityCount = effects.Sum(effect => effect.RejectedDuplicateIdentityCount),
            RejectedSameKindAmbiguityCount = effects.Sum(effect => effect.RejectedSameKindAmbiguityCount),
            Effects = effects,
        };
    }

    private static string DisplayKind(string kind) => string.IsNullOrWhiteSpace(kind)
        ? "Unknown"
        : char.ToUpperInvariant(kind[0]) + kind[1..].ToLowerInvariant();
}

internal sealed record PathOfExileTradeModifierVariantAuditSource
{
    public required string Label { get; init; }

    public required ResolvedSearchComponent Component { get; init; }

    public required PathOfExileTradeStatMatchCandidate SourceExactCandidate { get; init; }
}

internal sealed record PathOfExileTradeModifierVariantCatalogAuditReport
{
    public int RawProviderCandidateCount { get; init; }

    public int RejectedSemanticMismatchCount { get; init; }

    public int RejectedItemApplicabilityCount { get; init; }

    public int RejectedDuplicateIdentityCount { get; init; }

    public int RejectedSameKindAmbiguityCount { get; init; }

    public IReadOnlyList<PathOfExileTradeModifierVariantEffectAudit> Effects { get; init; } = [];
}

internal sealed record PathOfExileTradeModifierVariantEffectAudit
{
    public required string Label { get; init; }

    public required string SourceProviderStatId { get; init; }

    public required string SourceText { get; init; }

    public int RawProviderCandidateCount { get; init; }

    public int RejectedSemanticMismatchCount { get; init; }

    public int RejectedItemApplicabilityCount { get; init; }

    public int RejectedDuplicateIdentityCount { get; init; }

    public int RejectedSameKindAmbiguityCount { get; init; }

    public int FinalOptionCount { get; init; }

    public IReadOnlyList<string> FinalProviderKinds { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeModifierVariantCandidateTrace> Trace { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeModifierVariantDiscoveryDiagnostic> Diagnostics { get; init; } = [];
}
