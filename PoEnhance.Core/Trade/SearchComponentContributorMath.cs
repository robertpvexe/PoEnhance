namespace PoEnhance.Core.Trade;

public static class SearchComponentContributorMath
{
    public static bool TryGetActiveAdditiveMinimumFloor(
        ResolvedSearchComponent parent,
        out decimal floor)
    {
        ArgumentNullException.ThrowIfNull(parent);

        if (!SearchComponentContributorActivation.IsFilteringActive(parent))
        {
            floor = 0m;
            return true;
        }

        return TryGetSelectedAdditiveMinimumFloor(parent, out floor);
    }

    public static bool TryGetSelectedAdditiveMinimumFloor(
        ResolvedSearchComponent parent,
        out decimal floor)
    {
        ArgumentNullException.ThrowIfNull(parent);

        floor = 0m;
        var selected = parent.Contributors
            .Where(contributor => contributor.IsSelected)
            .ToArray();
        if (selected.Length == 0)
        {
            return true;
        }

        if (parent.ContributorProjection != SearchComponentContributorProjection.Additive ||
            parent.ValueBoundShape != ModifierBoundShape.Scalar ||
            selected.Any(contributor => !contributor.RequestedMinimum.HasValue))
        {
            return false;
        }

        floor = selected.Sum(contributor => contributor.RequestedMinimum!.Value);
        return true;
    }
}
