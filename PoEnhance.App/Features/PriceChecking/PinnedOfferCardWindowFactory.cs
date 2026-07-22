namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PinnedOfferCardWindowFactory : IPinnedOfferCardWindowFactory
{
    public IPinnedOfferCardWindow CreateWindow()
    {
        return new ItemCardPreviewWindow(OfferCardWindowMode.Pinned);
    }
}
