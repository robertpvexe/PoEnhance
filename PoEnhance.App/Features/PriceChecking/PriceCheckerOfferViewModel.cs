namespace PoEnhance.App.Features.PriceChecking;

public sealed record PriceCheckerOfferViewModel
{
    public required string PriceText { get; init; }

    public required string SellerText { get; init; }

    public required string OnlineStatusText { get; init; }

    public required string ItemText { get; init; }

    public required string IndexedText { get; init; }
}
