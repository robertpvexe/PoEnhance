namespace PoEnhance.App.Features.PriceChecking;

internal sealed class OfferCardPreviewWindowFactory : IOfferCardPreviewWindowFactory
{
    public IOfferCardPreviewWindow CreateWindow()
    {
        return new ItemCardPreviewWindow();
    }
}
