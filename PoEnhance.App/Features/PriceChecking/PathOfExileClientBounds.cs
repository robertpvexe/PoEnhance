namespace PoEnhance.App.Features.PriceChecking;

internal sealed record PathOfExileClientBounds(
    double Left,
    double Top,
    double Width,
    double Height,
    string DisplayDeviceName,
    double DpiScaleX,
    double DpiScaleY)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;

    public bool IsUsable => Width > 0 && Height > 0;
}
