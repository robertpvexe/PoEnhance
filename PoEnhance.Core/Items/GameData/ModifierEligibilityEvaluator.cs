using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed class ModifierEligibilityEvaluator
{
    public ModifierEligibilityResult Evaluate(
        ModifierDefinition modifier,
        ItemBaseRecord itemBase)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        ArgumentNullException.ThrowIfNull(itemBase);

        var modifierDomain = Normalize(modifier.Domain);
        var itemBaseDomain = Normalize(itemBase.Domain);
        if (modifierDomain is null || itemBaseDomain is null)
        {
            return Unknown(
                evaluated: false,
                "Modifier and item-base domains are required to evaluate eligibility.",
                modifierDomain,
                itemBaseDomain);
        }

        if (!string.Equals(modifierDomain, itemBaseDomain, StringComparison.OrdinalIgnoreCase))
        {
            return new ModifierEligibilityResult(
                true,
                ModifierEligibilityOutcome.Ineligible,
                ModifierEligibilityDiagnosticCodes.ModifierDomainMismatch,
                "The modifier domain does not match the resolved item-base domain.",
                ModifierDomain: modifierDomain,
                ItemBaseDomain: itemBaseDomain);
        }

        if (modifier.SpawnWeights.Count == 0)
        {
            return Unknown(
                evaluated: false,
                "Modifier spawn weights are required to evaluate item-base eligibility.",
                modifierDomain,
                itemBaseDomain);
        }

        if (itemBase.Tags.Count == 0)
        {
            return Unknown(
                evaluated: false,
                "Item-base tags are required to evaluate modifier spawn weights.",
                modifierDomain,
                itemBaseDomain);
        }

        var baseTags = new HashSet<string>(
            itemBase.Tags
                .Select(Normalize)
                .Where(tag => tag is not null)!,
            StringComparer.OrdinalIgnoreCase);

        foreach (var spawnWeight in modifier.SpawnWeights)
        {
            var tag = Normalize(spawnWeight.Tag);
            if (tag is null || (!IsDefaultTag(tag) && !baseTags.Contains(tag)))
            {
                continue;
            }

            return spawnWeight.Weight > 0
                ? new ModifierEligibilityResult(
                    true,
                    ModifierEligibilityOutcome.Eligible,
                    ModifierEligibilityDiagnosticCodes.ModifierEligibleForBase,
                    "The first matching spawn-weight tag has a positive weight for the resolved item base.",
                    tag,
                    modifierDomain,
                    itemBaseDomain)
                : new ModifierEligibilityResult(
                    true,
                    ModifierEligibilityOutcome.Ineligible,
                    ModifierEligibilityDiagnosticCodes.ModifierSpawnWeightZero,
                    "The first matching spawn-weight tag has zero weight for the resolved item base.",
                    tag,
                    modifierDomain,
                    itemBaseDomain);
        }

        return new ModifierEligibilityResult(
            true,
            ModifierEligibilityOutcome.Ineligible,
            ModifierEligibilityDiagnosticCodes.ModifierNoMatchingBaseTag,
            "No modifier spawn-weight tag matched the resolved item-base tags.",
            ModifierDomain: modifierDomain,
            ItemBaseDomain: itemBaseDomain);
    }

    private static ModifierEligibilityResult Unknown(
        bool evaluated,
        string reason,
        string? modifierDomain,
        string? itemBaseDomain)
    {
        return new ModifierEligibilityResult(
            evaluated,
            ModifierEligibilityOutcome.Unknown,
            ModifierEligibilityDiagnosticCodes.ModifierEligibilityUnknown,
            reason,
            ModifierDomain: modifierDomain,
            ItemBaseDomain: itemBaseDomain);
    }

    private static bool IsDefaultTag(string tag)
    {
        return string.Equals(tag, "default", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
