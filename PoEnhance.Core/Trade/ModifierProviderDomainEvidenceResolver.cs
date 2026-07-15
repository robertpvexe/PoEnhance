using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Trade;

internal static class ModifierProviderDomainEvidenceResolver
{
    public static IReadOnlyList<SearchComponentProviderDomainEvidence> Resolve(
        ResolvedSearchComponent component,
        ModifierDefinition sourceModifier,
        IReadOnlyList<string> componentLines,
        ItemBaseResolutionResult? itemBaseResolution,
        IReadOnlyList<string> traditionalInfluences,
        GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(sourceModifier);
        ArgumentNullException.ThrowIfNull(componentLines);
        ArgumentNullException.ThrowIfNull(traditionalInfluences);
        ArgumentNullException.ThrowIfNull(catalog);

        var evidence = new List<SearchComponentProviderDomainEvidence>();
        var itemBase = itemBaseResolution?.MatchedItemBase;
        var sourceDomain = CanonicalModifierEffectAggregator.ProviderDomainFor(component);
        AddEvidence(
            evidence,
            sourceDomain,
            sourceModifier,
            itemBase,
            matchedTag: null,
            isSourceExact: true,
            isProjectedDomain: false,
            evidenceStrength: 1000,
            reasonCode: "SOURCE_EXACT",
            "The copied modifier resolved exactly to this GameData source family.",
            component.ResolvedStatIds,
            catalog);

        if (itemBase is null || component.ResolvedStatIds.Count == 0)
        {
            return evidence;
        }

        var eligibilityContext = ItemModifierEligibilityContext.Create(itemBase, traditionalInfluences);
        foreach (var evaluation in ModifierProviderDomainEligibilityIndex
                     .For(catalog)
                     .Evaluate(component, sourceModifier, eligibilityContext)
                     .Where(evaluation => evaluation.Status == ModifierProviderDomainEligibilityStatus.Supported))
        {
            AddEvidence(
                evidence,
                evaluation.ProviderDomain,
                evaluation.Modifier,
                itemBase,
                evaluation.MatchedTag,
                isSourceExact: false,
                evaluation.IsProjectedDomain,
                evaluation.EvidenceStrength,
                evaluation.ReasonCode,
                evaluation.Reason,
                component.ResolvedStatIds,
                catalog);
        }

        return evidence
            .DistinctBy(entry => string.Join('\u001f', entry.ProviderDomain, entry.ModifierId), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(entry => entry.IsSourceExact)
            .ThenBy(entry => entry.ProviderDomain, StringComparer.Ordinal)
            .ThenBy(entry => entry.ModifierId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddEvidence(
        ICollection<SearchComponentProviderDomainEvidence> evidence,
        string providerDomain,
        ModifierDefinition modifier,
        ItemBaseRecord? itemBase,
        string? matchedTag,
        bool isSourceExact,
        bool isProjectedDomain,
        int evidenceStrength,
        string reasonCode,
        string reason,
        IReadOnlyList<string> componentStatIds,
        GameDataCatalog catalog)
    {
        var modifierId = Normalize(modifier.Id);
        if (modifierId is null || string.Equals(providerDomain, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        evidence.Add(new SearchComponentProviderDomainEvidence
        {
            ProviderDomain = providerDomain,
            ModifierId = modifierId,
            GenerationType = modifier.GenerationType,
            Locality = DetermineLocality(modifier, componentStatIds, catalog),
            SourceGenerationType = Normalize(modifier.SourceGenerationType),
            IsSourceExact = isSourceExact,
            IsProjectedDomain = isProjectedDomain,
            EvidenceStrength = evidenceStrength,
            ItemBaseId = Normalize(itemBase?.Id),
            ItemClass = Normalize(itemBase?.ItemClass),
            MatchedTag = Normalize(matchedTag),
            ApplicabilityReasonCode = reasonCode,
            ApplicabilityReason = reason,
        });
    }

    private static ModifierLocality DetermineLocality(
        ModifierDefinition modifier,
        IReadOnlyList<string> componentStatIds,
        GameDataCatalog catalog)
    {
        var expected = componentStatIds
            .Where(statId => !string.IsNullOrWhiteSpace(statId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var componentStats = modifier.Stats
            .Where(stat => !string.IsNullOrWhiteSpace(stat.StatId) && expected.Contains(stat.StatId))
            .ToArray();
        if (componentStats.Length != expected.Count)
        {
            return ModifierLocality.Unknown;
        }

        var localCount = 0;
        var globalCount = 0;
        foreach (var modifierStat in componentStats)
        {
            var stat = catalog.FindStatsById(modifierStat.StatId).SingleOrDefault();
            if (stat is null)
            {
                return ModifierLocality.Unknown;
            }

            if (stat.IsLocal)
            {
                localCount++;
            }
            else
            {
                globalCount++;
            }
        }

        return (localCount, globalCount) switch
        {
            (> 0, 0) => ModifierLocality.Local,
            (0, > 0) => ModifierLocality.Global,
            _ => ModifierLocality.Unknown,
        };
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
