namespace PoEnhance.App.Features.PriceChecking;

internal interface IExternalUrlLauncher
{
    bool TryOpen(Uri uri);
}
