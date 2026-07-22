namespace PoEnhance.App.Features.PriceChecking;

internal sealed class OfferCardPinRequestedEventArgs : EventArgs
{
    public OfferCardPinRequestedEventArgs(
        OfferCardSnapshot snapshot,
        PriceCheckerPlacement placement)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Placement = placement ?? throw new ArgumentNullException(nameof(placement));
    }

    public OfferCardSnapshot Snapshot { get; }

    public PriceCheckerPlacement Placement { get; }
}
