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

        var source = string.IsNullOrWhiteSpace(component.ProviderCanonicalSignature)
            ? component.CanonicalSignature
            : component.ProviderCanonicalSignature;
        source = source
            .Replace("+<number>", "+#", StringComparison.Ordinal)
            .Replace("-<number>", "-#", StringComparison.Ordinal)
            .Replace("<number>", "#", StringComparison.Ordinal);
        var templates = new List<string>();
        if (HasSingleNegateProjection(component))
        {
            var projected = IncreasedRegex().IsMatch(source)
                ? IncreasedRegex().Replace(source, "reduced", 1)
                : ReducedRegex().IsMatch(source)
                    ? ReducedRegex().Replace(source, "increased", 1)
                    : null;
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

        if (providerArity == 0 && !component.SupportsValueBounds)
        {
            return component with
            {
                ValueBoundShape = ModifierBoundShape.PresenceOnly,
                ValueBoundsUnsupportedReason =
                    "Official Trade exposes this stat as presence-only; numeric bounds are not meaningful.",
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

    private static string Pluralize(string noun) =>
        noun.EndsWith('s') ? noun : $"{noun}s";

    [GeneratedRegex(@"\bincreased\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex IncreasedRegex();

    [GeneratedRegex(@"\breduced\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ReducedRegex();

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
