namespace PoEnhance.App.Features.PriceChecking;

internal interface IOfferCardPreviewWindow
{
    event EventHandler? CloseRequested;

    bool IsClosed { get; }

    OfferCardSnapshot? CurrentSnapshot { get; }

    OfferCardPreviewSize UpdateContent(OfferCardSnapshot snapshot, double maximumHeight);

    void ApplyPlacement(PriceCheckerPlacement placement);

    void ShowInactive();

    void HideAndClear();

    void Close();
}
