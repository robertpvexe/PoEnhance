using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradePseudoVariantCatalogAuditor
{
    public static PathOfExileTradePseudoVariantCatalogAuditReport Audit(
        PathOfExileTradeStatCatalog catalog,
        IReadOnlyList<PathOfExileTradePseudoVariantAuditSource> logicalEffects)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(logicalEffects);

        var pseudoEntries = catalog.Entries
            .Select(PathOfExileTradeStatCandidateClassifier.ToCandidate)
            .Where(candidate => string.Equals(
                PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate),
                "pseudo",
                StringComparison.Ordinal))
            .ToArray();
        var distinctPseudoEntries = pseudoEntries
            .DistinctBy(candidate => candidate.StatId, StringComparer.Ordinal)
            .ToArray();
        var entries = distinctPseudoEntries
            .Select(candidate => Classify(candidate, logicalEffects))
            .ToArray();
        var ambiguities = logicalEffects
            .Select(source => new PathOfExileTradePseudoVariantAuditAmbiguity
            {
                LogicalEffect = source.Component.OriginalText,
                SourceProviderStatId = source.SourceExactCandidate.StatId,
                CompatiblePseudoStatIds = distinctPseudoEntries
                    .Where(candidate => PathOfExileTradePseudoVariantCompatibility
                        .Evaluate(source.Component, source.SourceExactCandidate, candidate)
                        .IsCompatible)
                    .Select(candidate => candidate.StatId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(statId => statId, StringComparer.Ordinal)
                    .ToArray(),
            })
            .Where(ambiguity => ambiguity.CompatiblePseudoStatIds.Count > 1)
            .DistinctBy(
                ambiguity => $"{ambiguity.SourceProviderStatId}\u001f{string.Join('\u001e', ambiguity.CompatiblePseudoStatIds)}",
                StringComparer.Ordinal)
            .ToArray();

        return new PathOfExileTradePseudoVariantCatalogAuditReport
        {
            TotalOfficialPseudoStatsInspected = pseudoEntries.Length,
            DistinctPseudoProviderStatCount = distinctPseudoEntries.Length,
            MatchedToLogicalEffectCount = entries.Count(entry => entry.Classification ==
                PathOfExileTradePseudoVariantAuditClassification.Matched),
            UnreachableCount = entries.Count(entry => entry.Classification ==
                PathOfExileTradePseudoVariantAuditClassification.Unreachable),
            RejectedIncompatibleCount = entries.Count(entry => entry.Classification ==
                PathOfExileTradePseudoVariantAuditClassification.RejectedIncompatible),
            NewlyCompatibleCount = entries.Count(entry => entry.WasUnreachableByLegacyDiscovery),
            DuplicateProviderIdentitiesRemoved = pseudoEntries.Length - distinctPseudoEntries.Length,
            RejectionReasonCounts = entries
                .Where(entry => entry.Classification ==
                    PathOfExileTradePseudoVariantAuditClassification.RejectedIncompatible)
                .GroupBy(entry => entry.RejectionCode!, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            Entries = entries,
            Ambiguities = ambiguities,
        };
    }

    private static PathOfExileTradePseudoVariantAuditEntry Classify(
        PathOfExileTradeStatMatchCandidate candidate,
        IReadOnlyList<PathOfExileTradePseudoVariantAuditSource> logicalEffects)
    {
        var evaluations = logicalEffects
            .Select(source => new
            {
                Source = source,
                Result = PathOfExileTradePseudoVariantCompatibility.Evaluate(
                    source.Component,
                    source.SourceExactCandidate,
                    candidate),
            })
            .ToArray();
        var compatible = evaluations
            .Where(evaluation => evaluation.Result.IsCompatible)
            .ToArray();
        if (compatible.Length > 0)
        {
            return new PathOfExileTradePseudoVariantAuditEntry
            {
                StatId = candidate.StatId,
                Text = candidate.Text,
                Classification = PathOfExileTradePseudoVariantAuditClassification.Matched,
                LogicalEffectMatches = compatible
                    .Select(evaluation => evaluation.Source.Component.OriginalText)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                BestCompatibilityScore = compatible.Max(evaluation => evaluation.Result.CompatibilityScore),
                MaximumCompatibilityScore = compatible.Max(evaluation => evaluation.Result.MaximumCompatibilityScore),
                WasUnreachableByLegacyDiscovery = !compatible.Any(evaluation =>
                    evaluation.Result.LegacyDiscoveryCompatible),
            };
        }

        var sameProperty = evaluations
            .Where(evaluation => string.Equals(
                evaluation.Result.SourceLogicalEffect,
                evaluation.Result.CandidateLogicalEffect,
                StringComparison.Ordinal))
            .OrderByDescending(evaluation => evaluation.Result.CompatibilityScore)
            .ToArray();
        if (sameProperty.Length == 0)
        {
            return new PathOfExileTradePseudoVariantAuditEntry
            {
                StatId = candidate.StatId,
                Text = candidate.Text,
                Classification = PathOfExileTradePseudoVariantAuditClassification.Unreachable,
            };
        }

        var best = sameProperty[0];
        return new PathOfExileTradePseudoVariantAuditEntry
        {
            StatId = candidate.StatId,
            Text = candidate.Text,
            Classification = PathOfExileTradePseudoVariantAuditClassification.RejectedIncompatible,
            RejectionCode = best.Result.RejectionCode,
            LogicalEffectMatches = sameProperty
                .Select(evaluation => evaluation.Source.Component.OriginalText)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            BestCompatibilityScore = best.Result.CompatibilityScore,
            MaximumCompatibilityScore = best.Result.MaximumCompatibilityScore,
        };
    }
}

internal sealed record PathOfExileTradePseudoVariantAuditSource
{
    public required ResolvedSearchComponent Component { get; init; }

    public required PathOfExileTradeStatMatchCandidate SourceExactCandidate { get; init; }
}

internal sealed record PathOfExileTradePseudoVariantCatalogAuditReport
{
    public int TotalOfficialPseudoStatsInspected { get; init; }

    public int DistinctPseudoProviderStatCount { get; init; }

    public int MatchedToLogicalEffectCount { get; init; }

    public int UnreachableCount { get; init; }

    public int RejectedIncompatibleCount { get; init; }

    public int NewlyCompatibleCount { get; init; }

    public int DuplicateProviderIdentitiesRemoved { get; init; }

    public IReadOnlyDictionary<string, int> RejectionReasonCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyList<PathOfExileTradePseudoVariantAuditEntry> Entries { get; init; } = [];

    public IReadOnlyList<PathOfExileTradePseudoVariantAuditAmbiguity> Ambiguities { get; init; } = [];
}

internal sealed record PathOfExileTradePseudoVariantAuditEntry
{
    public required string StatId { get; init; }

    public required string Text { get; init; }

    public PathOfExileTradePseudoVariantAuditClassification Classification { get; init; }

    public string? RejectionCode { get; init; }

    public IReadOnlyList<string> LogicalEffectMatches { get; init; } = [];

    public int BestCompatibilityScore { get; init; }

    public int MaximumCompatibilityScore { get; init; }

    public bool WasUnreachableByLegacyDiscovery { get; init; }
}

internal sealed record PathOfExileTradePseudoVariantAuditAmbiguity
{
    public required string LogicalEffect { get; init; }

    public required string SourceProviderStatId { get; init; }

    public IReadOnlyList<string> CompatiblePseudoStatIds { get; init; } = [];
}

internal enum PathOfExileTradePseudoVariantAuditClassification
{
    Matched,
    Unreachable,
    RejectedIncompatible,
}
