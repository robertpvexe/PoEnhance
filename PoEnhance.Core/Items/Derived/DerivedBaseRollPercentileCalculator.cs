using PoEnhance.GameData;

namespace PoEnhance.Core.Items.Derived;

internal static class DerivedBaseRollPercentileCalculator
{
    public static decimal? Calculate(DerivedDefensiveProperties defensiveProperties)
    {
        ArgumentNullException.ThrowIfNull(defensiveProperties);

        int? sharedNumerator = null;
        int? sharedDenominator = null;
        foreach (var property in defensiveProperties.Properties)
        {
            if (!TryGetRange(property, out var minimum, out var maximum))
            {
                if (property.Target is ItemPropertyTarget.Armour or
                    ItemPropertyTarget.Evasion or
                    ItemPropertyTarget.EnergyShield or
                    ItemPropertyTarget.Ward)
                {
                    return null;
                }

                continue;
            }

            if (minimum == maximum)
            {
                continue;
            }

            if (property.UnsupportedReason is not null ||
                property.ReconstructedBaseValue is not { } baseRoll ||
                baseRoll < minimum ||
                baseRoll > maximum)
            {
                return null;
            }

            var numerator = baseRoll - minimum;
            var denominator = maximum - minimum;
            if (sharedNumerator.HasValue &&
                (decimal)sharedNumerator.Value * denominator != (decimal)numerator * sharedDenominator!.Value)
            {
                return null;
            }

            sharedNumerator = numerator;
            sharedDenominator = denominator;
        }

        return sharedNumerator.HasValue
            ? sharedNumerator.Value * 100m / sharedDenominator!.Value
            : null;
    }

    private static bool TryGetRange(
        DerivedDefensiveProperty property,
        out int minimum,
        out int maximum)
    {
        minimum = default;
        maximum = default;
        var baseProperties = property.BaseProperties;
        if (baseProperties is null || baseProperties.Sources.Count == 0)
        {
            return false;
        }

        (int? Minimum, int? Maximum) range = property.Target switch
        {
            ItemPropertyTarget.Armour =>
                (baseProperties.ArmourMinimum, baseProperties.ArmourMaximum),
            ItemPropertyTarget.Evasion =>
                (baseProperties.EvasionRatingMinimum, baseProperties.EvasionRatingMaximum),
            ItemPropertyTarget.EnergyShield =>
                (baseProperties.EnergyShieldMinimum, baseProperties.EnergyShieldMaximum),
            ItemPropertyTarget.Ward =>
                (baseProperties.WardMinimum, baseProperties.WardMaximum),
            _ => (null, null),
        };
        if (!range.Minimum.HasValue || !range.Maximum.HasValue || range.Minimum > range.Maximum)
        {
            return false;
        }

        minimum = range.Minimum.Value;
        maximum = range.Maximum.Value;
        return true;
    }
}
