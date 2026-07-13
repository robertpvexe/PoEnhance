namespace PoEnhance.App.Features.PriceChecking;

public sealed record PriceCheckerPlacement(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
}
