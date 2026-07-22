namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PinnedOfferCardSessionController : IDisposable
{
    public const int MaximumPinnedCards = 4;
    public const string MaximumPinnedCardsFeedback =
        "Maximum of 4 pinned item cards reached.";

    private readonly IPinnedOfferCardWindowFactory windowFactory;
    private readonly PinnedOfferCardPlacementCalculator placementCalculator;
    private readonly List<PinnedCard> cards = [];
    private PathOfExileClientBounds? currentClientBounds;
    private bool isGameOverlayContextActive;
    private bool isDisposed;

    public PinnedOfferCardSessionController(
        IPinnedOfferCardWindowFactory windowFactory,
        PinnedOfferCardPlacementCalculator placementCalculator)
    {
        this.windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
        this.placementCalculator = placementCalculator ??
            throw new ArgumentNullException(nameof(placementCalculator));
    }

    internal int Count => cards.Count;

    internal IReadOnlyList<OfferCardSnapshot> Snapshots =>
        cards.Select(card => card.Snapshot).ToArray();

    internal PathOfExileClientBounds? CurrentClientBounds => currentClientBounds;

    public event EventHandler<PinnedOfferCardUnpinnedEventArgs>? Unpinned;

    public PinnedOfferCardPinResult TryPin(
        OfferCardSnapshot snapshot,
        PriceCheckerPlacement previewPlacement,
        PathOfExileClientBounds clientBounds)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(previewPlacement);
        ArgumentNullException.ThrowIfNull(clientBounds);

        var existingCard = cards.FirstOrDefault(card =>
            string.Equals(card.Snapshot.OfferId, snapshot.OfferId, StringComparison.Ordinal));
        if (existingCard is not null)
        {
            if (isGameOverlayContextActive)
            {
                existingCard.Window.ShowInactive();
            }

            return PinnedOfferCardPinResult.AlreadyPinned();
        }

        if (cards.Count >= MaximumPinnedCards)
        {
            return PinnedOfferCardPinResult.Failure(MaximumPinnedCardsFeedback);
        }

        currentClientBounds = clientBounds;
        var pinnedWindow = windowFactory.CreateWindow();
        var size = pinnedWindow.UpdateContent(
            snapshot,
            clientBounds.Width,
            clientBounds.Height);
        var requestedPlacement = previewPlacement with
        {
            Width = IsPositiveFinite(previewPlacement.Width)
                ? previewPlacement.Width
                : size.Width,
            Height = IsPositiveFinite(previewPlacement.Height)
                ? previewPlacement.Height
                : size.Height,
        };
        var placement = placementCalculator.PlaceNew(
            requestedPlacement,
            cards.Select(card => card.Placement).ToArray(),
            clientBounds);
        var card = new PinnedCard(snapshot, pinnedWindow, placement);
        cards.Add(card);
        pinnedWindow.CloseRequested += OnWindowCloseRequested;
        pinnedWindow.UnpinRequested += OnWindowUnpinRequested;
        pinnedWindow.DragDelta += OnWindowDragDelta;
        pinnedWindow.ApplyPlacement(placement);
        if (isGameOverlayContextActive)
        {
            pinnedWindow.ShowInactive();
        }

        return PinnedOfferCardPinResult.Success();
    }

    public void UpdateGameOverlayContext(
        bool isActive,
        PathOfExileClientBounds? clientBounds)
    {
        if (isDisposed)
        {
            return;
        }

        isGameOverlayContextActive = isActive;
        if (clientBounds is { IsUsable: true })
        {
            currentClientBounds = clientBounds;
            ClampAll(clientBounds);
        }

        // Pinned offer cards intentionally remain visible across foreground changes,
        // matching the pinned Price Checker lifetime. This method still tracks the
        // latest usable client bounds for dragging and placement clamping.
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        foreach (var card in cards.ToArray())
        {
            DetachAndClose(card);
        }

        cards.Clear();
        currentClientBounds = null;
    }

    private void ClampAll(PathOfExileClientBounds clientBounds)
    {
        foreach (var card in cards)
        {
            var clamped = placementCalculator.Clamp(card.Placement, clientBounds);
            if (clamped == card.Placement)
            {
                continue;
            }

            card.Placement = clamped;
            card.Window.ApplyPlacement(clamped);
        }
    }

    private void OnWindowCloseRequested(object? sender, EventArgs e)
    {
        var card = cards.FirstOrDefault(candidate => ReferenceEquals(candidate.Window, sender));
        if (card is null)
        {
            return;
        }

        cards.Remove(card);
        DetachAndClose(card);
    }

    private void OnWindowDragDelta(object? sender, OfferCardDragDeltaEventArgs e)
    {
        if (currentClientBounds is null)
        {
            return;
        }

        var card = cards.FirstOrDefault(candidate => ReferenceEquals(candidate.Window, sender));
        if (card is null)
        {
            return;
        }

        card.Placement = placementCalculator.ApplyDrag(
            card.Placement,
            e.HorizontalChange,
            e.VerticalChange,
            currentClientBounds);
        card.Window.ApplyPlacement(card.Placement);
    }

    private void OnWindowUnpinRequested(object? sender, EventArgs e)
    {
        var card = cards.FirstOrDefault(candidate => ReferenceEquals(candidate.Window, sender));
        if (card is null)
        {
            return;
        }

        cards.Remove(card);
        DetachAndClose(card);
        Unpinned?.Invoke(
            this,
            new PinnedOfferCardUnpinnedEventArgs(card.Snapshot, card.Placement));
    }

    private void DetachAndClose(PinnedCard card)
    {
        card.Window.CloseRequested -= OnWindowCloseRequested;
        card.Window.UnpinRequested -= OnWindowUnpinRequested;
        card.Window.DragDelta -= OnWindowDragDelta;
        card.Window.Close();
    }

    private static bool IsPositiveFinite(double value) =>
        double.IsFinite(value) && value > 0d;

    private sealed class PinnedCard
    {
        public PinnedCard(
            OfferCardSnapshot snapshot,
            IPinnedOfferCardWindow window,
            PriceCheckerPlacement placement)
        {
            Snapshot = snapshot;
            Window = window;
            Placement = placement;
        }

        public OfferCardSnapshot Snapshot { get; }

        public IPinnedOfferCardWindow Window { get; }

        public PriceCheckerPlacement Placement { get; set; }
    }
}
