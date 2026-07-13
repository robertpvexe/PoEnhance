namespace PoEnhance.App.Features.PriceChecking;

public sealed class PriceCheckerHorizontalDragEventArgs : EventArgs
{
    public PriceCheckerHorizontalDragEventArgs(double horizontalChange)
    {
        HorizontalChange = horizontalChange;
    }

    public double HorizontalChange { get; }
}
