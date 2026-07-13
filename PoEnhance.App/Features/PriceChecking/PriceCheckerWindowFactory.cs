namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerWindowFactory : IPriceCheckerWindowFactory
{
    public IPriceCheckerWindow CreateWindow()
    {
        return new PriceCheckerWindow();
    }
}
