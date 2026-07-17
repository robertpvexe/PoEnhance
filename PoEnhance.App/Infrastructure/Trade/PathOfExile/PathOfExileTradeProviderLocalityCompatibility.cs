using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradeProviderLocalityCompatibility
{
    public const string Compatible = "Compatible";
    public const string ExplicitLocalityConflict = "ExplicitLocalityConflict";
    public const string AmbiguousLocalityEvidence = "AmbiguousLocalityEvidence";
    public const string InsufficientLocalityEvidence = "InsufficientLocalityEvidence";
    public const string ProviderDomainConflict = "ProviderDomainConflict";
    public const string RetainedIdentityIncomplete = "RetainedIdentityIncomplete";
    public const string RetainedIdentitySemanticConflict = "RetainedIdentitySemanticConflict";
    public const string LocalDisplayedScopeUnproven = "LocalDisplayedScopeUnproven";

    public static PathOfExileTradeProviderLocalityDecision EvaluateVariant(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate sourceCandidate,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(sourceCandidate);
        ArgumentNullException.ThrowIfNull(candidate);

        var candidateKind = PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate);
        var candidateIdentity = PathOfExileTradeProviderIdentity.Create(candidate.StatId);
        var localDisplayedScope = RequireProvenLocalDisplayedPseudoScope(
            component.ReviewedItemPropertySemantic,
            candidateKind,
            candidate);
        if (localDisplayedScope is not null)
        {
            return localDisplayedScope;
        }

        var retained = Decide(
            component.Sources
                .Where(source =>
                    source.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact &&
                    string.Equals(source.ProviderIdentity, candidateIdentity, StringComparison.Ordinal))
                .Select(source => source.Locality),
            "ExactRetainedSourceIdentity");
        if (!retained.IsUnknown)
        {
            return ApplyProviderMarker(retained, candidate);
        }

        var exactFamilies = component.Sources
            .Where(source =>
                !string.IsNullOrWhiteSpace(source.ResolvedModifierId) &&
                source.ResolvedStatIds.Count > 0 &&
                (string.Equals(candidateKind, "pseudo", StringComparison.Ordinal) ||
                    string.Equals(source.ProviderDomain, candidateKind, StringComparison.OrdinalIgnoreCase)))
            .Select(source => source.Locality)
            .Concat(component.ProviderDomainEvidence
                .Where(evidence => evidence.IsSourceExact &&
                    (string.Equals(candidateKind, "pseudo", StringComparison.Ordinal) ||
                        string.Equals(evidence.ProviderDomain, candidateKind, StringComparison.OrdinalIgnoreCase)))
                .Select(evidence => evidence.Locality));
        if (string.Equals(candidate.StatId, sourceCandidate.StatId, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(component.ResolvedModifierId) &&
            component.ResolvedStatIds.Count > 0)
        {
            exactFamilies = exactFamilies.Append(component.Locality);
        }
        else if (string.Equals(candidateKind, "pseudo", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(component.ResolvedModifierId) &&
            component.ResolvedStatIds.Count > 0)
        {
            exactFamilies = exactFamilies.Append(component.Locality);
        }

        var exact = Decide(exactFamilies, "ExactGameDataSourceFamily");
        if (!exact.IsUnknown)
        {
            return ApplyProviderMarker(exact, candidate);
        }

        var contextual = Decide(
            component.ProviderDomainEvidence
                .Where(evidence =>
                    !evidence.IsSourceExact &&
                    (string.Equals(candidateKind, "pseudo", StringComparison.Ordinal) ||
                        string.Equals(evidence.ProviderDomain, candidateKind, StringComparison.OrdinalIgnoreCase)))
                .Select(evidence => evidence.Locality),
            "ContextualGameDataFamilies");
        if (!contextual.IsUnknown)
        {
            return ApplyProviderMarker(contextual, candidate);
        }

        return ApplyProviderMarker(Unknown("No applicable GameData locality evidence."), candidate);
    }

    public static PathOfExileTradeProviderLocalityDecision EvaluateRetainedSourceIdentity(
        SearchComponentSourceProvenance source,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidate);

        var candidateKind = PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate);
        var localDisplayedScope = RequireProvenLocalDisplayedPseudoScope(
            source.ReviewedItemPropertySemantic,
            candidateKind,
            candidate);
        if (localDisplayedScope is not null)
        {
            return localDisplayedScope;
        }

        if (!string.IsNullOrWhiteSpace(source.ProviderDomain) &&
            !string.Equals(source.ProviderDomain, candidateKind, StringComparison.OrdinalIgnoreCase))
        {
            return Incompatible(
                ProviderDomainConflict,
                source.Locality,
                "The retained source provider domain does not match the retained Trade identity.");
        }

        if (source.ProviderResolutionStatus != SearchComponentProviderResolutionStatus.Exact ||
            string.IsNullOrWhiteSpace(source.ProviderIdentity) ||
            string.IsNullOrWhiteSpace(source.ResolvedModifierId) ||
            source.ResolvedStatIds.Count == 0)
        {
            return Incompatible(
                RetainedIdentityIncomplete,
                source.Locality,
                "The retained source identity lacks exact GameData modifier/stat provenance.");
        }

        if (!string.Equals(
                source.ProviderIdentity,
                PathOfExileTradeProviderIdentity.Create(candidate.StatId),
                StringComparison.Ordinal) ||
            !HasCompatibleRetainedSemantics(source, candidate))
        {
            return Incompatible(
                RetainedIdentitySemanticConflict,
                source.Locality,
                "The retained Trade identity no longer matches the source effect, unit, arity, or projection semantics.");
        }

        return ApplyProviderMarker(
            FromKnownLocality(source.Locality, "ExactRetainedSourceIdentity"),
            candidate);
    }

    private static bool HasCompatibleRetainedSemantics(
        SearchComponentSourceProvenance source,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        var sourceTemplate = source.CanonicalSignature
            .Replace("+<number>", "+#", StringComparison.Ordinal)
            .Replace("-<number>", "-#", StringComparison.Ordinal)
            .Replace("<number>", "#", StringComparison.Ordinal);
        var sourceCandidate = candidate with
        {
            Text = sourceTemplate,
            NormalizedTemplate = PathOfExileTradeStatTemplateNormalizer.NormalizeTemplate(sourceTemplate),
            LookupTemplate = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(sourceTemplate),
        };
        var sameEffect = string.Equals(
            PathOfExileTradePseudoVariantCompatibility.LogicalEffectIdentity(sourceTemplate),
            PathOfExileTradePseudoVariantCompatibility.LogicalEffectIdentity(candidate.Text),
            StringComparison.Ordinal);
        var numericCompatible = PathOfExileTradePseudoVariantCompatibility
            .HasCompatibleNumericSemantics(sourceCandidate, candidate);
        var arity = PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(candidate.Text);
        var projectionCompatible = source.ValueBoundShape switch
        {
            ModifierBoundShape.Scalar => arity == 1,
            ModifierBoundShape.ArithmeticMeanRange => arity == 2,
            ModifierBoundShape.PresenceOnly => arity == 0,
            ModifierBoundShape.Unsupported => true,
            _ => false,
        };
        var transformsCompatible = source.TranslationHandlers.Count == 0 ||
            source.TranslationHandlers.Count == arity;
        return sameEffect && numericCompatible && projectionCompatible && transformsCompatible;
    }

    public static PathOfExileTradeProviderLocalityDecision EvaluateExactGameDataMatch(
        ModifierLocality sourceLocality,
        bool hasExactGameDataProvenance,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!hasExactGameDataProvenance &&
            candidate.ProviderLocality == PathOfExileTradeProviderStatLocality.Unmarked)
        {
            return ApplyProviderMarker(
                Unknown("The candidate has no exact GameData modifier/stat provenance."),
                candidate);
        }

        return ApplyProviderMarker(
            FromKnownLocality(sourceLocality, "ExactGameDataStatVector"),
            candidate);
    }

    private static PathOfExileTradeProviderLocalityDecision Decide(
        IEnumerable<ModifierLocality> evidence,
        string source)
    {
        var known = evidence
            .Where(locality => locality is ModifierLocality.Local or ModifierLocality.Global)
            .Distinct()
            .ToArray();
        return known.Length switch
        {
            0 => Unknown($"{source} supplied no known locality."),
            1 => Accepted(known[0], source),
            _ => new PathOfExileTradeProviderLocalityDecision
            {
                Status = PathOfExileTradeProviderLocalityDecisionStatus.Ambiguous,
                EffectiveLocality = ModifierLocality.Unknown,
                ReasonCode = AmbiguousLocalityEvidence,
                EvidenceSource = source,
                Reason = $"{source} contains conflicting Local and Global evidence.",
            },
        };
    }

    private static PathOfExileTradeProviderLocalityDecision FromKnownLocality(
        ModifierLocality locality,
        string source)
    {
        return locality is ModifierLocality.Local or ModifierLocality.Global
            ? Accepted(locality, source)
            : Unknown($"{source} did not resolve Local or Global locality.");
    }

    private static PathOfExileTradeProviderLocalityDecision ApplyProviderMarker(
        PathOfExileTradeProviderLocalityDecision evidence,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        if (evidence.Status == PathOfExileTradeProviderLocalityDecisionStatus.Ambiguous)
        {
            return evidence;
        }

        var markerLocality = candidate.ProviderLocality switch
        {
            PathOfExileTradeProviderStatLocality.Local => ModifierLocality.Local,
            PathOfExileTradeProviderStatLocality.Global => ModifierLocality.Global,
            _ => ModifierLocality.Unknown,
        };
        if (markerLocality == ModifierLocality.Unknown)
        {
            return evidence.IsResolved
                ? evidence
                : evidence with
                {
                    Status = PathOfExileTradeProviderLocalityDecisionStatus.InsufficientEvidence,
                    ReasonCode = InsufficientLocalityEvidence,
                };
        }

        if (!evidence.IsResolved)
        {
            return evidence with
            {
                Status = PathOfExileTradeProviderLocalityDecisionStatus.InsufficientEvidence,
                EffectiveLocality = ModifierLocality.Unknown,
                ReasonCode = InsufficientLocalityEvidence,
                EvidenceSource = "ExplicitProviderMarker",
                Reason = $"The provider template is explicitly {markerLocality}, but compatible source semantics are unknown.",
            };
        }

        return evidence.EffectiveLocality == markerLocality
            ? evidence
            : Incompatible(
                ExplicitLocalityConflict,
                evidence.EffectiveLocality,
                $"The provider template is explicitly {markerLocality}, but stronger GameData evidence is {evidence.EffectiveLocality}.",
                evidence.EvidenceSource);
    }

    private static PathOfExileTradeProviderLocalityDecision? RequireProvenLocalDisplayedPseudoScope(
        ItemPropertySemanticDescriptor? reviewedSemantic,
        string candidateKind,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        if (reviewedSemantic?.Applicability != ItemPropertyApplicability.UnconditionalDisplayedLocal ||
            !string.Equals(candidateKind, "pseudo", StringComparison.Ordinal) ||
            candidate.ProviderLocality == PathOfExileTradeProviderStatLocality.Local)
        {
            return null;
        }

        return Incompatible(
            LocalDisplayedScopeUnproven,
            ModifierLocality.Local,
            "A Pseudo Trade identity for a reviewed local displayed-item property must explicitly prove local scope in the official provider catalog.",
            "ReviewedItemPropertySemantic");
    }

    private static PathOfExileTradeProviderLocalityDecision Accepted(
        ModifierLocality locality,
        string source)
    {
        return new PathOfExileTradeProviderLocalityDecision
        {
            Status = PathOfExileTradeProviderLocalityDecisionStatus.Compatible,
            EffectiveLocality = locality,
            ReasonCode = Compatible,
            EvidenceSource = source,
            Reason = $"{source} resolves the provider identity as {locality}.",
        };
    }

    private static PathOfExileTradeProviderLocalityDecision Unknown(string reason)
    {
        return new PathOfExileTradeProviderLocalityDecision
        {
            Status = PathOfExileTradeProviderLocalityDecisionStatus.InsufficientEvidence,
            EffectiveLocality = ModifierLocality.Unknown,
            ReasonCode = InsufficientLocalityEvidence,
            EvidenceSource = "None",
            Reason = reason,
        };
    }

    private static PathOfExileTradeProviderLocalityDecision Incompatible(
        string reasonCode,
        ModifierLocality locality,
        string reason,
        string evidenceSource = "ExactRetainedSourceIdentity")
    {
        return new PathOfExileTradeProviderLocalityDecision
        {
            Status = PathOfExileTradeProviderLocalityDecisionStatus.Incompatible,
            EffectiveLocality = locality,
            ReasonCode = reasonCode,
            EvidenceSource = evidenceSource,
            Reason = reason,
        };
    }
}

internal enum PathOfExileTradeProviderLocalityDecisionStatus
{
    Compatible,
    Incompatible,
    Ambiguous,
    InsufficientEvidence,
}

internal sealed record PathOfExileTradeProviderLocalityDecision
{
    public PathOfExileTradeProviderLocalityDecisionStatus Status { get; init; }

    public ModifierLocality EffectiveLocality { get; init; } = ModifierLocality.Unknown;

    public required string ReasonCode { get; init; }

    public required string EvidenceSource { get; init; }

    public required string Reason { get; init; }

    public bool IsCompatible => Status == PathOfExileTradeProviderLocalityDecisionStatus.Compatible;

    public bool IsResolved => EffectiveLocality is ModifierLocality.Local or ModifierLocality.Global;

    public bool IsUnknown => Status == PathOfExileTradeProviderLocalityDecisionStatus.InsufficientEvidence;
}
