namespace PoEnhance.Core.Trade;

public static class SearchComponentContributorActivation
{
    public static bool SupportsComposition(ResolvedSearchComponent parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var selected = parent.FilterVariants.FirstOrDefault(variant => string.Equals(
            variant.Identity,
            parent.SelectedFilterVariantIdentity,
            StringComparison.Ordinal));
        return selected?.SupportsContributorComposition == true;
    }

    public static bool IsFilteringActive(ResolvedSearchComponent parent) =>
        parent.IsSelected &&
        SupportsComposition(parent) &&
        !AreSelectedContributorsSuspendedByParentBound(parent);

    public static int ActiveSelectionCount(ResolvedSearchComponent parent) =>
        IsFilteringActive(parent)
            ? parent.Contributors.Count(contributor => contributor.IsSelected)
            : 0;

    public static SearchComponentContributorInactiveReason GetInactiveReason(
        ResolvedSearchComponent parent,
        SearchComponentContributor contributor)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(contributor);

        if (!SupportsComposition(parent))
        {
            return SearchComponentContributorInactiveReason.ParentModeDoesNotComposeContributors;
        }

        return contributor.IsSelected && AreSelectedContributorsSuspendedByParentBound(parent)
            ? SearchComponentContributorInactiveReason.ParentBoundBelowSelectedChildFloor
            : SearchComponentContributorInactiveReason.None;
    }

    public static bool AreSelectedContributorsSuspendedByParentBound(
        ResolvedSearchComponent parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        return parent.IsSelected &&
            SupportsComposition(parent) &&
            SearchComponentContributorMath.TryGetSelectedAdditiveMinimumFloor(parent, out var floor) &&
            parent.Contributors.Any(contributor => contributor.IsSelected) &&
            (!parent.RequestedMinimum.HasValue || parent.RequestedMinimum.Value < floor);
    }
}
