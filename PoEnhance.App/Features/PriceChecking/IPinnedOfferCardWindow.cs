namespace PoEnhance.App.Features.PriceChecking;

internal interface IPinnedOfferCardWindow
{
    event EventHandler? CloseRequested;

    event EventHandler? UnpinRequested;

    event EventHandler<OfferCardDragDeltaEventArgs>? DragDelta;

    bool IsClosed { get; }

    OfferCardSnapshot? CurrentSnapshot { get; }

    PriceCheckerPlacement? CurrentPlacement { get; }

    OfferCardPreviewSize UpdateContent(
        OfferCardSnapshot snapshot,
        double maximumWidth,
        double maximumHeight);

    void ApplyPlacement(PriceCheckerPlacement placement);

    void ShowInactive();

    void Close();
}
