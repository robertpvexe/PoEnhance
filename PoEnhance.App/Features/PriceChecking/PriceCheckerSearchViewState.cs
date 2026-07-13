namespace PoEnhance.App.Features.PriceChecking;

public sealed record PriceCheckerSearchViewState
{
    public PriceCheckerSearchViewStatus Status { get; init; } = PriceCheckerSearchViewStatus.Idle;

    public string LeagueIdentifier { get; init; } = PriceCheckerSearchController.DefaultLeagueIdentifier;

    public bool CanSearch { get; init; }

    public string Message { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<PriceCheckerOfferViewModel> Offers { get; init; } = [];

    public bool IsLoading => Status == PriceCheckerSearchViewStatus.Loading;
}
