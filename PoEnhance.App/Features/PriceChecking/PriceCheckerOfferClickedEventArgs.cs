namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerOfferClickedEventArgs : EventArgs
{
    public PriceCheckerOfferClickedEventArgs(OfferCardSnapshot snapshot)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public OfferCardSnapshot Snapshot { get; }
}
