using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Items.Derived;

public sealed class DerivedWeaponPropertyCalculator
{
    private const string PhysicalDamageName = "physical damage";
    private const string ElementalDamageName = "elemental damage";
    private const string ChaosDamageName = "chaos damage";
    private const string AttacksPerSecondName = "attacks per second";
    private const string CriticalStrikeChanceName = "critical strike chance";

    public DerivedWeaponProperties Calculate(ParsedItem parsedItem)
    {
        ArgumentNullException.ThrowIfNull(parsedItem);

        var physicalProperties = FindProperties(parsedItem, PhysicalDamageName);
        var elementalProperties = FindProperties(parsedItem, ElementalDamageName);
        var chaosProperties = FindProperties(parsedItem, ChaosDamageName);
        var attacksPerSecondProperties = FindProperties(parsedItem, AttacksPerSecondName);
        var criticalStrikeChanceProperties = FindProperties(parsedItem, CriticalStrikeChanceName);
        var hasDamageProperty = physicalProperties.Count > 0 ||
            elementalProperties.Count > 0 ||
            chaosProperties.Count > 0;

        if (!hasDamageProperty && attacksPerSecondProperties.Count == 0)
        {
            return new DerivedWeaponProperties
            {
                Status = DerivedWeaponPropertyStatus.NotApplicable,
            };
        }

        var diagnostics = new List<DerivedWeaponPropertyDiagnostic>();
        var blockingStatus = DerivedWeaponPropertyStatus.Success;
        var attacksPerSecond = ReadAttacksPerSecond(
            attacksPerSecondProperties,
            diagnostics,
            ref blockingStatus);
        var physicalDamage = ReadDamage(
            physicalProperties,
            PhysicalDamageName,
            allowMultipleRanges: false,
            diagnostics,
            ref blockingStatus);
        var elementalDamage = ReadDamage(
            elementalProperties,
            ElementalDamageName,
            allowMultipleRanges: true,
            diagnostics,
            ref blockingStatus);
        var chaosDamage = ReadDamage(
            chaosProperties,
            ChaosDamageName,
            allowMultipleRanges: true,
            diagnostics,
            ref blockingStatus);

        if (!hasDamageProperty)
        {
            diagnostics.Add(new DerivedWeaponPropertyDiagnostic(
                DerivedWeaponPropertyDiagnosticCodes.MissingDamage,
                "At least one displayed Physical, Elemental, or Chaos Damage property is required."));
            PromoteStatus(ref blockingStatus, DerivedWeaponPropertyStatus.Invalid);
        }

        var criticalStrikeChance = ReadCriticalStrikeChance(
            criticalStrikeChanceProperties,
            diagnostics);
        if (blockingStatus != DerivedWeaponPropertyStatus.Success || !attacksPerSecond.Value.HasValue)
        {
            return new DerivedWeaponProperties
            {
                Status = blockingStatus,
                AttacksPerSecond = attacksPerSecond.Value,
                CriticalStrikeChance = criticalStrikeChance.Value,
                AttacksPerSecondSourceProperty = attacksPerSecond.SourceProperty,
                CriticalStrikeChanceSourceProperty = criticalStrikeChance.SourceProperty,
                Diagnostics = diagnostics.ToArray(),
            };
        }

        var physical = CalculateDamage(physicalDamage, attacksPerSecond.Value.Value);
        var elemental = CalculateDamage(elementalDamage, attacksPerSecond.Value.Value);
        var chaos = CalculateDamage(chaosDamage, attacksPerSecond.Value.Value);
        var totalDps = (physical?.DamagePerSecond ?? 0m) +
            (elemental?.DamagePerSecond ?? 0m) +
            (chaos?.DamagePerSecond ?? 0m);

        return new DerivedWeaponProperties
        {
            Status = DerivedWeaponPropertyStatus.Success,
            PhysicalDamage = physical,
            ElementalDamage = elemental,
            ChaosDamage = chaos,
            TotalDps = totalDps,
            AttacksPerSecond = attacksPerSecond.Value,
            CriticalStrikeChance = criticalStrikeChance.Value,
            AttacksPerSecondSourceProperty = attacksPerSecond.SourceProperty,
            CriticalStrikeChanceSourceProperty = criticalStrikeChance.SourceProperty,
            Diagnostics = diagnostics.ToArray(),
        };
    }

    private static IReadOnlyList<ParsedItemProperty> FindProperties(
        ParsedItem parsedItem,
        string normalizedName)
    {
        return parsedItem.Properties
            .Where(property => string.Equals(
                property.NormalizedName,
                normalizedName,
                StringComparison.Ordinal))
            .ToArray();
    }

    private static PropertyScalar ReadAttacksPerSecond(
        IReadOnlyList<ParsedItemProperty> properties,
        ICollection<DerivedWeaponPropertyDiagnostic> diagnostics,
        ref DerivedWeaponPropertyStatus blockingStatus)
    {
        if (properties.Count == 0)
        {
            diagnostics.Add(new DerivedWeaponPropertyDiagnostic(
                DerivedWeaponPropertyDiagnosticCodes.MissingAttacksPerSecond,
                "A displayed Attacks per Second property is required for weapon DPS."));
            PromoteStatus(ref blockingStatus, DerivedWeaponPropertyStatus.Invalid);
            return default;
        }

        if (properties.Count != 1)
        {
            diagnostics.Add(new DerivedWeaponPropertyDiagnostic(
                DerivedWeaponPropertyDiagnosticCodes.AmbiguousProperty,
                "More than one Attacks per Second property was parsed.",
                properties[0]));
            PromoteStatus(ref blockingStatus, DerivedWeaponPropertyStatus.Unsupported);
            return new PropertyScalar(null, properties[0]);
        }

        var property = properties[0];
        if (property.NumericGroups.Count != 1 ||
            !property.NumericGroups[0].IsScalar ||
            property.NumericGroups[0].IsPercentage)
        {
            diagnostics.Add(new DerivedWeaponPropertyDiagnostic(
                DerivedWeaponPropertyDiagnosticCodes.UnsupportedAttacksPerSecond,
                "Attacks per Second must contain one non-percentage scalar numeric group.",
                property));
            PromoteStatus(ref blockingStatus, DerivedWeaponPropertyStatus.Unsupported);
            return new PropertyScalar(null, property);
        }

        var value = property.NumericGroups[0].ScalarValue!.Value;
        if (value <= 0m)
        {
            diagnostics.Add(new DerivedWeaponPropertyDiagnostic(
                DerivedWeaponPropertyDiagnosticCodes.InvalidAttacksPerSecond,
                "Attacks per Second must be greater than zero.",
                property));
            PromoteStatus(ref blockingStatus, DerivedWeaponPropertyStatus.Invalid);
            return new PropertyScalar(null, property);
        }

        return new PropertyScalar(value, property);
    }

    private static ParsedItemProperty? ReadDamage(
        IReadOnlyList<ParsedItemProperty> properties,
        string normalizedName,
        bool allowMultipleRanges,
        ICollection<DerivedWeaponPropertyDiagnostic> diagnostics,
        ref DerivedWeaponPropertyStatus blockingStatus)
    {
        if (properties.Count == 0)
        {
            return null;
        }

        if (properties.Count != 1)
        {
            diagnostics.Add(new DerivedWeaponPropertyDiagnostic(
                DerivedWeaponPropertyDiagnosticCodes.AmbiguousProperty,
                $"More than one {normalizedName} property was parsed.",
                properties[0]));
            PromoteStatus(ref blockingStatus, DerivedWeaponPropertyStatus.Unsupported);
            return null;
        }

        var property = properties[0];
        if (property.NumericGroups.Count == 0 ||
            !allowMultipleRanges && property.NumericGroups.Count != 1 ||
            property.NumericGroups.Any(group => !group.IsRange || group.IsPercentage))
        {
            diagnostics.Add(new DerivedWeaponPropertyDiagnostic(
                DerivedWeaponPropertyDiagnosticCodes.UnsupportedDamage,
                $"{property.Name} must contain " +
                    (allowMultipleRanges ? "one or more" : "one") +
                    " non-percentage minimum/maximum range groups.",
                property));
            PromoteStatus(ref blockingStatus, DerivedWeaponPropertyStatus.Unsupported);
            return null;
        }

        if (property.NumericGroups.Any(group =>
                group.MinimumValue!.Value < 0m ||
                group.MaximumValue!.Value < group.MinimumValue.Value))
        {
            diagnostics.Add(new DerivedWeaponPropertyDiagnostic(
                DerivedWeaponPropertyDiagnosticCodes.InvalidDamageRange,
                $"{property.Name} ranges must be non-negative and ordered minimum to maximum.",
                property));
            PromoteStatus(ref blockingStatus, DerivedWeaponPropertyStatus.Invalid);
            return null;
        }

        return property;
    }

    private static PropertyScalar ReadCriticalStrikeChance(
        IReadOnlyList<ParsedItemProperty> properties,
        ICollection<DerivedWeaponPropertyDiagnostic> diagnostics)
    {
        if (properties.Count == 0)
        {
            return default;
        }

        if (properties.Count != 1)
        {
            diagnostics.Add(new DerivedWeaponPropertyDiagnostic(
                DerivedWeaponPropertyDiagnosticCodes.UnsupportedCriticalStrikeChance,
                "More than one Critical Strike Chance property was parsed.",
                properties[0]));
            return new PropertyScalar(null, properties[0]);
        }

        var property = properties[0];
        if (property.NumericGroups.Count != 1 ||
            !property.NumericGroups[0].IsScalar ||
            !property.NumericGroups[0].IsPercentage ||
            property.NumericGroups[0].ScalarValue!.Value < 0m)
        {
            diagnostics.Add(new DerivedWeaponPropertyDiagnostic(
                DerivedWeaponPropertyDiagnosticCodes.UnsupportedCriticalStrikeChance,
                "Critical Strike Chance must contain one non-negative percentage scalar numeric group.",
                property));
            return new PropertyScalar(null, property);
        }

        return new PropertyScalar(property.NumericGroups[0].ScalarValue, property);
    }

    private static DerivedWeaponDamage? CalculateDamage(
        ParsedItemProperty? property,
        decimal attacksPerSecond)
    {
        if (property is null)
        {
            return null;
        }

        var ranges = property.NumericGroups
            .Select(group => new DerivedWeaponDamageRange(
                group,
                (group.MinimumValue!.Value + group.MaximumValue!.Value) / 2m))
            .ToArray();
        var averageHit = ranges.Sum(range => range.AverageHit);
        return new DerivedWeaponDamage(
            property,
            ranges,
            averageHit,
            averageHit * attacksPerSecond);
    }

    private static void PromoteStatus(
        ref DerivedWeaponPropertyStatus current,
        DerivedWeaponPropertyStatus candidate)
    {
        if (candidate == DerivedWeaponPropertyStatus.Unsupported ||
            current == DerivedWeaponPropertyStatus.Success)
        {
            current = candidate;
        }
    }

    private readonly record struct PropertyScalar(
        decimal? Value,
        ParsedItemProperty? SourceProperty);
}
