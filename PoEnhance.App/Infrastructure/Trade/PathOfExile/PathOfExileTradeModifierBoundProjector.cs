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

        if (TryGetExactFixedLiteralValue(component, providerStat, out _))
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

        if (TryGetExactFixedLiteralValue(component, providerStat, out var exactValue))
        {
            return new PathOfExileTradeProviderBoundProjection
            {
                IsFaithful = true,
                ValueBoundShape = ModifierBoundShape.Scalar,
                Minimum = exactValue,
                Maximum = exactValue,
                ProjectionKind = "ExactFixedLiteralScalar",
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
                ValueBoundShape = ModifierBoundShape.Scalar,
                Minimum = component.ProviderFallbackNumericValues[0],
                ProjectionKind = "FixedPresenceScalar",
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
        if (TryGetExactFixedLiteralValue(component, providerStat, out var exactValue))
        {
            return component with
            {
                SupportsValueBounds = true,
                ValueBoundShape = ModifierBoundShape.Scalar,
                RequestedMinimum = exactValue,
                RequestedMaximum = exactValue,
                ValueBoundsUnsupportedReason = null,
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

    private static bool HasFixedPresenceOneProjection(ResolvedSearchComponent component) =>
        component.ValueBoundShape == ModifierBoundShape.PresenceOnly &&
        component.ProviderFallbackNumericValues.Count == 1 &&
        component.ProviderFallbackNumericValues[0] == 1m;

    private static bool TryGetExactFixedLiteralValue(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate providerStat,
        out decimal value)
    {
        value = default;
        if (component.ValueBoundShape != ModifierBoundShape.Scalar ||
            component.CanonicalNumericValues.Count != 1 ||
            PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(providerStat.Text) != 0)
        {
            return false;
        }

        var hasMechanicallyRetainedFixedSignature = component.ProviderSearchSignatures.Any(signature =>
        {
            var providerTemplate = signature
                .Replace("+<number>", "+#", StringComparison.Ordinal)
                .Replace("-<number>", "-#", StringComparison.Ordinal)
                .Replace("<number>", "#", StringComparison.Ordinal);
            return PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(providerTemplate) == 0 &&
                FixedNumericLiteralRegex().IsMatch(providerTemplate) &&
                string.Equals(
                    PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(providerTemplate),
                    providerStat.LookupTemplate,
                    StringComparison.Ordinal);
        });
        if (!hasMechanicallyRetainedFixedSignature)
        {
            return false;
        }

        value = component.CanonicalNumericValues[0];
        return true;
    }

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
