namespace PoEnhance.App.Features.PriceChecking;

public sealed record PriceCheckerSearchViewState
{
    public PriceCheckerSearchViewStatus Status { get; init; } = PriceCheckerSearchViewStatus.Idle;

    public string? LeagueIdentifier { get; init; }

    public bool CanSearch { get; init; }

    public bool CanLoadMore { get; init; }

    public bool CanOpenTrade { get; init; }

    public string Message { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<PriceCheckerItemPropertyViewModel> ItemProperties { get; init; } = [];

    public IReadOnlyList<PriceCheckerModifierViewModel> Modifiers { get; init; } = [];

    public IReadOnlyList<object> Stats => [.. ItemProperties, .. Modifiers];

    public IReadOnlyList<PriceCheckerOfferViewModel> Offers { get; init; } = [];

    public bool IsLoading => Status == PriceCheckerSearchViewStatus.Loading;

    public int SelectedItemPropertyCount => ItemProperties.Count(property => property.IsSelected);

    public int ItemPropertyCount => ItemProperties.Count;

    public int SelectedModifierCount => AllCanonicalModifiers()
        .Where(modifier => modifier.IsSelected)
        .Select(modifier => modifier.SourceIndex)
        .Distinct()
        .Count();

    public int ModifierCount => AllCanonicalModifiers()
        .Select(modifier => modifier.SourceIndex)
        .Distinct()
        .Count();

    /// <summary>
    /// Unique user-selected Trade search criteria (item properties, parent filters, nested
    /// logical/shared children). Mirrored shared checkboxes and duplicate Pseudo Totals count once.
    /// </summary>
    public int SelectedStatsCount => CountUniqueSelectedCriteria();

    /// <summary>
    /// Physical parent / item-property row count (denominator legacy). Prefer
    /// <see cref="SelectedStatsCount"/> with "Stats: N filters selected" copy.
    /// </summary>
    public int StatsCount => ItemPropertyCount + ModifierCount;

    private int CountUniqueSelectedCriteria()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in ItemProperties.Where(property => property.IsSelected))
        {
            keys.Add($"property:{(int)property.Kind}");
        }

        foreach (var modifier in AllCanonicalModifiers())
        {
            if (modifier.IsSelected)
            {
                keys.Add(CriterionKey(
                    modifier.SelectedFilterVariant?.Identity,
                    $"modifier:{modifier.SourceIndex}"));
            }

            foreach (var child in modifier.Contributors)
            {
                if (!child.IsSelected || child.IsInactive)
                {
                    continue;
                }

                if (child.IsSharedCriterionMirror)
                {
                    keys.Add($"shared:{child.SharedCriterionId}");
                    continue;
                }

                if (child.HasFilterVariants)
                {
                    keys.Add(CriterionKey(
                        child.SelectedFilterVariant?.Identity,
                        $"logical:{child.ParentSourceIndex}:{child.ContributorIndex}"));
                    continue;
                }

                // Additive Exact source children without Mod Type variants: independent only when
                // the parent is unchecked (otherwise folded into / composed with the parent).
                if (!modifier.IsSelected)
                {
                    keys.Add($"contributor:{child.ParentSourceIndex}:{child.ContributorIndex}");
                }
            }
        }

        return keys.Count;
    }

    private static string CriterionKey(string? identity, string fallback) =>
        string.IsNullOrWhiteSpace(identity) ? fallback : $"criterion:{identity.Trim()}";

    private IEnumerable<PriceCheckerModifierViewModel> AllCanonicalModifiers() =>
        Modifiers.Concat(ItemProperties.SelectMany(property => property.Children));
}
