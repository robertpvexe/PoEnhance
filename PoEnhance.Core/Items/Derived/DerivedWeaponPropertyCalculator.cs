using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Items.Derived;

public sealed partial class DerivedWeaponPropertyCalculator
{
    private const string PhysicalDamageName = "physical damage";
    private const string ElementalDamageName = "elemental damage";
    private const string ChaosDamageName = "chaos damage";
    private const string AttacksPerSecondName = "attacks per second";
    private const string CriticalStrikeChanceName = "critical strike chance";
    private const string QualityName = "quality";
    private const int NormalizedQuality = 20;
    private const string Q20ReconstructionMethod =
        "Round each endpoint to the nearest integer, midpoint away from zero, after applying (base + proven local added Physical Damage) * (1 + (proven local increased Physical Damage + 20 Quality) / 100); multiply the rounded endpoint average by displayed APS.";

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

    public DerivedWeaponProperties CalculateQ20(
        ParsedItem parsedItem,
        ItemBaseRecord? itemBase,
        IReadOnlyList<DerivedWeaponModifierEffect> modifierEffects)
    {
        ArgumentNullException.ThrowIfNull(parsedItem);
        ArgumentNullException.ThrowIfNull(modifierEffects);

        var displayed = Calculate(parsedItem);
        if (displayed.Status != DerivedWeaponPropertyStatus.Success)
        {
            return displayed;
        }

        var qualityResult = ReadObservedQuality(parsedItem);
        var provenance = new DerivedWeaponQ20Provenance
        {
            ObservedQuality = qualityResult.ObservedQuality,
            ObservedQualityAssumedZero = qualityResult.AssumedZero,
            BaseItemId = itemBase?.Id,
            BaseWeaponProperties = itemBase?.WeaponProperties,
        };
        if (qualityResult.UnsupportedReason is not null)
        {
            return Q20Unsupported(displayed, provenance, qualityResult.UnsupportedReason, qualityResult.SourceProperty);
        }

        if (displayed.PhysicalDamage is null || displayed.AttacksPerSecond is not > 0m)
        {
            return Q20Unsupported(
                displayed,
                provenance,
                "A displayed Physical Damage range and Attacks per Second value are required for exact Q20 normalization.",
                displayed.PhysicalDamage?.SourceProperty ?? displayed.AttacksPerSecondSourceProperty);
        }

        var baseProperties = itemBase?.WeaponProperties;
        if (baseProperties?.PhysicalDamageMinimum is null ||
            baseProperties.PhysicalDamageMaximum is null)
        {
            return Q20Unsupported(
                displayed,
                provenance,
                "The resolved item base does not retain an exact sourced Physical Damage minimum and maximum.",
                displayed.PhysicalDamage.SourceProperty);
        }

        if (baseProperties.Sources.Count == 0)
        {
            return Q20Unsupported(
                displayed,
                provenance,
                "The resolved item base numerical weapon properties have no retained provenance.",
                displayed.PhysicalDamage.SourceProperty);
        }

        var localAddedMinimum = 0m;
        var localAddedMaximum = 0m;
        var localIncreasedPercent = 0m;
        var contributionProvenance = new List<DerivedWeaponQ20ModifierProvenance>();
        foreach (var effect in modifierEffects)
        {
            var safetyReason = UnsafeEffectReason(effect);
            if (safetyReason is not null)
            {
                return Q20Unsupported(displayed, provenance, safetyReason, displayed.PhysicalDamage.SourceProperty);
            }

            var semantic = effect.ReviewedItemPropertySemantic;
            if (semantic is null)
            {
                if (CouldAffectLocalPhysicalDamage(effect))
                {
                    return Q20Unsupported(
                        displayed,
                        provenance,
                        $"Modifier component '{effect.ComponentId}' may affect local Physical Damage without exact reviewed component semantics.",
                        displayed.PhysicalDamage.SourceProperty);
                }

                continue;
            }

            foreach (var contribution in semantic.Contributions.Where(contribution =>
                         contribution.Targets.Contains(ItemPropertyTarget.PhysicalDamage)))
            {
                if (!effect.IsExactlyResolved ||
                    !effect.IsLocal ||
                    !effect.HasProvenStatAssociation ||
                    semantic.Applicability != ItemPropertyApplicability.UnconditionalDisplayedLocal)
                {
                    return Q20Unsupported(
                        displayed,
                        provenance,
                        $"Modifier component '{effect.ComponentId}' has an unproven local Physical Damage association.",
                        displayed.PhysicalDamage.SourceProperty);
                }

                switch (contribution.Operation)
                {
                    case ItemPropertyOperation.Added when effect.CanonicalNumericValues.Count == 2:
                        localAddedMinimum += effect.CanonicalNumericValues[0];
                        localAddedMaximum += effect.CanonicalNumericValues[1];
                        break;
                    case ItemPropertyOperation.IncreasedPercent when effect.CanonicalNumericValues.Count == 1:
                        localIncreasedPercent += effect.CanonicalNumericValues[0];
                        break;
                    default:
                        return Q20Unsupported(
                            displayed,
                            provenance,
                            $"Modifier component '{effect.ComponentId}' has an unsupported Physical Damage operation " +
                            $"or numeric shape ({contribution.Operation}, {effect.CanonicalNumericValues.Count} values).",
                            displayed.PhysicalDamage.SourceProperty);
                }

                contributionProvenance.Add(new DerivedWeaponQ20ModifierProvenance
                {
                    ComponentId = effect.ComponentId,
                    SourceModifierIndex = effect.SourceModifierIndex,
                    ResolvedModifierId = effect.ResolvedModifierId,
                    ReviewedSemanticDescriptorId = semantic.Id,
                    Operation = contribution.Operation,
                    CanonicalNumericValues = effect.CanonicalNumericValues.ToArray(),
                });
            }

            if (semantic.Contributions.Any(contribution =>
                    contribution.Targets.Contains(ItemPropertyTarget.Quality)))
            {
                return Q20Unsupported(
                    displayed,
                    provenance,
                    "A local modifier changes item Quality, so exactly 20 normal weapon Quality cannot be separated safely.",
                    displayed.PhysicalDamage.SourceProperty);
            }
        }

        var physicalScale = 1m + ((localIncreasedPercent + NormalizedQuality) / 100m);
        if (physicalScale < 0m ||
            baseProperties.PhysicalDamageMinimum.Value + localAddedMinimum < 0m ||
            baseProperties.PhysicalDamageMaximum.Value + localAddedMaximum < 0m)
        {
            return Q20Unsupported(
                displayed,
                provenance,
                "The reconstructed Q20 Physical Damage range is negative or uses unsupported set-to-zero behavior.",
                displayed.PhysicalDamage.SourceProperty);
        }

        var q20Minimum = RoundPhysicalEndpoint(
            (baseProperties.PhysicalDamageMinimum.Value + localAddedMinimum) * physicalScale);
        var q20Maximum = RoundPhysicalEndpoint(
            (baseProperties.PhysicalDamageMaximum.Value + localAddedMaximum) * physicalScale);
        if (q20Maximum < q20Minimum)
        {
            return Q20Unsupported(
                displayed,
                provenance,
                "The reconstructed Q20 Physical Damage endpoints are not ordered.",
                displayed.PhysicalDamage.SourceProperty);
        }

        var sourceGroup = displayed.PhysicalDamage.SourceProperty.NumericGroups[0];
        var q20Group = sourceGroup with
        {
            OriginalText = $"{q20Minimum:G29}-{q20Maximum:G29}",
            ScalarValue = null,
            MinimumValue = q20Minimum,
            MaximumValue = q20Maximum,
            IsPercentage = false,
        };
        var q20AverageHit = (q20Minimum + q20Maximum) / 2m;
        var q20PhysicalDamage = new DerivedWeaponDamage(
            displayed.PhysicalDamage.SourceProperty,
            [new DerivedWeaponDamageRange(q20Group, q20AverageHit)],
            q20AverageHit,
            q20AverageHit * displayed.AttacksPerSecond.Value);
        var q20TotalDps = q20PhysicalDamage.DamagePerSecond +
            (displayed.ElementalDps ?? 0m) +
            (displayed.ChaosDps ?? 0m);

        return displayed with
        {
            PhysicalDamage = q20PhysicalDamage,
            TotalDps = q20TotalDps,
            Q20Status = DerivedWeaponQ20Status.Success,
            Q20Provenance = provenance with
            {
                ModifierContributions = contributionProvenance,
                ReconstructionMethod = Q20ReconstructionMethod,
            },
        };
    }

    internal static decimal RoundPhysicalEndpoint(decimal value) =>
        decimal.Round(value, 0, MidpointRounding.AwayFromZero);

    private static ObservedQualityResult ReadObservedQuality(ParsedItem parsedItem)
    {
        var properties = FindProperties(parsedItem, QualityName);
        if (properties.Count == 0)
        {
            return new ObservedQualityResult(0m, AssumedZero: true, null, null);
        }

        if (properties.Count != 1)
        {
            return new ObservedQualityResult(
                null,
                AssumedZero: false,
                "More than one Quality property was parsed; observed Quality is ambiguous.",
                properties[0]);
        }

        var property = properties[0];
        if (property.NumericGroups.Count != 1 ||
            !property.NumericGroups[0].IsScalar ||
            !property.NumericGroups[0].IsPercentage)
        {
            return new ObservedQualityResult(
                null,
                AssumedZero: false,
                "Observed Quality must contain one percentage integer.",
                property);
        }

        var value = property.NumericGroups[0].ScalarValue!.Value;
        if (value < 0m || value != decimal.Truncate(value))
        {
            return new ObservedQualityResult(
                null,
                AssumedZero: false,
                "Observed Quality must be a non-negative integer percentage.",
                property);
        }

        return new ObservedQualityResult(value, AssumedZero: false, null, property);
    }

    private static string? UnsafeEffectReason(DerivedWeaponModifierEffect effect)
    {
        if (effect.UsesPositionalFallback)
        {
            return $"Modifier component '{effect.ComponentId}' uses positional stat association fallback.";
        }

        foreach (var statIdValue in effect.ResolvedStatIds)
        {
            var statId = statIdValue?.Trim() ?? string.Empty;
            if (string.Equals(
                    statId,
                    "local_quality_does_not_increase_physical_damage",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "The item changes ordinary weapon Quality so it does not increase Physical Damage.";
            }

            if (statId.Contains("per_quality", StringComparison.OrdinalIgnoreCase) &&
                (statId.Contains("attack_speed", StringComparison.OrdinalIgnoreCase) ||
                 statId.Contains("critical", StringComparison.OrdinalIgnoreCase) ||
                 statId.Contains("elemental", StringComparison.OrdinalIgnoreCase)))
            {
                return $"The item has alternate per-Quality weapon behavior ('{statId}').";
            }

            if (statId.Contains("explicit_modifier_effect", StringComparison.OrdinalIgnoreCase) ||
                statId.Contains("effect_of_explicit_mod", StringComparison.OrdinalIgnoreCase))
            {
                return $"The item has an indirect explicit-modifier-effect scaler ('{statId}').";
            }

            if (statId.Contains("physical_damage", StringComparison.OrdinalIgnoreCase) &&
                (statId.Contains("set_to_zero", StringComparison.OrdinalIgnoreCase) ||
                 statId.Contains("always_zero", StringComparison.OrdinalIgnoreCase)))
            {
                return $"The item has unsupported set-to-zero Physical Damage behavior ('{statId}').";
            }
        }

        return null;
    }

    private static bool CouldAffectLocalPhysicalDamage(DerivedWeaponModifierEffect effect)
    {
        return effect.ResolvedStatIds.Any(statId =>
            !statId.Contains("leech_from_physical_damage", StringComparison.OrdinalIgnoreCase) &&
            statId.Contains("physical_damage", StringComparison.OrdinalIgnoreCase) &&
            (effect.IsLocal || statId.StartsWith("local_", StringComparison.OrdinalIgnoreCase)));
    }

    private static DerivedWeaponProperties Q20Unsupported(
        DerivedWeaponProperties displayed,
        DerivedWeaponQ20Provenance provenance,
        string reason,
        ParsedItemProperty? sourceProperty)
    {
        return displayed with
        {
            PhysicalDamage = null,
            TotalDps = null,
            Q20Status = DerivedWeaponQ20Status.Unsupported,
            Q20Provenance = provenance with { UnsupportedReason = reason },
            Diagnostics = displayed.Diagnostics.Concat(
            [
                new DerivedWeaponPropertyDiagnostic(
                    DerivedWeaponPropertyDiagnosticCodes.Q20NormalizationUnsupported,
                    reason,
                    sourceProperty),
            ]).ToArray(),
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

    private readonly record struct ObservedQualityResult(
        decimal? ObservedQuality,
        bool AssumedZero,
        string? UnsupportedReason,
        ParsedItemProperty? SourceProperty);
}
