namespace PoEnhance.App.Features.PriceChecking;

public sealed record PriceCheckerOfferViewModel
{
    public required string Id { get; init; }

    public required string ItemName { get; init; }

    public required string SellerAccountName { get; init; }

    public required string ListedText { get; init; }

    public string? ListedToolTip { get; init; }

    public required string ItemLevelText { get; init; }

    public required string PriceText { get; init; }

    public required OfferCardSnapshot CardSnapshot { get; init; }
}
