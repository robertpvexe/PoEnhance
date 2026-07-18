using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradeModifierBoundProjector
{
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
}
