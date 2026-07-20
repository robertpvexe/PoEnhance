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

    public int SelectedStatsCount => SelectedItemPropertyCount + SelectedModifierCount;

    public int StatsCount => ItemPropertyCount + ModifierCount;

    private IEnumerable<PriceCheckerModifierViewModel> AllCanonicalModifiers() =>
        Modifiers.Concat(ItemProperties.SelectMany(property => property.Children));
}
