namespace PoEnhance.App.Features.PriceChecking;

internal sealed record PinnedOfferCardPinResult(
    bool IsSuccess,
    bool IsAlreadyPinned,
    string? Feedback)
{
    public static PinnedOfferCardPinResult Success() => new(true, false, null);

    public static PinnedOfferCardPinResult AlreadyPinned() => new(false, true, null);

    public static PinnedOfferCardPinResult Failure(string feedback) => new(false, false, feedback);
}
