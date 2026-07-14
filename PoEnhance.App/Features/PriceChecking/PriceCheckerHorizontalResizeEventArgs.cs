namespace PoEnhance.App.Features.PriceChecking;

public sealed class PriceCheckerHorizontalResizeEventArgs : EventArgs
{
    public PriceCheckerHorizontalResizeEventArgs(double horizontalChange)
    {
        HorizontalChange = horizontalChange;
    }

    public double HorizontalChange { get; }
}
