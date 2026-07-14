namespace PoEnhance.App.Features.PriceChecking;

internal interface IPriceCheckerNativeResizeWindow
{
    bool TryGetNativeBounds(out PriceCheckerNativeRectangle bounds);

    bool TryGetCursorScreenX(out double screenX);

    bool TrySetNativeBounds(PriceCheckerNativeRectangle bounds);
}
