namespace PoEnhance.App.Features.PriceChecking;

internal sealed record PriceCheckerNativeRectangle(
    double Left,
    double Top,
    double Width,
    double Height)
{
    public double Right => Left + Width;
}
