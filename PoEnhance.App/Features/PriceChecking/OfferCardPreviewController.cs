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

    public event EventHandler<OfferCardPinRequestedEventArgs>? PinRequested;

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
        previewWindow.SetPinFeedback(null);
        var size = previewWindow.UpdateContent(snapshot, clientBounds.Height);
        var placement = placementCalculator.Calculate(
            clientBounds,
            priceCheckerBounds,
            size);
        previewWindow.ApplyPlacement(placement);
        previewWindow.ShowInactive();
    }

    public void ShowAt(
        OfferCardSnapshot snapshot,
        PriceCheckerPlacement placement,
        PathOfExileClientBounds clientBounds)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(clientBounds);

        var previewWindow = EnsureWindow();
        previewWindow.SetPinFeedback(null);
        var measuredSize = previewWindow.UpdateContent(snapshot, clientBounds.Height);
        var requestedPlacement = IsUsable(placement)
            ? placement
            : new PriceCheckerPlacement(
                clientBounds.Left,
                clientBounds.Top,
                measuredSize.Width,
                measuredSize.Height);
        previewWindow.ApplyPlacement(
            placementCalculator.Clamp(requestedPlacement, clientBounds));
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

    public void SetPinFeedback(string? message)
    {
        if (!isDisposed)
        {
            window?.SetPinFeedback(message);
        }
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
            window.PinRequested -= OnPinRequested;
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
            window.PinRequested -= OnPinRequested;
        }

        window = windowFactory.CreateWindow();
        window.CloseRequested += OnCloseRequested;
        window.PinRequested += OnPinRequested;
        return window;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Clear();
    }

    private void OnPinRequested(object? sender, EventArgs e)
    {
        var previewWindow = window;
        if (previewWindow is null ||
            !ReferenceEquals(sender, previewWindow) ||
            previewWindow.CurrentSnapshot is not { } snapshot ||
            previewWindow.CurrentPlacement is not { } placement)
        {
            return;
        }

        PinRequested?.Invoke(
            this,
            new OfferCardPinRequestedEventArgs(snapshot, placement));
    }

    private static bool IsUsable(PriceCheckerPlacement placement) =>
        double.IsFinite(placement.Width) &&
        double.IsFinite(placement.Height) &&
        placement.Width > 0d &&
        placement.Height > 0d;
}
