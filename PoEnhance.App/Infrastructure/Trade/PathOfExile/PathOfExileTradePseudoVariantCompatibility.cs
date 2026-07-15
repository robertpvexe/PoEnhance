using System.Text.RegularExpressions;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static partial class PathOfExileTradePseudoVariantCompatibility
{
    public const string Compatible = "Compatible";
    public const string NotPseudo = "NotPseudo";
    public const string DifferentLogicalEffect = "DifferentLogicalEffect";
    public const string IncompatibleNumericArity = "IncompatibleNumericArity";
    public const string IncompatibleNumericUnit = "IncompatibleNumericUnit";
    public const string IncompatibleDirection = "IncompatibleDirection";
    public const string IncompatibleProjection = "IncompatibleProjection";
    public const string IncompatibleTranslationProjection = "IncompatibleTranslationProjection";

    public static PathOfExileTradePseudoVariantCompatibilityResult Evaluate(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate sourceExactCandidate,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        return EvaluateCore(
            component,
            sourceExactCandidate,
            candidate,
            requirePseudo: true);
    }

    internal static PathOfExileTradePseudoVariantCompatibilityResult EvaluateVariant(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate sourceExactCandidate,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        return EvaluateCore(
            component,
            sourceExactCandidate,
            candidate,
            requirePseudo: false);
    }

    private static PathOfExileTradePseudoVariantCompatibilityResult EvaluateCore(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate sourceExactCandidate,
        PathOfExileTradeStatMatchCandidate candidate,
        bool requirePseudo)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(sourceExactCandidate);
        ArgumentNullException.ThrowIfNull(candidate);

        var providerKind = PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate);
        var sourceTemplate = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
            sourceExactCandidate.Text);
        var candidateTemplate = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
            candidate.Text);
        var sourceEffect = LogicalEffectIdentity(sourceTemplate);
        var candidateEffect = LogicalEffectIdentity(candidateTemplate);
        var legacySourceEffect = LegacyLogicalEffectIdentity(sourceTemplate, removePseudoBreadth: false);
        var legacyCandidateEffect = LegacyLogicalEffectIdentity(candidateTemplate, removePseudoBreadth: true);
        var sourceNumeric = NumericSemantics(sourceTemplate);
        var candidateNumeric = NumericSemantics(candidateTemplate);
        var isPseudo = string.Equals(providerKind, "pseudo", StringComparison.Ordinal);
        var sameEffect = string.Equals(sourceEffect, candidateEffect, StringComparison.Ordinal);
        var sameArity = sourceNumeric.Count == candidateNumeric.Count;
        var sameUnits = sameArity && sourceNumeric
            .Select(token => token.Unit)
            .SequenceEqual(candidateNumeric.Select(token => token.Unit), StringComparer.Ordinal);
        var sameDirection = sameArity && sourceNumeric
            .Select(token => token.Direction)
            .SequenceEqual(candidateNumeric.Select(token => token.Direction), StringComparer.Ordinal);
        var projectionCompatible = HasCompatibleProjection(component, candidateNumeric.Count);
        var translationCompatible = HasCompatibleTranslationProjection(component, sourceNumeric.Count);
        var localityDecision = PathOfExileTradeProviderLocalityCompatibility.EvaluateVariant(
            component,
            sourceExactCandidate,
            candidate);
        var localityCompatible = localityDecision.IsCompatible;
        var conditions = new[]
        {
            !requirePseudo || isPseudo,
            sameEffect,
            sameArity,
            sameUnits,
            sameDirection,
            projectionCompatible,
            translationCompatible,
            localityCompatible,
        };
        var rejectionCode = requirePseudo && !isPseudo
            ? NotPseudo
            : !sameEffect
                ? DifferentLogicalEffect
                : !sameArity
                    ? IncompatibleNumericArity
                    : !sameUnits
                        ? IncompatibleNumericUnit
                        : !sameDirection
                            ? IncompatibleDirection
                            : !projectionCompatible
                                ? IncompatibleProjection
                                : !translationCompatible
                                    ? IncompatibleTranslationProjection
                                    : !localityCompatible
                                        ? localityDecision.ReasonCode
                                        : Compatible;

        return new PathOfExileTradePseudoVariantCompatibilityResult
        {
            IsCompatible = rejectionCode == Compatible,
            RejectionCode = rejectionCode,
            CompatibilityScore = conditions.Count(condition => condition),
            MaximumCompatibilityScore = conditions.Length,
            SourceNormalizedTemplate = sourceTemplate,
            CandidateNormalizedTemplate = candidateTemplate,
            SourceLogicalEffect = sourceEffect,
            CandidateLogicalEffect = candidateEffect,
            LegacySourceLogicalEffect = legacySourceEffect,
            LegacyCandidateLogicalEffect = legacyCandidateEffect,
            LegacyDiscoveryCompatible = string.Equals(
                legacySourceEffect,
                legacyCandidateEffect,
                StringComparison.Ordinal),
            ProviderKind = providerKind,
            SourceLocality = sourceExactCandidate.ProviderLocality,
            CandidateLocality = candidate.ProviderLocality,
            LocalityDecision = localityDecision,
            SourceNumericSemantics = sourceNumeric.Select(token => token.Display).ToArray(),
            CandidateNumericSemantics = candidateNumeric.Select(token => token.Display).ToArray(),
            HasTotalOrCombinedMarker = PseudoBreadthRegex().IsMatch(candidateTemplate),
            BoundDirection = component.DefaultBoundDirection,
            ValueShape = component.ValueBoundShape,
            TranslationHandlers = component.ValueBoundTranslationHandlers,
        };
    }

    public static bool HasCompatibleNumericSemantics(
        PathOfExileTradeStatMatchCandidate source,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        var sourceNumeric = NumericSemantics(source.Text);
        var candidateNumeric = NumericSemantics(candidate.Text);
        return sourceNumeric.Count > 0 &&
            sourceNumeric.SequenceEqual(candidateNumeric);
    }

    internal static string LogicalEffectIdentity(string? text)
    {
        var normalized = PathOfExileTradeStatTemplateNormalizer
            .NormalizeLookupTemplate(text)
            .ToLowerInvariant();
        normalized = NumericAndUnitRegex().Replace(normalized, " ");
        normalized = PseudoBreadthRegex().Replace(normalized, " ");
        normalized = AdditiveDirectionRegex().Replace(normalized, " ");
        normalized = LeadingContributionPrepositionRegex().Replace(normalized, " ");
        return CollapseWhitespace(normalized);
    }

    private static string LegacyLogicalEffectIdentity(string? text, bool removePseudoBreadth)
    {
        var normalized = PathOfExileTradeStatTemplateNormalizer
            .NormalizeLookupTemplate(text)
            .ToLowerInvariant();
        normalized = NumericAndUnitRegex().Replace(normalized, " ");
        if (removePseudoBreadth)
        {
            normalized = PseudoBreadthRegex().Replace(normalized, " ");
        }

        return CollapseWhitespace(normalized);
    }

    private static IReadOnlyList<NumericSemanticToken> NumericSemantics(string? text)
    {
        var normalized = PathOfExileTradeStatTemplateNormalizer
            .NormalizeLookupTemplate(text)
            .ToLowerInvariant();
        var wordDirection = WordDirection(normalized);
        return NumericTokenRegex().Matches(normalized)
            .Select(match =>
            {
                var explicitDirection = match.Groups["sign"].Value;
                var direction = string.IsNullOrEmpty(explicitDirection)
                    ? wordDirection
                    : explicitDirection;
                return new NumericSemanticToken(
                    match.Groups["percent"].Value == "%" ? "%" : "flat",
                    direction);
            })
            .ToArray();
    }

    private static string WordDirection(string normalizedTemplate)
    {
        var hasIncreased = IncreasedRegex().IsMatch(normalizedTemplate);
        var hasReduced = ReducedRegex().IsMatch(normalizedTemplate);
        return hasIncreased == hasReduced
            ? string.Empty
            : hasIncreased ? "+" : "-";
    }

    private static bool HasCompatibleProjection(
        ResolvedSearchComponent component,
        int candidateArity)
    {
        return component.ValueBoundShape switch
        {
            ModifierBoundShape.Scalar => candidateArity == 1,
            ModifierBoundShape.ArithmeticMeanRange => candidateArity == 2,
            ModifierBoundShape.PresenceOnly => candidateArity == 0,
            _ => false,
        };
    }

    private static bool HasCompatibleTranslationProjection(
        ResolvedSearchComponent component,
        int sourceArity)
    {
        return component.ValueBoundTranslationHandlers.Count == 0 ||
            component.ValueBoundTranslationHandlers.Count == sourceArity;
    }

    private static string CollapseWhitespace(string value) => string.Join(' ', value.Split(
        [' ', '\t', '\r', '\n'],
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private sealed record NumericSemanticToken(string Unit, string Direction)
    {
        public string Display => $"{Direction}{Unit}";
    }

    [GeneratedRegex(@"(?<sign>[+-]?)#(?<percent>%?)", RegexOptions.CultureInvariant)]
    private static partial Regex NumericTokenRegex();

    [GeneratedRegex(@"[+-]?#%?", RegexOptions.CultureInvariant)]
    private static partial Regex NumericAndUnitRegex();

    [GeneratedRegex(@"\b(?:total|combined)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PseudoBreadthRegex();

    [GeneratedRegex(@"\b(?:increased|reduced)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex AdditiveDirectionRegex();

    [GeneratedRegex(@"^\s*to\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LeadingContributionPrepositionRegex();

    [GeneratedRegex(@"\bincreased\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex IncreasedRegex();

    [GeneratedRegex(@"\breduced\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ReducedRegex();
}

internal sealed record PathOfExileTradePseudoVariantCompatibilityResult
{
    public bool IsCompatible { get; init; }

    public required string RejectionCode { get; init; }

    public int CompatibilityScore { get; init; }

    public int MaximumCompatibilityScore { get; init; }

    public required string SourceNormalizedTemplate { get; init; }

    public required string CandidateNormalizedTemplate { get; init; }

    public required string SourceLogicalEffect { get; init; }

    public required string CandidateLogicalEffect { get; init; }

    public required string LegacySourceLogicalEffect { get; init; }

    public required string LegacyCandidateLogicalEffect { get; init; }

    public bool LegacyDiscoveryCompatible { get; init; }

    public required string ProviderKind { get; init; }

    public PathOfExileTradeProviderStatLocality SourceLocality { get; init; }

    public PathOfExileTradeProviderStatLocality CandidateLocality { get; init; }

    public required PathOfExileTradeProviderLocalityDecision LocalityDecision { get; init; }

    public IReadOnlyList<string> SourceNumericSemantics { get; init; } = [];

    public IReadOnlyList<string> CandidateNumericSemantics { get; init; } = [];

    public bool HasTotalOrCombinedMarker { get; init; }

    public ModifierBoundDirection BoundDirection { get; init; }

    public ModifierBoundShape ValueShape { get; init; }

    public IReadOnlyList<IReadOnlyList<string>> TranslationHandlers { get; init; } = [];
}
