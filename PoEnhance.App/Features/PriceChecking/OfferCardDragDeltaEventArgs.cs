namespace PoEnhance.App.Features.PriceChecking;

internal sealed class OfferCardDragDeltaEventArgs : EventArgs
{
    public OfferCardDragDeltaEventArgs(double horizontalChange, double verticalChange)
    {
        if (!double.IsFinite(horizontalChange))
        {
            throw new ArgumentOutOfRangeException(nameof(horizontalChange));
        }

        if (!double.IsFinite(verticalChange))
        {
            throw new ArgumentOutOfRangeException(nameof(verticalChange));
        }

        HorizontalChange = horizontalChange;
        VerticalChange = verticalChange;
    }

    public double HorizontalChange { get; }

    public double VerticalChange { get; }
}
