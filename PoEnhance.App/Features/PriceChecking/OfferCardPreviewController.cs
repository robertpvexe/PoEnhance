namespace PoEnhance.App.Features.PriceChecking;

internal sealed class OfferCardPreviewController : IDisposable
{
    private readonly IOfferCardPreviewWindowFactory windowFactory;
    private readonly OfferCardPreviewPlacementCalculator placementCalculator;
    private IOfferCardPreviewWindow? window;
    private bool isDisposed;

    public OfferCardPreviewController(
        IOfferCardPreviewWindowFactory windowFactory,
        OfferCardPreviewPlacementCalculator placementCalculator)
    {
        this.windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
        this.placementCalculator = placementCalculator ??
            throw new ArgumentNullException(nameof(placementCalculator));
    }

    internal OfferCardSnapshot? CurrentSnapshot => window?.CurrentSnapshot;

    public void Show(
        OfferCardSnapshot snapshot,
        PriceCheckerPlacement priceCheckerBounds,
        PathOfExileClientBounds clientBounds)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(priceCheckerBounds);
        ArgumentNullException.ThrowIfNull(clientBounds);

        var previewWindow = EnsureWindow();
        var size = previewWindow.UpdateContent(snapshot, clientBounds.Height);
        var placement = placementCalculator.Calculate(
            clientBounds,
            priceCheckerBounds,
            size);
        previewWindow.ApplyPlacement(placement);
        previewWindow.ShowInactive();
    }

    public void Clear()
    {
        if (isDisposed)
        {
            return;
        }

        window?.HideAndClear();
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        if (window is not null)
        {
            window.CloseRequested -= OnCloseRequested;
            window.Close();
            window = null;
        }
    }

    private IOfferCardPreviewWindow EnsureWindow()
    {
        if (window is { IsClosed: false })
        {
            return window;
        }

        if (window is not null)
        {
            window.CloseRequested -= OnCloseRequested;
        }

        window = windowFactory.CreateWindow();
        window.CloseRequested += OnCloseRequested;
        return window;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Clear();
    }
}
