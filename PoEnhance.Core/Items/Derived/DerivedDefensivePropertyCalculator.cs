using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Items.Derived;

public sealed partial class DerivedWeaponPropertyCalculator
{
    private const int DefensiveNormalizedQuality = 20;

    public DerivedDefensiveProperties CalculateDefensiveQ20(
        ParsedItem parsedItem,
        ItemBaseRecord? itemBase,
        IReadOnlyList<DerivedWeaponModifierEffect> modifierEffects)
    {
        ArgumentNullException.ThrowIfNull(parsedItem);
        ArgumentNullException.ThrowIfNull(modifierEffects);

        var results = new List<DerivedDefensiveProperty>();
        AddQualityNormalized(results, parsedItem, itemBase, modifierEffects,
            ItemPropertyTarget.EnergyShield, "energy shield");
        AddQualityNormalized(results, parsedItem, itemBase, modifierEffects,
            ItemPropertyTarget.Armour, "armour");
        AddQualityNormalized(results, parsedItem, itemBase, modifierEffects,
            ItemPropertyTarget.Evasion, "evasion rating");
        AddWard(results, parsedItem, itemBase, modifierEffects);
        AddBlock(results, parsedItem, itemBase, modifierEffects);
        return new DerivedDefensiveProperties { Properties = results };
    }

    internal static decimal RoundDefence(decimal value) =>
        decimal.Round(value, 0, MidpointRounding.AwayFromZero);

    private static void AddQualityNormalized(
        ICollection<DerivedDefensiveProperty> results,
        ParsedItem parsedItem,
        ItemBaseRecord? itemBase,
        IReadOnlyList<DerivedWeaponModifierEffect> effects,
        ItemPropertyTarget target,
        string propertyName)
    {
        var property = FindSingleScalarProperty(parsedItem, propertyName, percentage: false);
        if (property is null)
        {
            return;
        }

        var observed = property.NumericGroups[0].ScalarValue!.Value;
        var quality = ReadQuality(parsedItem);
        var baseProperties = itemBase?.DefenceProperties;
        var range = BaseRange(baseProperties, target);
        var contributions = ReadContributions(effects, target);
        var unsupported = quality.Reason ?? contributions.Reason;
        if (range is null || baseProperties?.Sources.Count == 0)
        {
            unsupported ??= "The resolved item base has no exact sourced defensive base range.";
        }

        if (unsupported is null)
        {
            var candidates = Enumerable.Range(range!.Value.Minimum, range.Value.Maximum - range.Value.Minimum + 1)
                .Where(baseValue => RoundDefence(
                    (baseValue + contributions.Added) *
                    (1m + contributions.IncreasedPercent / 100m) *
                    (1m + quality.Value!.Value / 100m)) == observed)
                .ToArray();
            if (candidates.Length == 1)
            {
                var q20 = RoundDefence(
                    (candidates[0] + contributions.Added) *
                    (1m + contributions.IncreasedPercent / 100m) *
                    (1m + DefensiveNormalizedQuality / 100m));
                results.Add(Create(
                    target, q20, property, true, null, candidates[0], contributions,
                    quality.Value, baseProperties));
                return;
            }

            unsupported = candidates.Length == 0
                ? "The displayed defensive value cannot be reconstructed from the sourced base range and reviewed local effects."
                : "The displayed defensive value maps to more than one possible base roll, so exact Q20 normalization is ambiguous.";
        }

        results.Add(Create(
            target, observed, property, false, unsupported, null, contributions,
            quality.Value, baseProperties));
    }

    private static void AddWard(
        ICollection<DerivedDefensiveProperty> results,
        ParsedItem parsedItem,
        ItemBaseRecord? itemBase,
        IReadOnlyList<DerivedWeaponModifierEffect> effects)
    {
        var property = FindSingleScalarProperty(parsedItem, "ward", percentage: false);
        if (property is null)
        {
            return;
        }

        var contributions = ReadContributions(effects, ItemPropertyTarget.Ward);
        results.Add(Create(
            ItemPropertyTarget.Ward,
            property.NumericGroups[0].ScalarValue!.Value,
            property,
            false,
            contributions.Reason,
            null,
            contributions,
            ReadQuality(parsedItem).Value,
            itemBase?.DefenceProperties));
    }

    private static void AddBlock(
        ICollection<DerivedDefensiveProperty> results,
        ParsedItem parsedItem,
        ItemBaseRecord? itemBase,
        IReadOnlyList<DerivedWeaponModifierEffect> effects)
    {
        var property = FindSingleScalarProperty(parsedItem, "chance to block", percentage: true);
        if (property is null)
        {
            return;
        }

        var observed = property.NumericGroups[0].ScalarValue!.Value;
        var contributions = ReadContributions(effects, ItemPropertyTarget.Block);
        var baseProperties = itemBase?.DefenceProperties;
        var unsupported = contributions.Reason;
        if (baseProperties?.ChanceToBlockPercent is not { } baseBlock || baseProperties.Sources.Count == 0)
        {
            unsupported ??= "The resolved item base has no exact sourced item-local Chance to Block.";
        }
        else if (RoundDefence(
                     (baseBlock + contributions.Added) *
                     (1m + contributions.IncreasedPercent / 100m)) != observed)
        {
            unsupported ??= "The displayed Chance to Block cannot be reconstructed from sourced base block and reviewed local effects.";
        }

        results.Add(Create(
            ItemPropertyTarget.Block, observed, property, false, unsupported,
            baseProperties?.ChanceToBlockPercent, contributions, null, baseProperties));
    }

    private static DerivedDefensiveProperty Create(
        ItemPropertyTarget target,
        decimal value,
        ParsedItemProperty property,
        bool isQ20,
        string? unsupportedReason,
        int? baseValue,
        ContributionResult contributions,
        decimal? observedQuality,
        ItemBaseDefenceProperties? baseProperties) =>
        new()
        {
            Target = target,
            Value = value,
            SourceProperty = property,
            IsQ20 = isQ20,
            UnsupportedReason = unsupportedReason,
            ReconstructedBaseValue = baseValue,
            LocalAdded = contributions.Added,
            LocalIncreasedPercent = contributions.IncreasedPercent,
            ObservedQuality = observedQuality,
            BaseProperties = baseProperties,
            ModifierContributions = contributions.Provenance,
        };

    private static ContributionResult ReadContributions(
        IReadOnlyList<DerivedWeaponModifierEffect> effects,
        ItemPropertyTarget target)
    {
        var added = 0m;
        var increased = 0m;
        var provenance = new List<DerivedWeaponQ20ModifierProvenance>();
        foreach (var effect in effects)
        {
            if (effect.UsesPositionalFallback && CouldAffect(effect, target))
            {
                return new(added, increased, provenance,
                    $"Modifier component '{effect.ComponentId}' uses positional stat association fallback.");
            }

            if (HasUnsupportedControl(effect, target, out var controlReason))
            {
                return new(added, increased, provenance, controlReason);
            }

            var semantic = effect.ReviewedItemPropertySemantic;
            if (semantic is null)
            {
                if (CouldAffect(effect, target))
                {
                    return new(added, increased, provenance,
                        $"Modifier component '{effect.ComponentId}' may affect this displayed property without reviewed local semantics.");
                }

                continue;
            }

            if (target is ItemPropertyTarget.Armour or ItemPropertyTarget.Evasion or ItemPropertyTarget.EnergyShield &&
                semantic.Contributions.Any(contribution =>
                    contribution.Targets.Contains(ItemPropertyTarget.Quality)))
            {
                return new(added, increased, provenance,
                    "A local modifier changes item Quality, so ordinary Q20 cannot be separated safely.");
            }

            foreach (var contribution in semantic.Contributions.Where(contribution =>
                         contribution.Targets.Contains(target)))
            {
                if (!effect.IsExactlyResolved || !effect.IsLocal || !effect.HasProvenStatAssociation ||
                    semantic.Applicability != ItemPropertyApplicability.UnconditionalDisplayedLocal)
                {
                    return new(added, increased, provenance,
                        $"Modifier component '{effect.ComponentId}' has an unproven item-local association.");
                }

                if (contribution.Operation == ItemPropertyOperation.Added &&
                    effect.CanonicalNumericValues.Count == 1)
                {
                    added += effect.CanonicalNumericValues[0];
                }
                else if (contribution.Operation == ItemPropertyOperation.IncreasedPercent &&
                         effect.CanonicalNumericValues.Count == 1)
                {
                    increased += effect.CanonicalNumericValues[0];
                }
                else
                {
                    return new(added, increased, provenance,
                        $"Modifier component '{effect.ComponentId}' has an unsupported operation or numeric shape.");
                }

                provenance.Add(new DerivedWeaponQ20ModifierProvenance
                {
                    ComponentId = effect.ComponentId,
                    SourceModifierIndex = effect.SourceModifierIndex,
                    ResolvedModifierId = effect.ResolvedModifierId,
                    ReviewedSemanticDescriptorId = semantic.Id,
                    Operation = contribution.Operation,
                    CanonicalNumericValues = effect.CanonicalNumericValues.ToArray(),
                });
            }
        }

        return new(added, increased, provenance, null);
    }

    private static bool HasUnsupportedControl(
        DerivedWeaponModifierEffect effect,
        ItemPropertyTarget target,
        out string reason)
    {
        foreach (var value in effect.ResolvedStatIds)
        {
            var statId = value?.Trim() ?? string.Empty;
            if (statId.Contains("explicit_modifier_effect", StringComparison.OrdinalIgnoreCase) ||
                statId.Contains("effect_of_explicit_mod", StringComparison.OrdinalIgnoreCase) ||
                statId.Contains("per_quality", StringComparison.OrdinalIgnoreCase) ||
                (statId.Contains("quality_does_not_increase_defences", StringComparison.OrdinalIgnoreCase) &&
                 target is ItemPropertyTarget.Armour or ItemPropertyTarget.Evasion or ItemPropertyTarget.EnergyShield) ||
                ((statId.Contains("set_to_zero", StringComparison.OrdinalIgnoreCase) ||
                  statId.Contains("always_zero", StringComparison.OrdinalIgnoreCase)) && CouldAffect(effect, target)))
            {
                reason = $"The item has unsupported control or replacement behavior ('{statId}').";
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }

    private static bool CouldAffect(DerivedWeaponModifierEffect effect, ItemPropertyTarget target)
    {
        var terms = target switch
        {
            ItemPropertyTarget.Armour => new[] { "armour", "physical_damage_reduction_rating" },
            ItemPropertyTarget.Evasion => new[] { "evasion" },
            ItemPropertyTarget.EnergyShield => new[] { "energy_shield" },
            ItemPropertyTarget.Ward => new[] { "ward" },
            ItemPropertyTarget.Block => new[] { "block_chance", "additional_block" },
            _ => [],
        };
        return effect.ResolvedStatIds.Any(statId =>
            (effect.IsLocal || statId.StartsWith("local_", StringComparison.OrdinalIgnoreCase)) &&
            terms.Any(term => statId.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    private static ParsedItemProperty? FindSingleScalarProperty(
        ParsedItem item,
        string normalizedName,
        bool percentage)
    {
        var matches = item.Properties.Where(property =>
            string.Equals(property.NormalizedName, normalizedName, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 &&
               matches[0].NumericGroups.Count == 1 &&
               matches[0].NumericGroups[0].IsScalar &&
               matches[0].NumericGroups[0].IsPercentage == percentage &&
               matches[0].NumericGroups[0].ScalarValue is >= 0m
            ? matches[0]
            : null;
    }

    private static QualityResult ReadQuality(ParsedItem item)
    {
        var matches = item.Properties.Where(property =>
            string.Equals(property.NormalizedName, "quality", StringComparison.Ordinal)).ToArray();
        if (matches.Length == 0)
        {
            return new(0m, null);
        }

        if (matches.Length != 1 || matches[0].NumericGroups.Count != 1 ||
            !matches[0].NumericGroups[0].IsScalar || !matches[0].NumericGroups[0].IsPercentage ||
            matches[0].NumericGroups[0].ScalarValue is not { } value || value < 0m ||
            value != decimal.Truncate(value))
        {
            return new(null, "Observed Quality is malformed or ambiguous.");
        }

        return new(value, null);
    }

    private static DefenceRange? BaseRange(ItemBaseDefenceProperties? properties, ItemPropertyTarget target)
    {
        var (minimum, maximum) = target switch
        {
            ItemPropertyTarget.Armour => (properties?.ArmourMinimum, properties?.ArmourMaximum),
            ItemPropertyTarget.Evasion => (properties?.EvasionRatingMinimum, properties?.EvasionRatingMaximum),
            ItemPropertyTarget.EnergyShield => (properties?.EnergyShieldMinimum, properties?.EnergyShieldMaximum),
            ItemPropertyTarget.Ward => (properties?.WardMinimum, properties?.WardMaximum),
            _ => (null, null),
        };
        return minimum.HasValue && maximum.HasValue && minimum <= maximum
            ? new DefenceRange(minimum.Value, maximum.Value)
            : null;
    }

    private readonly record struct DefenceRange(int Minimum, int Maximum);
    private readonly record struct QualityResult(decimal? Value, string? Reason);
    private readonly record struct ContributionResult(
        decimal Added,
        decimal IncreasedPercent,
        IReadOnlyList<DerivedWeaponQ20ModifierProvenance> Provenance,
        string? Reason);
}
