namespace PoEnhance.Core.Trade;

/// <summary>
/// Counts unique user-selected Trade search criteria for the Price Checker header.
/// Mirrored shared criteria and duplicate Pseudo Total selections collapse to one.
/// Item influence / Foulborn state toggles are not counted as stat filters.
/// </summary>
public static class TradeSearchSelectedCriteriaCounter
{
    public static int Count(TradeSearchDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in draft.ItemProperties.Where(property => property.IsSelected))
        {
            keys.Add($"property:{(int)property.Kind}");
        }

        foreach (var criterion in draft.SharedCriteria.Where(candidate => candidate.IsSelected))
        {
            keys.Add(StatKey(criterion.ProviderStatId, $"shared:{criterion.CriterionId}"));
        }

        for (var index = 0; index < draft.ModifierFilters.Count; index++)
        {
            var modifier = draft.ModifierFilters[index];
            if (modifier.IsSelected)
            {
                keys.Add(StatKey(
                    modifier.ProviderStatId,
                    modifier.SelectedFilterVariantIdentity,
                    $"modifier:{index}"));
            }

            foreach (var logical in modifier.LogicalContributions.Where(candidate => candidate.IsSelected))
            {
                keys.Add(StatKey(
                    logical.ProviderStatId,
                    logical.SelectedFilterVariantIdentity,
                    $"logical:{index}:{logical.DisplayText}"));
            }

            if (!SearchComponentContributorActivation.IsFilteringActive(modifier))
            {
                continue;
            }

            foreach (var contributor in modifier.Contributors.Where(candidate => candidate.IsSelected))
            {
                if (SearchComponentContributorActivation.GetInactiveReason(modifier, contributor) !=
                    SearchComponentContributorInactiveReason.None)
                {
                    continue;
                }

                // Group-A composite sources fold into the selected parent logical/Pseudo filter.
                if (modifier.IsSelected &&
                    ApprovedMultiEffectProjectionCatalog.IsApprovedCompositeStat(
                        contributor.Source.ResolvedStatIds) &&
                    modifier.ResolvedStatIds.Count == 1 &&
                    !string.IsNullOrWhiteSpace(modifier.ProviderStatId))
                {
                    continue;
                }

                keys.Add(StatKey(
                    contributor.ProviderIdentity,
                    $"contributor:{index}:{contributor.DisplayText}"));
            }
        }

        return keys.Count;
    }

    private static string StatKey(string? primary, string fallback) =>
        string.IsNullOrWhiteSpace(primary) ? fallback : $"stat:{primary.Trim()}";

    private static string StatKey(string? primary, string? secondary, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return $"stat:{primary.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(secondary))
        {
            return $"stat:{secondary.Trim()}";
        }

        return fallback;
    }
}
