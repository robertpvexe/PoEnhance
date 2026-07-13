using System.Globalization;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed record PriceCheckerPlacementKey(
    string DisplayDeviceName,
    int ClientWidth,
    int ClientHeight,
    int DpiScaleXPermille,
    int DpiScaleYPermille)
{
    public static PriceCheckerPlacementKey FromClientBounds(PathOfExileClientBounds clientBounds)
    {
        return new PriceCheckerPlacementKey(
            string.IsNullOrWhiteSpace(clientBounds.DisplayDeviceName)
                ? "unknown-monitor"
                : clientBounds.DisplayDeviceName,
            (int)Math.Round(clientBounds.Width, MidpointRounding.AwayFromZero),
            (int)Math.Round(clientBounds.Height, MidpointRounding.AwayFromZero),
            ToPermille(clientBounds.DpiScaleX),
            ToPermille(clientBounds.DpiScaleY));
    }

    public string ToStorageKey()
    {
        return string.Join(
            "|",
            $"display={DisplayDeviceName}",
            $"client={ClientWidth}x{ClientHeight}",
            $"dpi={DpiScaleXPermille}x{DpiScaleYPermille}");
    }

    private static int ToPermille(double value)
    {
        return (int)Math.Round(value * 1000d, MidpointRounding.AwayFromZero);
    }
}
