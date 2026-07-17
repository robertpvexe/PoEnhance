namespace PoEnhance.App.Features.PriceChecking;

public sealed record PriceCheckerSearchViewState
{
    public PriceCheckerSearchViewStatus Status { get; init; } = PriceCheckerSearchViewStatus.Idle;

    public string LeagueIdentifier { get; init; } = PriceCheckerSearchController.DefaultLeagueIdentifier;

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

    public int SelectedModifierCount =>
        Modifiers.Count(modifier => modifier.IsSelected) +
        ItemProperties.Sum(property => property.Children.Count(modifier => modifier.IsSelected));

    public int ModifierCount =>
        Modifiers.Count + ItemProperties.Sum(property => property.Children.Count);

    public int SelectedStatsCount => SelectedItemPropertyCount + SelectedModifierCount;

    public int StatsCount => ItemPropertyCount + ModifierCount;
}
