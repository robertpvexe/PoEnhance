namespace PoEnhance.App.Features.PriceChecking;

internal interface IOfferCardPreviewWindow
{
    event EventHandler? CloseRequested;

    event EventHandler? PinRequested;

    bool IsClosed { get; }

    OfferCardSnapshot? CurrentSnapshot { get; }

    PriceCheckerPlacement? CurrentPlacement { get; }

    OfferCardPreviewSize UpdateContent(
        OfferCardSnapshot snapshot,
        double maximumWidth,
        double maximumHeight);

    void ApplyPlacement(PriceCheckerPlacement placement);

    void ShowInactive();

    void HideAndClear();

    void SetPinFeedback(string? message);

    void Close();
}
