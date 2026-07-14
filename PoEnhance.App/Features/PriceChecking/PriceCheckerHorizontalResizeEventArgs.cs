namespace PoEnhance.App.Features.PriceChecking;

public sealed class PriceCheckerHorizontalResizeEventArgs : EventArgs
{
    public PriceCheckerHorizontalResizeEventArgs(double horizontalChange)
        : this(horizontalChange, horizontalChange)
    {
    }

    public PriceCheckerHorizontalResizeEventArgs(
        double horizontalChange,
        double cursorScreenX)
    {
        HorizontalChange = horizontalChange;
        CursorScreenX = cursorScreenX;
    }

    public double HorizontalChange { get; }

    public double CursorScreenX { get; }
}
