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

    /// <summary>
    /// True when selected contributors may participate in filtering under the current parent
    /// selection. Composition-capable Mod Types keep the established aggregate+child contract.
    /// When the parent checkbox is unchecked, individually selected sources may still filter by
    /// their own exact Trade identities even if the parent cannot compose an aggregate.
    /// </summary>
    public static bool AllowsContributorFiltering(ResolvedSearchComponent parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        return SupportsComposition(parent) || !parent.IsSelected;
    }

    /// <summary>
    /// True when selected contributors should be serialized/validated. Parent checkbox selection
    /// is intentionally independent of child checkboxes.
    /// </summary>
    public static bool IsFilteringActive(ResolvedSearchComponent parent) =>
        AllowsContributorFiltering(parent) &&
        parent.Contributors.Any(contributor => contributor.IsSelected) &&
        !AreSelectedContributorsSuspendedByParentBound(parent);

    public static int ActiveSelectionCount(ResolvedSearchComponent parent) =>
        AllowsContributorFiltering(parent)
            ? parent.Contributors.Count(contributor => contributor.IsSelected)
            : 0;

    public static SearchComponentContributorInactiveReason GetInactiveReason(
        ResolvedSearchComponent parent,
        SearchComponentContributor contributor)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(contributor);

        // Selected non-composition parents keep the established restriction: contributors cannot
        // ride along with Explicit/standalone aggregate encoding. Unchecked parents never block
        // independent source selection through this reason.
        if (parent.IsSelected && !SupportsComposition(parent))
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

        // Suspension only applies when the parent filter itself is selected and its bound
        // cannot cover the selected child floor. Unchecked parents never suspend children.
        return parent.IsSelected &&
            SupportsComposition(parent) &&
            SearchComponentContributorMath.TryGetSelectedAdditiveMinimumFloor(parent, out var floor) &&
            parent.Contributors.Any(contributor => contributor.IsSelected) &&
            (!parent.RequestedMinimum.HasValue || parent.RequestedMinimum.Value < floor);
    }
}
