using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradeModifierVariantDiscovery
{
    public const string Accepted = "Accepted";
    public const string SemanticMismatch = "SemanticMismatch";
    public const string ProviderKindUnknown = "ProviderKindUnknown";
    public const string ItemApplicabilityUnproven = "ItemApplicabilityUnproven";
    public const string DuplicateProviderStatId = "DuplicateProviderStatId";
    public const string DuplicateCanonicalIdentity = "DuplicateCanonicalIdentity";
    public const string WeakerSemanticProvenance = "WeakerSemanticProvenance";
    public const string SameKindAmbiguous = "SameKindAmbiguous";

    public static PathOfExileTradeModifierVariantDiscoveryResult Discover(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        PathOfExileTradeStatMatchCandidate sourceExactCandidate)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(sourceExactCandidate);

        var sourceEffect = PathOfExileTradePseudoVariantCompatibility.LogicalEffectIdentity(
            sourceExactCandidate.Text);
        var sourceDomains = SourceDomains(component);
        var working = catalog.FindCandidatesByLogicalEffect(sourceEffect)
            .Select((candidate, index) => Evaluate(
                index,
                component,
                sourceExactCandidate,
                candidate,
                sourceDomains))
            .ToList();

        if (!working.Any(candidate => string.Equals(
                candidate.Candidate.StatId,
                sourceExactCandidate.StatId,
                StringComparison.Ordinal)))
        {
            working.Add(Evaluate(
                working.Count,
                component,
                sourceExactCandidate,
                sourceExactCandidate,
                sourceDomains));
        }

        var diagnostics = working
            .Where(candidate => string.Equals(
                candidate.Trace.RejectionReason,
                $"{SemanticMismatch}:" +
                    PathOfExileTradeProviderLocalityCompatibility.AmbiguousLocalityEvidence,
                StringComparison.Ordinal))
            .GroupBy(candidate => PathOfExileTradeStatCandidateClassifier.GetProviderKind(
                candidate.Candidate), StringComparer.Ordinal)
            .Select(group => new PathOfExileTradeModifierVariantDiscoveryDiagnostic
            {
                Code = PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantLocalityAmbiguous,
                Message = $"Excluded ambiguous {DisplayKind(group.Key)} Trade locality; applicable GameData families contain both Local and Global semantics.",
                ProviderKind = group.Key,
                ProviderStatIds = group.Select(candidate => candidate.Candidate.StatId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(statId => statId, StringComparer.Ordinal)
                    .ToArray(),
            })
            .ToList();
        var eligible = working.Where(candidate => candidate.IsEligible).ToList();
        eligible = DeduplicateExactProviderIds(eligible, working, component, sourceExactCandidate);
        eligible = DeduplicateGroups(
            eligible,
            working,
            component,
            sourceExactCandidate,
            CanonicalProviderIdentity,
            DuplicateCanonicalIdentity,
            diagnostics);
        eligible = DeduplicateGroups(
            eligible,
            working,
            component,
            sourceExactCandidate,
            candidate => PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate),
            WeakerSemanticProvenance,
            diagnostics);

        var finalIndexes = eligible.Select(candidate => candidate.Index).ToHashSet();
        var trace = working
            .OrderBy(candidate => candidate.Candidate.ProviderOrder)
            .ThenBy(candidate => candidate.Index)
            .Select(candidate => candidate.Trace with
            {
                IsAccepted = finalIndexes.Contains(candidate.Index),
                RejectionReason = finalIndexes.Contains(candidate.Index)
                    ? Accepted
                    : candidate.Trace.RejectionReason,
            })
            .ToArray();

        return new PathOfExileTradeModifierVariantDiscoveryResult
        {
            Candidates = eligible
                .OrderBy(candidate => candidate.Candidate.ProviderOrder)
                .ThenBy(candidate => candidate.Candidate.StatId, StringComparer.Ordinal)
                .Select(candidate => candidate.Candidate)
                .ToArray(),
            Diagnostics = diagnostics,
            Trace = trace,
        };
    }

    private static WorkingCandidate Evaluate(
        int index,
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate source,
        PathOfExileTradeStatMatchCandidate candidate,
        IReadOnlyList<string> sourceDomains)
    {
        var compatibility = PathOfExileTradePseudoVariantCompatibility.EvaluateVariant(
            component,
            source,
            candidate);
        var kind = PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate);
        var evidence = component.ProviderDomainEvidence
            .Where(entry => string.Equals(
                entry.ProviderDomain,
                kind,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var isSourceIdentity = string.Equals(
            candidate.StatId,
            source.StatId,
            StringComparison.Ordinal);
        var hasContributorIdentity = component.Sources.Any(entry => string.Equals(
            entry.ProviderIdentity,
            PathOfExileTradeProviderIdentity.Create(candidate.StatId),
            StringComparison.Ordinal));
        var hasSourceDomainProvenance = component.Sources.Any(entry =>
            !string.IsNullOrWhiteSpace(entry.ResolvedModifierId) &&
            string.Equals(entry.ProviderDomain, kind, StringComparison.OrdinalIgnoreCase));
        var isPseudo = string.Equals(kind, "pseudo", StringComparison.Ordinal);
        var kindKnown = !string.Equals(
            kind,
            PathOfExileTradeStatCandidateClassifier.UnknownProviderKind,
            StringComparison.Ordinal);
        var sourceIdentityWithoutProjectableBounds = isSourceIdentity &&
            compatibility.LocalityDecision.IsCompatible &&
            compatibility.RejectionCode is
                PathOfExileTradePseudoVariantCompatibility.IncompatibleProjection or
                PathOfExileTradePseudoVariantCompatibility.IncompatibleTranslationProjection;
        var semanticCompatible = compatibility.IsCompatible || sourceIdentityWithoutProjectableBounds;
        var itemApplicable = isPseudo || isSourceIdentity || hasContributorIdentity ||
            hasSourceDomainProvenance || evidence.Length > 0;
        var isEligible = semanticCompatible && kindKnown && itemApplicable;
        var reason = !semanticCompatible
            ? $"{SemanticMismatch}:{compatibility.RejectionCode}"
            : !kindKnown
                ? ProviderKindUnknown
                : !itemApplicable
                    ? ItemApplicabilityUnproven
                    : Accepted;
        var applicability = isPseudo
            ? "Pseudo totals do not require one source modifier domain."
            : isSourceIdentity
                ? "The official identity is the exact source match."
                : hasContributorIdentity
                    ? "The official identity is retained contributor provenance."
                    : hasSourceDomainProvenance
                        ? "A resolved source contributor proves this GameData provider domain."
                        : evidence.Length > 0
                            ? string.Join(" | ", evidence.Select(entry =>
                                $"{entry.ModifierId}: {entry.ApplicabilityReason}"))
                            : "No applicable GameData source family proves this provider domain for the resolved item base.";
        var itemBaseContext = component.ProviderDomainEvidence
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ItemBaseId) ||
                !string.IsNullOrWhiteSpace(entry.ItemClass))
            .Select(entry => $"{entry.ItemClass ?? "<unknown class>"} | {entry.ItemBaseId ?? "<unknown base>"}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new WorkingCandidate(
            index,
            candidate,
            isEligible,
            new PathOfExileTradeModifierVariantCandidateTrace
            {
                CandidateIndex = index,
                ProviderStatId = candidate.StatId,
                ProviderKind = kind,
                OfficialTemplate = candidate.Text,
                NormalizedLogicalEffect = compatibility.CandidateLogicalEffect,
                ProviderLocality = candidate.ProviderLocality,
                NumericUnit = string.Join(",", compatibility.CandidateNumericSemantics
                    .Select(value => value.TrimStart('+', '-'))),
                NumericArity = compatibility.CandidateNumericSemantics.Count,
                NumericSemantics = compatibility.CandidateNumericSemantics,
                TranslationHandlers = compatibility.TranslationHandlers,
                BoundDirection = compatibility.BoundDirection,
                SourceModifierDomains = sourceDomains,
                ItemBaseContext = itemBaseContext,
                ItemApplicability = applicability,
                ApplicabilityEvidenceStrength = evidence.Select(entry => entry.EvidenceStrength).DefaultIfEmpty().Max(),
                CompatibilityScore = compatibility.CompatibilityScore + (itemApplicable ? 1 : 0),
                MaximumCompatibilityScore = compatibility.MaximumCompatibilityScore + 1,
                IsAccepted = false,
                RejectionReason = reason,
            });
    }

    private static List<WorkingCandidate> DeduplicateExactProviderIds(
        IReadOnlyList<WorkingCandidate> eligible,
        IReadOnlyList<WorkingCandidate> all,
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate source)
    {
        var retained = new List<WorkingCandidate>();
        foreach (var group in eligible.GroupBy(
                     candidate => candidate.Candidate.StatId,
                     StringComparer.Ordinal))
        {
            var selected = group
                .OrderByDescending(candidate => ProvenanceScore(component, candidate.Candidate, source))
                .ThenBy(candidate => candidate.Candidate.ProviderOrder)
                .ThenBy(candidate => candidate.Index)
                .First();
            retained.Add(selected);
            foreach (var duplicate in group.Where(candidate => candidate.Index != selected.Index))
            {
                Reject(all, duplicate.Index, DuplicateProviderStatId);
            }
        }

        return retained;
    }

    private static List<WorkingCandidate> DeduplicateGroups(
        IReadOnlyList<WorkingCandidate> eligible,
        IReadOnlyList<WorkingCandidate> all,
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate source,
        Func<PathOfExileTradeStatMatchCandidate, string> keySelector,
        string weakerReason,
        ICollection<PathOfExileTradeModifierVariantDiscoveryDiagnostic> diagnostics)
    {
        var retained = new List<WorkingCandidate>();
        foreach (var group in eligible.GroupBy(candidate => keySelector(candidate.Candidate), StringComparer.Ordinal))
        {
            var candidates = group.ToArray();
            if (candidates.Length == 1)
            {
                retained.Add(candidates[0]);
                continue;
            }

            var ranked = candidates
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Score = ProvenanceScore(component, candidate.Candidate, source),
                })
                .OrderByDescending(entry => entry.Score)
                .ToArray();
            if (ranked.Length == 1 || ranked[0].Score > ranked[1].Score)
            {
                retained.Add(ranked[0].Candidate);
                foreach (var weaker in ranked.Skip(1))
                {
                    Reject(all, weaker.Candidate.Index, weakerReason);
                }

                continue;
            }

            var kind = PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidates[0].Candidate);
            var statIds = candidates
                .Select(candidate => candidate.Candidate.StatId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(statId => statId, StringComparer.Ordinal)
                .ToArray();
            foreach (var ambiguous in candidates)
            {
                Reject(all, ambiguous.Index, SameKindAmbiguous);
            }

            diagnostics.Add(new PathOfExileTradeModifierVariantDiscoveryDiagnostic
            {
                Code = PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantKindAmbiguous,
                Message = $"Excluded ambiguous {DisplayKind(kind)} Trade variants [{string.Join(", ", statIds)}]; no candidate had stronger locality, GameData provenance, or item applicability.",
                ProviderKind = kind,
                ProviderStatIds = statIds,
            });
        }

        return retained;
    }

    private static void Reject(
        IReadOnlyList<WorkingCandidate> candidates,
        int index,
        string reason)
    {
        var candidate = candidates.Single(entry => entry.Index == index);
        candidate.Trace = candidate.Trace with { RejectionReason = reason };
    }

    private static int ProvenanceScore(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate candidate,
        PathOfExileTradeStatMatchCandidate source)
    {
        var score = 0;
        if (string.Equals(candidate.StatId, source.StatId, StringComparison.Ordinal))
        {
            score += 1000;
        }

        if (string.Equals(
                PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate),
                PathOfExileTradeStatCandidateClassifier.GetProviderKind(source),
                StringComparison.Ordinal))
        {
            score += 100;
        }

        if (string.Equals(candidate.NormalizedTemplate, source.NormalizedTemplate, StringComparison.Ordinal))
        {
            score += 20;
        }

        var localityDecision = PathOfExileTradeProviderLocalityCompatibility.EvaluateVariant(
            component,
            source,
            candidate);
        var candidateMarker = candidate.ProviderLocality switch
        {
            PathOfExileTradeProviderStatLocality.Local => ModifierLocality.Local,
            PathOfExileTradeProviderStatLocality.Global => ModifierLocality.Global,
            _ => ModifierLocality.Unknown,
        };
        if (localityDecision.IsCompatible &&
            candidateMarker != ModifierLocality.Unknown &&
            candidateMarker == localityDecision.EffectiveLocality)
        {
            score += 10;
        }

        var kind = PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate);
        score += component.ProviderDomainEvidence
            .Where(evidence => string.Equals(
                evidence.ProviderDomain,
                kind,
                StringComparison.OrdinalIgnoreCase))
            .Select(evidence => evidence.EvidenceStrength)
            .DefaultIfEmpty()
            .Max();

        return score;
    }

    private static string CanonicalProviderIdentity(PathOfExileTradeStatMatchCandidate candidate)
    {
        return string.Join(
            '\u001f',
            PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate),
            candidate.NormalizedTemplate.ToLowerInvariant(),
            candidate.ProviderLocality,
            string.Join('\u001e', candidate.OptionMetadata
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => $"{entry.Key}={entry.Value}")));
    }

    private static IReadOnlyList<string> SourceDomains(ResolvedSearchComponent component)
    {
        var domains = component.Sources
            .Select(source => source.ProviderDomain)
            .Concat(component.ProviderDomainEvidence
                .Where(evidence => evidence.IsSourceExact)
                .Select(evidence => evidence.ProviderDomain))
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (domains.Length > 0)
        {
            return domains;
        }

        return
        [
            component.IsFractured ? "Fractured" :
            component.IsCrafted ? "Crafted" :
            component.IsVeiled ? "Veiled" :
            component.IsBaseImplicit ? "Implicit" :
            component.ParsedKind is PoEnhance.Core.Items.Parsing.ParsedModifierKind.Prefix or
                PoEnhance.Core.Items.Parsing.ParsedModifierKind.Suffix ? "Explicit" :
            component.ParsedKind == PoEnhance.Core.Items.Parsing.ParsedModifierKind.Implicit ? "Implicit" :
            "Unknown",
        ];
    }

    private static string DisplayKind(string kind) => string.IsNullOrWhiteSpace(kind)
        ? "Unknown"
        : char.ToUpperInvariant(kind[0]) + kind[1..].ToLowerInvariant();

    private sealed class WorkingCandidate(
        int index,
        PathOfExileTradeStatMatchCandidate candidate,
        bool isEligible,
        PathOfExileTradeModifierVariantCandidateTrace trace)
    {
        public int Index { get; } = index;

        public PathOfExileTradeStatMatchCandidate Candidate { get; } = candidate;

        public bool IsEligible { get; } = isEligible;

        public PathOfExileTradeModifierVariantCandidateTrace Trace { get; set; } = trace;
    }
}

internal sealed record PathOfExileTradeModifierVariantDiscoveryResult
{
    public IReadOnlyList<PathOfExileTradeStatMatchCandidate> Candidates { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeModifierVariantDiscoveryDiagnostic> Diagnostics { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeModifierVariantCandidateTrace> Trace { get; init; } = [];
}

internal sealed record PathOfExileTradeModifierVariantDiscoveryDiagnostic
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public required string ProviderKind { get; init; }

    public IReadOnlyList<string> ProviderStatIds { get; init; } = [];
}

internal sealed record PathOfExileTradeModifierVariantCandidateTrace
{
    public int CandidateIndex { get; init; }

    public required string ProviderStatId { get; init; }

    public required string ProviderKind { get; init; }

    public required string OfficialTemplate { get; init; }

    public required string NormalizedLogicalEffect { get; init; }

    public PathOfExileTradeProviderStatLocality ProviderLocality { get; init; }

    public required string NumericUnit { get; init; }

    public int NumericArity { get; init; }

    public IReadOnlyList<string> NumericSemantics { get; init; } = [];

    public IReadOnlyList<IReadOnlyList<string>> TranslationHandlers { get; init; } = [];

    public ModifierBoundDirection BoundDirection { get; init; }

    public IReadOnlyList<string> SourceModifierDomains { get; init; } = [];

    public IReadOnlyList<string> ItemBaseContext { get; init; } = [];

    public required string ItemApplicability { get; init; }

    public int ApplicabilityEvidenceStrength { get; init; }

    public int CompatibilityScore { get; init; }

    public int MaximumCompatibilityScore { get; init; }

    public bool IsAccepted { get; init; }

    public required string RejectionReason { get; init; }
}
