using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed class ModifierEligibilityEvaluator
{
    public ModifierEligibilityResult Evaluate(
        ModifierDefinition modifier,
        ItemBaseRecord itemBase)
    {
        ArgumentNullException.ThrowIfNull(itemBase);

        return Evaluate(modifier, ItemModifierEligibilityContext.ForItemBase(itemBase));
    }

    public ModifierEligibilityResult Evaluate(
        ModifierDefinition modifier,
        ItemModifierEligibilityContext context)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        ArgumentNullException.ThrowIfNull(context);

        var modifierDomain = Normalize(modifier.Domain);
        var itemBaseDomain = Normalize(context.ItemBase.Domain);
        if (modifierDomain is null || itemBaseDomain is null)
        {
            return Unknown(
                evaluated: false,
                "Modifier and item-base domains are required to evaluate eligibility.",
                modifierDomain,
                itemBaseDomain);
        }

        var usesProviderOwnedAffixDomain = string.Equals(
            modifierDomain,
            "unveiled",
            StringComparison.OrdinalIgnoreCase);
        if (!string.Equals(modifierDomain, itemBaseDomain, StringComparison.OrdinalIgnoreCase) &&
            !usesProviderOwnedAffixDomain)
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

        if (context.ItemBase.Tags.Count == 0 && context.DynamicTags.Count == 0)
        {
            return Unknown(
                evaluated: false,
                "Item-base tags or dynamic item-context tags are required to evaluate modifier spawn weights.",
                modifierDomain,
                itemBaseDomain);
        }

        var staticTags = new HashSet<string>(
            context.ItemBase.Tags
                .Select(Normalize)
                .Where(tag => tag is not null)!,
            StringComparer.OrdinalIgnoreCase);
        var dynamicTags = new HashSet<string>(
            context.DynamicTags
                .Select(Normalize)
                .Where(tag => tag is not null)!,
            StringComparer.OrdinalIgnoreCase);
        var candidateTags = new HashSet<string>(staticTags, StringComparer.OrdinalIgnoreCase);
        candidateTags.UnionWith(dynamicTags);

        foreach (var spawnWeight in modifier.SpawnWeights)
        {
            var tag = Normalize(spawnWeight.Tag);
            if (tag is null || (!IsDefaultTag(tag) && !candidateTags.Contains(tag)))
            {
                continue;
            }

            var matchedDynamicTag = dynamicTags.Contains(tag);
            return spawnWeight.Weight > 0
                ? new ModifierEligibilityResult(
                    true,
                    ModifierEligibilityOutcome.Eligible,
                    matchedDynamicTag
                        ? ModifierEligibilityDiagnosticCodes.ModifierDynamicTagMatch
                        : ModifierEligibilityDiagnosticCodes.ModifierEligibleForBase,
                    matchedDynamicTag
                        ? "The first matching spawn-weight tag has a positive weight for the dynamic item context."
                        : "The first matching spawn-weight tag has a positive weight for the resolved item base.",
                    tag,
                    modifierDomain,
                    itemBaseDomain,
                    matchedDynamicTag)
                : new ModifierEligibilityResult(
                    true,
                    ModifierEligibilityOutcome.Ineligible,
                    ModifierEligibilityDiagnosticCodes.ModifierSpawnWeightZero,
                    "The first matching spawn-weight tag has zero weight for the resolved item base.",
                    tag,
                    modifierDomain,
                    itemBaseDomain,
                    matchedDynamicTag);
        }

        var hasInfluenceSpawnWeight = modifier.SpawnWeights
            .Select(spawnWeight => Normalize(spawnWeight.Tag))
            .Any(IsKnownTraditionalInfluenceTag);
        return new ModifierEligibilityResult(
            true,
            ModifierEligibilityOutcome.Ineligible,
            hasInfluenceSpawnWeight
                ? ModifierEligibilityDiagnosticCodes.ModifierRequiredInfluenceMissing
                : ModifierEligibilityDiagnosticCodes.ModifierNoMatchingBaseTag,
            hasInfluenceSpawnWeight
                ? "No modifier spawn-weight tag matched the resolved item-base tags or traditional influence context."
                : "No modifier spawn-weight tag matched the resolved item-base tags.",
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

    private static bool IsKnownTraditionalInfluenceTag(string? tag)
    {
        if (tag is null)
        {
            return false;
        }

        return tag.EndsWith("_shaper", StringComparison.OrdinalIgnoreCase)
            || tag.EndsWith("_elder", StringComparison.OrdinalIgnoreCase)
            || tag.EndsWith("_adjudicator", StringComparison.OrdinalIgnoreCase)
            || tag.EndsWith("_eyrie", StringComparison.OrdinalIgnoreCase)
            || tag.EndsWith("_basilisk", StringComparison.OrdinalIgnoreCase)
            || tag.EndsWith("_crusader", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
