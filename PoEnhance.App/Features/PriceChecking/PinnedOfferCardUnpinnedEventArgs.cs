namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PinnedOfferCardUnpinnedEventArgs : EventArgs
{
    public PinnedOfferCardUnpinnedEventArgs(
        OfferCardSnapshot snapshot,
        PriceCheckerPlacement placement)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Placement = placement ?? throw new ArgumentNullException(nameof(placement));
    }

    public OfferCardSnapshot Snapshot { get; }

    public PriceCheckerPlacement Placement { get; }
}
