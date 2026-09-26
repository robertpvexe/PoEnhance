using PoEnhance.Core.Trade;
using PoEnhance.GameData;
using System.Text.RegularExpressions;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static partial class PathOfExileTradeModifierBoundProjector
{
    private const string NegateHandler = "negate";

    public static IReadOnlyList<string> ProjectedLookupTemplates(
        ResolvedSearchComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        var source = GetProjectionSourceTemplate(component);
        var templates = new List<string>();
        if (HasSingleNegateProjection(component))
        {
            string? projected = null;
            if (IncreasedRegex().IsMatch(source))
            {
                projected = IncreasedRegex().Replace(source, "reduced", 1);
            }
            else if (ReducedRegex().IsMatch(source))
            {
                projected = ReducedRegex().Replace(source, "increased", 1);
            }
            else if (MoreRegex().IsMatch(source))
            {
                projected = MoreRegex().Replace(source, "less", 1);
            }
            else if (LessRegex().IsMatch(source))
            {
                projected = LessRegex().Replace(source, "more", 1);
            }

            if (projected is not null)
            {
                templates.Add(PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(projected));
            }
        }

        if (HasFixedPresenceOneProjection(component))
        {
            var projected = SingularAdditionalRegex().Replace(
                source,
                match => $"# additional {Pluralize(match.Groups["noun"].Value)}");
            if (!string.Equals(projected, source, StringComparison.Ordinal))
            {
                templates.Add(PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(projected));
            }
        }

        return templates.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static bool CanProjectSemanticBridge(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate providerStat)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(providerStat);

        if (IsProvenFixedLiteralProviderCandidate(component, providerStat))
        {
            return true;
        }

        var projectedTemplates = ProjectedLookupTemplates(component);
        return projectedTemplates.Contains(providerStat.LookupTemplate, StringComparer.Ordinal) &&
            (HasSingleNegateProjection(component) &&
                PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(providerStat.Text) == 1 ||
            HasFixedPresenceOneProjection(component) &&
                PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(providerStat.Text) == 1);
    }

    public static PathOfExileTradeProviderBoundProjection ProjectBounds(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate providerStat)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(providerStat);

        if (CanApplyFixedQueryValue(component, providerStat))
        {
            return new PathOfExileTradeProviderBoundProjection
            {
                IsFaithful = true,
                ValueBoundShape = ModifierBoundShape.Scalar,
                Minimum = component.FixedQueryValue,
                Maximum = component.FixedQueryValue,
                ProjectionKind = "FixedNumericQueryConstraint",
            };
        }

        // Omitted chance-wrapper presence must win over ExactOwnerChancePercentSiblingFallback:
        // when the only observed numeric is an identity literal inside the wrapped action
        // (e.g. Level 20), that scalar is not a chance percent.
        if (TryProjectOmittedChanceWrapperPresence(
                component,
                providerStat,
                out var omittedChanceProjection))
        {
            return omittedChanceProjection;
        }

        // TRADE.4c Track A — numeric-count display vs Trade article FilterArity-0 template.
        if (TryProjectNumericCountArticlePresence(
                component,
                providerStat,
                out var articlePresenceProjection))
        {
            return articlePresenceProjection;
        }

        // TRADE.4c Track B — plural source vs singular Trade # template; project non-embedded query scalar.
        if (TryProjectSingularPluralInflectionScalar(
                component,
                providerStat,
                out var inflectionScalarProjection))
        {
            return inflectionScalarProjection;
        }

        if (TryProjectExactOwnerChancePercentSiblingFallback(
                component,
                providerStat,
                out var chanceSiblingProjection))
        {
            return chanceSiblingProjection;
        }

        if (TryProjectTranslationFamilyCompanionPresence(
                component,
                providerStat,
                out var companionPresenceProjection))
        {
            return companionPresenceProjection;
        }

        if (IsProvenFixedLiteralProviderCandidate(component, providerStat))
        {
            return new PathOfExileTradeProviderBoundProjection
            {
                IsFaithful = true,
                ValueBoundShape = ModifierBoundShape.PresenceOnly,
                Minimum = null,
                Maximum = null,
                ProjectionKind = "ExactFixedLiteralPresence",
            };
        }

        // Provider template itself encodes the reversing polarity (e.g. "#% reduced ...").
        // Canonical/query values for negate handlers live in non-reversing (increased) space;
        // projecting onto the reversing Trade form requires the positive display magnitude.
        if (TryProjectReversingProviderMagnitude(component, providerStat, out var reversingProjection))
        {
            return reversingProjection;
        }

        if (CanProjectSemanticBridge(component, providerStat) &&
            HasSingleNegateProjection(component))
        {
            if (component.CanonicalNumericValues.Count == 1)
            {
                return new PathOfExileTradeProviderBoundProjection
                {
                    IsFaithful = true,
                    ValueBoundShape = ModifierBoundShape.Scalar,
                    Minimum = component.RequestedMinimum,
                    Maximum = component.RequestedMaximum,
                    ProjectionKind = "CanonicalNegatedScalar",
                };
            }

            return new PathOfExileTradeProviderBoundProjection
            {
                IsFaithful = true,
                ValueBoundShape = ModifierBoundShape.Scalar,
                Minimum = component.RequestedMaximum.HasValue
                    ? -component.RequestedMaximum.Value
                    : null,
                Maximum = component.RequestedMinimum.HasValue
                    ? -component.RequestedMinimum.Value
                    : null,
                ProjectionKind = "NegatedScalar",
            };
        }

        if (CanProjectSemanticBridge(component, providerStat) &&
            HasFixedPresenceOneProjection(component))
        {
            return new PathOfExileTradeProviderBoundProjection
            {
                IsFaithful = true,
                ValueBoundShape = ModifierBoundShape.PresenceOnly,
                Minimum = null,
                Maximum = null,
                ProjectionKind = "FixedPresenceIdentity",
            };
        }

        var projected = Project(component, providerStat);
        return new PathOfExileTradeProviderBoundProjection
        {
            IsFaithful = projected.SupportsValueBounds ||
                projected.ValueBoundShape == ModifierBoundShape.PresenceOnly,
            ValueBoundShape = projected.ValueBoundShape,
            Minimum = projected.SupportsValueBounds ? projected.RequestedMinimum : null,
            Maximum = projected.SupportsValueBounds ? projected.RequestedMaximum : null,
            ProjectionKind = "DisplayIdentity",
        };
    }

    public static ResolvedSearchComponent Project(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate? providerStat)
    {
        if (providerStat is null)
        {
            return component;
        }

        var providerArity = PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(
            providerStat.Text);
        if (CanApplyFixedQueryValue(component, providerStat))
        {
            return component with
            {
                SupportsValueBounds = false,
                ValueBoundShape = ModifierBoundShape.Scalar,
                RequestedMinimum = null,
                RequestedMaximum = null,
                ValueBoundsUnsupportedReason =
                    "The source proves a fixed numeric query value, but it is not user-editable.",
            };
        }

        if (TryProjectOmittedChanceWrapperPresence(component, providerStat, out _))
        {
            return component with
            {
                SupportsValueBounds = false,
                ValueBoundShape = ModifierBoundShape.PresenceOnly,
                RequestedMinimum = null,
                RequestedMaximum = null,
                ValueBoundsUnsupportedReason =
                    "Exact Unique chance StatId matched an omitted Trade chance-wrapper template without a proven chance percent value.",
            };
        }

        if (TryProjectNumericCountArticlePresence(component, providerStat, out _))
        {
            return component with
            {
                SupportsValueBounds = false,
                ValueBoundShape = ModifierBoundShape.PresenceOnly,
                RequestedMinimum = null,
                RequestedMaximum = null,
                ValueBoundsUnsupportedReason =
                    "Exact Unique numeric-count translation branch mapped to a Trade article FilterArity-0 template; source count is not a filter value.",
            };
        }

        if (TryProjectSingularPluralInflectionScalar(component, providerStat, out var inflectionProjection))
        {
            if (inflectionProjection.ValueBoundShape == ModifierBoundShape.PresenceOnly)
            {
                return component with
                {
                    SupportsValueBounds = false,
                    ValueBoundShape = ModifierBoundShape.PresenceOnly,
                    RequestedMinimum = null,
                    RequestedMaximum = null,
                    ValueBoundsUnsupportedReason =
                        "Exact Unique singular/plural inflection matched Trade FilterArity-1 with embedded literals, but the Trade # query slot could not be uniquely recovered from source-minus-embedded evidence.",
                };
            }

            return component with
            {
                SupportsValueBounds = true,
                ValueBoundShape = ModifierBoundShape.Scalar,
                RequestedMinimum = inflectionProjection.Minimum,
                RequestedMaximum = inflectionProjection.Maximum,
                ValueBoundsUnsupportedReason = null,
            };
        }

        if (TryProjectExactOwnerChancePercentSiblingFallback(component, providerStat, out _))
        {
            return component with
            {
                SupportsValueBounds = false,
                ValueBoundShape = ModifierBoundShape.Scalar,
                RequestedMinimum = null,
                RequestedMaximum = null,
                ValueBoundsUnsupportedReason =
                    "Exact Unique chance mechanics project a fixed owner scalar onto the parametric Trade sibling.",
            };
        }

        if (TryProjectTranslationFamilyCompanionPresence(component, providerStat, out _))
        {
            return component with
            {
                SupportsValueBounds = false,
                ValueBoundShape = ModifierBoundShape.PresenceOnly,
                RequestedMinimum = null,
                RequestedMaximum = null,
                ValueBoundsUnsupportedReason =
                    "Special phrase display projects onto a translation-family numeric Trade companion as presence-only.",
            };
        }

        if (IsProvenFixedLiteralProviderCandidate(component, providerStat))
        {
            return component with
            {
                SupportsValueBounds = false,
                ValueBoundShape = ModifierBoundShape.PresenceOnly,
                RequestedMinimum = null,
                RequestedMaximum = null,
                ValueBoundsUnsupportedReason =
                    "This Trade filter represents a fixed literal provider variant and has no numeric Min/Max.",
            };
        }

        if (component.ValueBoundShape == ModifierBoundShape.Unsupported &&
            component.ObservedNumericValues.Count == 2 &&
            providerArity == 2 &&
            component.ReviewedItemPropertySemantic?.Contributions.Any(contribution =>
                contribution.Operation == ItemPropertyOperation.Added) == true)
        {
            var canonicalValues = component.ObservedNumericValues.ToArray();
            return component with
            {
                SupportsValueBounds = true,
                ValueBoundShape = ModifierBoundShape.ArithmeticMeanRange,
                CanonicalNumericValues = canonicalValues,
                DefaultBoundDirection = ModifierBoundDirection.Minimum,
                RequestedMinimum = component.RequestedMinimum ??
                    (canonicalValues[0] + canonicalValues[1]) / 2m,
                RequestedMaximum = component.RequestedMaximum,
                ValueBoundsUnsupportedReason = null,
            };
        }

        if (component.ValueBoundShape == ModifierBoundShape.ArithmeticMeanRange)
        {
            if (component.ObservedNumericValues.Count != 2 || providerArity != 2)
            {
                return component with
                {
                    SupportsValueBounds = false,
                    RequestedMinimum = null,
                    RequestedMaximum = null,
                    ValueBoundsUnsupportedReason =
                        "The resolved Trade stat does not expose the same two-value range as the GameData translation.",
                };
            }

            return component with
            {
                SupportsValueBounds = true,
                RequestedMinimum = component.RequestedMinimum ??
                    (component.ObservedNumericValues[0] + component.ObservedNumericValues[1]) / 2m,
                RequestedMaximum = component.RequestedMaximum,
                ValueBoundsUnsupportedReason = null,
            };
        }

        // Official Trade filter arity is the count of '#' placeholders in the selected
        // catalog entry text. Literal digits in that text are identity, not query slots.
        // Arity 0 must suppress min/max even when Core/draft still carries parsed numbers.
        if (providerArity == 0)
        {
            return component with
            {
                SupportsValueBounds = false,
                ValueBoundShape = ModifierBoundShape.PresenceOnly,
                RequestedMinimum = null,
                RequestedMaximum = null,
                ValueBoundsUnsupportedReason =
                    "Official Trade FilterArity is 0 (no dynamic '#' placeholders); numeric Min/Max are suppressed.",
            };
        }

        return component;
    }

    private static bool HasSingleNegateProjection(ResolvedSearchComponent component) =>
        component.ValueBoundShape == ModifierBoundShape.Scalar &&
        component.ValueBoundTranslationHandlers.Count == 1 &&
        component.ValueBoundTranslationHandlers[0].Count == 1 &&
        string.Equals(
            component.ValueBoundTranslationHandlers[0][0],
            NegateHandler,
            StringComparison.OrdinalIgnoreCase);

    private static bool TryProjectReversingProviderMagnitude(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate providerStat,
        out PathOfExileTradeProviderBoundProjection projection)
    {
        projection = null!;
        if (!TryGetNegateProviderPolarity(component, providerStat, out var providerIsReversingForm) ||
            !providerIsReversingForm ||
            !TryGetObservedMagnitude(component, out var magnitude))
        {
            return false;
        }

        // Single observed scalar uses Min-only provider convention.
        projection = new PathOfExileTradeProviderBoundProjection
        {
            IsFaithful = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            Minimum = magnitude,
            Maximum = null,
            ProjectionKind = "ReversingProviderMagnitudeScalar",
        };
        return true;
    }

    /// <summary>
    /// Proves whether the selected Trade provider is the reversing (typically reduced/less) or
    /// non-reversing (typically increased/more) side of a negate-handler polarity pair.
    /// Fail-closed when polarity cannot be proven from the component signature and provider template.
    /// </summary>
    private static bool TryGetNegateProviderPolarity(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate providerStat,
        out bool providerIsReversingForm)
    {
        providerIsReversingForm = false;
        if (!HasSingleNegateProjection(component))
        {
            return false;
        }

        var source = GetProjectionSourceTemplate(component);
        if (!TryClassifyNegatePolarity(source, out var sourceIsReversing))
        {
            return false;
        }

        var normalizedSource = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(source);
        if (string.Equals(providerStat.LookupTemplate, normalizedSource, StringComparison.Ordinal))
        {
            providerIsReversingForm = sourceIsReversing;
            return true;
        }

        if (!CanProjectSemanticBridge(component, providerStat))
        {
            return false;
        }

        // Bridge always targets the opposite polarity of the component signature source.
        providerIsReversingForm = !sourceIsReversing;
        return true;
    }

    /// <summary>
    /// Classifies a negate-handler signature as reversing (<c>reduced</c>/<c>less</c>) or
    /// non-reversing (<c>increased</c>/<c>more</c>). Fail-closed on mixed or absent polarity words.
    /// </summary>
    private static bool TryClassifyNegatePolarity(string source, out bool isReversingForm)
    {
        isReversingForm = false;
        var isNonReversing = IncreasedRegex().IsMatch(source) || MoreRegex().IsMatch(source);
        var isReversing = ReducedRegex().IsMatch(source) || LessRegex().IsMatch(source);
        if (isNonReversing == isReversing)
        {
            return false;
        }

        isReversingForm = isReversing;
        return true;
    }

    private static bool TryGetObservedMagnitude(
        ResolvedSearchComponent component,
        out decimal magnitude)
    {
        if (component.ObservedNumericValues.Count == 1)
        {
            magnitude = decimal.Abs(component.ObservedNumericValues[0]);
            return true;
        }

        if (component.CanonicalNumericValues.Count == 1)
        {
            magnitude = decimal.Abs(component.CanonicalNumericValues[0]);
            return true;
        }

        magnitude = default;
        return false;
    }

    private static string GetProjectionSourceTemplate(ResolvedSearchComponent component)
    {
        var source = string.IsNullOrWhiteSpace(component.ProviderCanonicalSignature)
            ? component.CanonicalSignature
            : component.ProviderCanonicalSignature;
        return source
            .Replace("+<number>", "+#", StringComparison.Ordinal)
            .Replace("-<number>", "-#", StringComparison.Ordinal)
            .Replace("<number>", "#", StringComparison.Ordinal);
    }

    internal static bool CanApplyFixedQueryValue(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate providerStat) =>
        component.FixedQueryValue.HasValue &&
        component.CanonicalNumericValues.Count == 1 &&
        component.CanonicalNumericValues[0] == component.FixedQueryValue.Value &&
        PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(providerStat.Text) == 1;

    internal static bool IsProvenFixedLiteralProviderCandidate(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate providerStat)
    {
        // Official Trade may expose a fixed-literal entry whose lookupTemplate is intentionally
        // generalized to #. Literal proof must use the provider entry text, not LookupTemplate.
        if (PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(providerStat.Text) != 0 ||
            !FixedNumericLiteralRegex().IsMatch(providerStat.Text))
        {
            return false;
        }

        var normalizedProviderText = PathOfExileTradeStatTemplateNormalizer.NormalizeComparableProviderText(
            providerStat.Text);
        return component.ProviderSearchSignatures.Any(signature =>
        {
            var retainedTemplate = ToProviderTemplateMarkers(signature);
            if (PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(retainedTemplate) != 0 ||
                !FixedNumericLiteralRegex().IsMatch(retainedTemplate))
            {
                return false;
            }

            return string.Equals(
                PathOfExileTradeStatTemplateNormalizer.NormalizeComparableProviderText(retainedTemplate),
                normalizedProviderText,
                StringComparison.Ordinal);
        });
    }

    private static string ToProviderTemplateMarkers(string signature) =>
        signature
            .Replace("+<number>", "+#", StringComparison.Ordinal)
            .Replace("-<number>", "-#", StringComparison.Ordinal)
            .Replace("<number>", "#", StringComparison.Ordinal);

    private static bool HasFixedPresenceOneProjection(ResolvedSearchComponent component) =>
        component.ValueBoundShape == ModifierBoundShape.PresenceOnly &&
        component.ProviderFallbackNumericValues.Count == 1 &&
        component.ProviderFallbackNumericValues[0] == 1m;

    public static bool CanApplyExactOwnerChancePercentSiblingFallback(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate providerStat) =>
        TryProjectExactOwnerChancePercentSiblingFallback(component, providerStat, out _);

    /// <summary>
    /// TRADE.2.2 — special phrase display (0 placeholders) that discovered a translation-family
    /// numeric companion Trade template must stay presence-only; the companion's fixed/extreme
    /// translation value is not a user roll threshold.
    /// </summary>
    private static bool TryProjectTranslationFamilyCompanionPresence(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate providerStat,
        out PathOfExileTradeProviderBoundProjection projection)
    {
        projection = null!;
        if (!component.HasExactUniqueSourceProvenance ||
            component.FixedQueryValue.HasValue ||
            PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(providerStat.Text) == 0 ||
            providerStat.OptionMetadata.Count != 0)
        {
            return false;
        }

        var sourceDisplay = GetProjectionSourceTemplate(component);
        if (PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(sourceDisplay) != 0)
        {
            return false;
        }

        var providerLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
            providerStat.Text);
        var companionLookups = component.ProviderSearchSignatures
            .Where(signature => !string.IsNullOrWhiteSpace(signature))
            .Select(signature => PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                signature
                    .Replace("+<number>", "+#", StringComparison.Ordinal)
                    .Replace("-<number>", "-#", StringComparison.Ordinal)
                    .Replace("<number>", "#", StringComparison.Ordinal)))
            .Where(lookup =>
                !string.IsNullOrWhiteSpace(lookup) &&
                PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(lookup) > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (companionLookups.Length == 0 ||
            !companionLookups.Contains(providerLookup, StringComparer.Ordinal))
        {
            return false;
        }

        // Display itself must not already be the parametric companion form.
        var displayLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
            sourceDisplay);
        if (string.Equals(displayLookup, providerLookup, StringComparison.Ordinal))
        {
            return false;
        }

        projection = new PathOfExileTradeProviderBoundProjection
        {
            IsFaithful = true,
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            Minimum = null,
            Maximum = null,
            ProjectionKind = "TranslationFamilyCompanionPresence",
        };
        return true;
    }

    private static bool TryProjectExactOwnerChancePercentSiblingFallback(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate providerStat,
        out PathOfExileTradeProviderBoundProjection projection)
    {
        projection = null!;
        if (!component.HasExactUniqueSourceProvenance ||
            component.ProviderFallbackNumericValues.Count != 1 ||
            component.FixedQueryValue.HasValue ||
            PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(providerStat.Text) != 1 ||
            providerStat.OptionMetadata.Count != 0 ||
            !IndicatesChancePercentInternalMechanic(component.ResolvedStatIds) ||
            !TryGetChancePercentPresenceRest(providerStat.Text, out var presenceRest) ||
            !MatchesPresenceDisplaySemantics(component, presenceRest) ||
            FallbackNumericsAreEmbeddedInPresenceRest(
                presenceRest,
                component.ProviderFallbackNumericValues))
        {
            return false;
        }

        var scalar = component.ProviderFallbackNumericValues[0];
        projection = new PathOfExileTradeProviderBoundProjection
        {
            IsFaithful = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            Minimum = scalar,
            Maximum = scalar,
            ProjectionKind = "ExactOwnerChancePercentSiblingFallback",
        };
        return true;
    }

    /// <summary>
    /// TRADE.4c Track A — Trade article FilterArity-0 template discovered from a numeric-count
    /// source branch projects presence-only (source count is a translation selector, not Min/Max).
    /// </summary>
    private static bool TryProjectNumericCountArticlePresence(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate providerStat,
        out PathOfExileTradeProviderBoundProjection projection)
    {
        projection = null!;
        if (!component.HasExactUniqueSourceProvenance ||
            component.FixedQueryValue.HasValue ||
            PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(providerStat.Text) != 0 ||
            providerStat.OptionMetadata.Count != 0)
        {
            return false;
        }

        var providerLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
            providerStat.Text);
        if (string.IsNullOrWhiteSpace(providerLookup) ||
            !(providerLookup.StartsWith("An ", StringComparison.Ordinal) ||
                providerLookup.StartsWith("A ", StringComparison.Ordinal)))
        {
            return false;
        }

        var articleRest = providerLookup.StartsWith("An ", StringComparison.Ordinal)
            ? providerLookup[3..]
            : providerLookup[2..];
        var expectedNumeric = "# " + articleRest;
        IEnumerable<string?> texts =
        [
            component.OriginalText,
            component.CanonicalSignature,
            component.ProviderCanonicalSignature,
            .. component.ProviderSearchSignatures,
        ];
        var matchedNumericSibling = false;
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var lookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                text
                    .Replace("+<number>", "+#", StringComparison.Ordinal)
                    .Replace("-<number>", "-#", StringComparison.Ordinal)
                    .Replace("<number>", "#", StringComparison.Ordinal));
            if (string.Equals(lookup, expectedNumeric, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(lookup, providerLookup, StringComparison.OrdinalIgnoreCase))
            {
                matchedNumericSibling = string.Equals(
                    lookup,
                    expectedNumeric,
                    StringComparison.OrdinalIgnoreCase);
                if (matchedNumericSibling ||
                    string.Equals(lookup, providerLookup, StringComparison.OrdinalIgnoreCase))
                {
                    // Require the source to be the numeric-count sibling, not already the article form.
                    if (matchedNumericSibling)
                    {
                        projection = new PathOfExileTradeProviderBoundProjection
                        {
                            IsFaithful = true,
                            ValueBoundShape = ModifierBoundShape.PresenceOnly,
                            Minimum = null,
                            Maximum = null,
                            ProjectionKind = "NumericCountArticlePresence",
                        };
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// TRADE.4c / TRADE.4c.1 Track B — singular Trade <c>#</c> template with embedded literals;
    /// project the non-embedded source scalar that fills the Trade query slot.
    /// </summary>
    /// <remarks>
    /// Real Ctrl+D drafts may initialize <see cref="ResolvedSearchComponent.RequestedMinimum"/> from a
    /// multiline per-line single-placeholder catalog signature that is an embedded Trade literal
    /// (e.g. source <c>8</c> on a line whose Trade form keeps <c>8</c> as identity). Once the
    /// singular/plural inflection + FilterArity-1 + embedded-literal shape is proven, never fall
    /// through to DisplayIdentity with that corrupted Core scalar — recover the query slot as
    /// source numbers minus Trade embedded literals, or fail closed to presence-only.
    /// </remarks>
    private static bool TryProjectSingularPluralInflectionScalar(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate providerStat,
        out PathOfExileTradeProviderBoundProjection projection)
    {
        projection = null!;
        if (!component.HasExactUniqueSourceProvenance ||
            component.FixedQueryValue.HasValue ||
            PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(providerStat.Text) != 1 ||
            providerStat.OptionMetadata.Count != 0)
        {
            return false;
        }

        var providerLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
            providerStat.Text);
        var sourceLookup = GetPrimarySourceLookup(component);
        if (sourceLookup is null ||
            string.Equals(sourceLookup, providerLookup, StringComparison.Ordinal))
        {
            return false;
        }

        if (!HasSingleRegularSingularPluralTokenDiff(sourceLookup, providerLookup))
        {
            return false;
        }

        var embedded = PathOfExileTradeStatTemplateNormalizer
            .NormalizeModifierText(providerStat.Text)
            .ExtractedNumericValues;
        if (embedded.Count == 0)
        {
            // Inflection-only siblings without Trade embedded literals keep normal scalar paths.
            return false;
        }

        if (TryResolveSingularPluralInflectionQueryScalar(component, embedded, out var scalar))
        {
            projection = new PathOfExileTradeProviderBoundProjection
            {
                IsFaithful = true,
                ValueBoundShape = ModifierBoundShape.Scalar,
                Minimum = scalar,
                Maximum = scalar,
                ProjectionKind = "SingularPluralInflectionScalar",
            };
            return true;
        }

        // Structure proven but the Trade # slot value is not uniquely recoverable from
        // source-minus-embedded evidence. Do not trust Core ExactInitializedEditableQueryValue.
        projection = new PathOfExileTradeProviderBoundProjection
        {
            IsFaithful = true,
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            Minimum = null,
            Maximum = null,
            ProjectionKind = "SingularPluralInflectionUnresolvedPresence",
        };
        return true;
    }

    /// <summary>
    /// Resolves the Trade <c>#</c> query scalar as the unique source number that is not an
    /// embedded literal in the selected Trade template.
    /// </summary>
    private static bool TryResolveSingularPluralInflectionQueryScalar(
        ResolvedSearchComponent component,
        IReadOnlyList<decimal> embedded,
        out decimal scalar)
    {
        scalar = 0m;
        foreach (var observed in EnumerateInflectionSourceNumericCandidates(component))
        {
            var queryValues = observed
                .Where(value => !embedded.Contains(value))
                .Distinct()
                .ToArray();
            if (queryValues.Length == 1)
            {
                scalar = queryValues[0];
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<IReadOnlyList<decimal>> EnumerateInflectionSourceNumericCandidates(
        ResolvedSearchComponent component)
    {
        if (component.ObservedNumericValues.Count > 0)
        {
            yield return component.ObservedNumericValues;
        }

        if (component.ProviderFallbackNumericValues.Count > 0)
        {
            yield return component.ProviderFallbackNumericValues;
        }

        // TRADE.4c.1 — real multiline Unique drafts may carry only the embedded-literal line's
        // ExactInitializedEditableQueryValue (e.g. [8]). Recover full source numerics from text.
        foreach (var text in new[] { component.OriginalText, component.RawCopiedText })
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var extracted = PathOfExileTradeStatTemplateNormalizer
                .NormalizeModifierText(text)
                .ExtractedNumericValues;
            if (extracted.Count > 0)
            {
                yield return extracted;
            }
        }
    }

    private static string? GetPrimarySourceLookup(ResolvedSearchComponent component)
    {
        IEnumerable<string?> texts =
        [
            component.ProviderCanonicalSignature,
            component.CanonicalSignature,
            .. component.ProviderSearchSignatures,
            component.OriginalText,
        ];
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var lookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                text
                    .Replace("+<number>", "+#", StringComparison.Ordinal)
                    .Replace("-<number>", "-#", StringComparison.Ordinal)
                    .Replace("<number>", "#", StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(lookup))
            {
                return lookup;
            }
        }

        return null;
    }

    private static bool HasSingleRegularSingularPluralTokenDiff(string left, string right)
    {
        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (leftTokens.Length != rightTokens.Length)
        {
            return false;
        }

        var diffs = 0;
        string? a = null;
        string? b = null;
        for (var i = 0; i < leftTokens.Length; i++)
        {
            if (string.Equals(leftTokens[i], rightTokens[i], StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            diffs++;
            a = leftTokens[i];
            b = rightTokens[i];
        }

        if (diffs != 1 || a is null || b is null)
        {
            return false;
        }

        return string.Equals(a + "s", b, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(b + "s", a, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// TRADE.4a — when Exact Unique chance StatIds discover Trade's "#% chance to …" template
    /// but the copied display omitted that wrapper (so no chance percent was observed), project
    /// presence-only. Prevents identity literals (e.g. Level 20) from filling the chance slot.
    /// Activates only when any fallback numerics are embedded identity literals inside the
    /// wrapped action text — not when they are an authoritative owner chance percent.
    /// </summary>
    private static bool TryProjectOmittedChanceWrapperPresence(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate providerStat,
        out PathOfExileTradeProviderBoundProjection projection)
    {
        projection = null!;
        if (!component.HasExactUniqueSourceProvenance ||
            component.FixedQueryValue.HasValue ||
            PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(providerStat.Text) != 1 ||
            providerStat.OptionMetadata.Count != 0 ||
            !IndicatesChanceWrapperInternalMechanic(component.ResolvedStatIds) ||
            !TryGetChancePercentPresenceRest(providerStat.Text, out var presenceRest) ||
            !MatchesPresenceDisplaySemantics(component, presenceRest) ||
            SourceAlreadyIncludesChanceWrapper(component) ||
            !FallbackNumericsAreEmbeddedInPresenceRest(
                presenceRest,
                EffectiveFallbackNumerics(component)))
        {
            return false;
        }

        projection = new PathOfExileTradeProviderBoundProjection
        {
            IsFaithful = true,
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            Minimum = null,
            Maximum = null,
            ProjectionKind = "OmittedChanceWrapperPresence",
        };
        return true;
    }

    private static IReadOnlyList<decimal> EffectiveFallbackNumerics(ResolvedSearchComponent component) =>
        component.ProviderFallbackNumericValues.Count > 0
            ? component.ProviderFallbackNumericValues
            : component.ObservedNumericValues;

    private static bool FallbackNumericsAreEmbeddedInPresenceRest(
        string presenceRest,
        IReadOnlyList<decimal> fallbackValues)
    {
        // No separate chance scalar — presence-only is safe.
        if (fallbackValues.Count == 0)
        {
            return true;
        }

        var embedded = PathOfExileTradeStatTemplateNormalizer
            .NormalizeModifierText(presenceRest)
            .ExtractedNumericValues;
        if (embedded.Count == 0)
        {
            // Presence rest has no literals; fallback is an owner chance % (sibling path).
            return false;
        }

        return embedded.Count == fallbackValues.Count &&
            embedded.Zip(fallbackValues, (left, right) => left == right).All(equal => equal);
    }

    private static bool IndicatesChanceWrapperInternalMechanic(IReadOnlyList<string> resolvedStatIds)
    {
        foreach (var statId in resolvedStatIds)
        {
            if (string.IsNullOrWhiteSpace(statId))
            {
                continue;
            }

            var trimmed = statId.Trim();
            if (trimmed.Contains("%_chance", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("_chance_", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith("_chance", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SourceAlreadyIncludesChanceWrapper(ResolvedSearchComponent component)
    {
        IEnumerable<string?> texts =
        [
            component.OriginalText,
            component.CanonicalSignature,
            component.ProviderCanonicalSignature,
            .. component.ProviderSearchSignatures,
        ];
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var normalized = PathOfExileTradeStatTemplateNormalizer.NormalizeComparableProviderText(text);
            if (normalized.StartsWith("#% chance to ", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("% chance to ", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("#% chance ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IndicatesChancePercentInternalMechanic(IReadOnlyList<string> resolvedStatIds)
    {
        foreach (var statId in resolvedStatIds)
        {
            if (string.IsNullOrWhiteSpace(statId))
            {
                continue;
            }

            var trimmed = statId.Trim();
            if (trimmed.Contains("chance", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("_%", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetChancePercentPresenceRest(string providerText, out string presenceRest)
    {
        const string prefix = "#% chance to ";
        presenceRest = string.Empty;
        if (string.IsNullOrWhiteSpace(providerText) ||
            !providerText.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        presenceRest = providerText[prefix.Length..];
        return presenceRest.Length > 0 &&
            PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(presenceRest) == 0;
    }

    private static bool MatchesPresenceDisplaySemantics(
        ResolvedSearchComponent component,
        string presenceText)
    {
        var comparablePresence = PathOfExileTradeStatTemplateNormalizer.NormalizeComparableProviderText(
            presenceText);
        if (string.IsNullOrWhiteSpace(comparablePresence))
        {
            return false;
        }

        IEnumerable<string?> texts =
        [
            component.OriginalText,
            component.CanonicalSignature,
            component.ProviderCanonicalSignature,
            .. component.ProviderSearchSignatures,
        ];
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (string.Equals(
                    PathOfExileTradeStatTemplateNormalizer.NormalizeComparableProviderText(text),
                    comparablePresence,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string Pluralize(string noun) =>
        noun.EndsWith('s') ? noun : $"{noun}s";

    [GeneratedRegex(@"\bincreased\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex IncreasedRegex();

    [GeneratedRegex(@"\breduced\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ReducedRegex();

    [GeneratedRegex(@"\bmore\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex MoreRegex();

    [GeneratedRegex(@"\bless\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LessRegex();

    [GeneratedRegex(
        @"\ban additional (?<noun>[A-Za-z]+)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SingularAdditionalRegex();

    [GeneratedRegex(@"(?<![\w#])[+-]?\d+(?:\.\d+)?(?![\w#])", RegexOptions.CultureInvariant)]
    private static partial Regex FixedNumericLiteralRegex();
}

internal sealed record PathOfExileTradeProviderBoundProjection
{
    public bool IsFaithful { get; init; }

    public ModifierBoundShape ValueBoundShape { get; init; }

    public decimal? Minimum { get; init; }

    public decimal? Maximum { get; init; }

    public required string ProjectionKind { get; init; }
}
