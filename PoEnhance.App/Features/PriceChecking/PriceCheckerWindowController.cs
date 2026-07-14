using PoEnhance.App.Infrastructure.PathOfExile;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using Serilog;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerWindowController
{
    private readonly IPathOfExileClientBoundsProvider clientBoundsProvider;
    private readonly PriceCheckerPlacementCalculator placementCalculator;
    private readonly PriceCheckerPlacementStore placementStore;
    private readonly IPriceCheckerWindowFactory windowFactory;
    private readonly ITradeSearchDraftMapper draftMapper;
    private readonly ITradeSearchDraftValidator draftValidator;
    private readonly IPathOfExileForegroundWindowDetector foregroundWindowDetector;
    private readonly IPriceCheckerDeferredActionScheduler deferredActionScheduler;
    private readonly PriceCheckerSearchController searchController;
    private IPriceCheckerWindow? window;
    private PathOfExileClientBounds? currentClientBounds;
    private PriceCheckerPlacementKey? currentPlacementKey;
    private PriceCheckerPlacement? currentPlacement;
    private double currentHorizontalCorrection;
    private double? currentPanelWidth;
    private HorizontalResizeSession? horizontalResizeSession;
    private bool isAutoCloseArmed;
    private bool isPinned;

    public PriceCheckerWindowController(
        IPriceCheckerWindowFactory windowFactory,
        IPathOfExileTradePriceCheckService priceCheckService)
        : this(
            new PathOfExileClientBoundsProvider(),
            new PriceCheckerPlacementCalculator(),
            new PriceCheckerPlacementStore(
                new PriceCheckerPlacementStorePathResolver().ResolveDefaultPath()),
            windowFactory,
            new CoreTradeSearchDraftMapperAdapter(),
            new CoreTradeSearchDraftValidatorAdapter(),
            new PathOfExileForegroundWindowDetector(),
            new WpfPriceCheckerDeferredActionScheduler(),
            new PriceCheckerSearchController(priceCheckService))
    {
    }

    internal PriceCheckerWindowController(
        IPathOfExileClientBoundsProvider clientBoundsProvider,
        PriceCheckerPlacementCalculator placementCalculator,
        PriceCheckerPlacementStore placementStore,
        IPriceCheckerWindowFactory windowFactory,
        ITradeSearchDraftMapper draftMapper,
        ITradeSearchDraftValidator draftValidator,
        IPathOfExileForegroundWindowDetector foregroundWindowDetector,
        IPriceCheckerDeferredActionScheduler deferredActionScheduler,
        PriceCheckerSearchController searchController)
    {
        this.clientBoundsProvider = clientBoundsProvider;
        this.placementCalculator = placementCalculator;
        this.placementStore = placementStore;
        this.windowFactory = windowFactory;
        this.draftMapper = draftMapper;
        this.draftValidator = draftValidator;
        this.foregroundWindowDetector = foregroundWindowDetector;
        this.deferredActionScheduler = deferredActionScheduler;
        this.searchController = searchController;
    }

    public PriceCheckerWindowUpdateResult ShowOrUpdate(
        ParsedItem parsedItem,
        ItemBaseResolutionResult? itemBaseResolution,
        IReadOnlyList<ModifierCandidateResolutionResult> modifierResolutions)
    {
        var draftResult = draftMapper.CreateDraft(
            parsedItem,
            itemBaseResolution,
            modifierResolutions);

        if (!draftResult.IsSuccess || draftResult.Draft is null)
        {
            var diagnostic = draftResult.Diagnostics.FirstOrDefault();
            return PriceCheckerWindowUpdateResult.Failure(
                diagnostic is null
                    ? "Trade draft could not be created"
                    : $"{diagnostic.Code}: {diagnostic.Message}");
        }

        if (!clientBoundsProvider.TryGetClientBounds(out var clientBounds))
        {
            return PriceCheckerWindowUpdateResult.Failure(
                "Path of Exile client bounds are unavailable");
        }

        var validationResult = draftValidator.Validate(draftResult.Draft);
        var priceCheckerWindow = EnsureWindow();
        priceCheckerWindow.UpdateContent(new PriceCheckerWindowState(
            draftResult.Draft,
            validationResult));
        searchController.UpdateCurrentDraft(draftResult.Draft, validationResult);

        var placementKey = PriceCheckerPlacementKey.FromClientBounds(clientBounds);
        if (currentPlacementKey != placementKey)
        {
            currentHorizontalCorrection = placementStore.LoadHorizontalCorrection(placementKey);
            currentPanelWidth = placementStore.LoadPanelWidth(placementKey);
        }

        currentClientBounds = clientBounds;
        currentPlacementKey = placementKey;
        var requestedPanelWidth = currentPanelWidth ?? placementCalculator.CalculatePanelWidth(clientBounds);
        currentPlacement = placementCalculator.CalculatePlacement(
            clientBounds,
            currentHorizontalCorrection,
            requestedPanelWidth);
        currentPanelWidth = currentPlacement.Width;
        var automaticLeft = placementCalculator.CalculateAutomaticLeft(
            clientBounds,
            currentPlacement.Width);
        priceCheckerWindow.ApplyPlacement(currentPlacement);
        Log.Debug(
            "Price Checker placement applied. ContextKey={ContextKey}; Client=({ClientLeft}, {ClientTop}, {ClientWidth}, {ClientHeight}); Dpi=({DpiScaleX}, {DpiScaleY}); PanelWidth={PanelWidth}; AutomaticX={AutomaticX}; Correction={Correction}; FinalX={FinalX}",
            placementKey.ToStorageKey(),
            clientBounds.Left,
            clientBounds.Top,
            clientBounds.Width,
            clientBounds.Height,
            clientBounds.DpiScaleX,
            clientBounds.DpiScaleY,
            currentPlacement.Width,
            automaticLeft,
            currentHorizontalCorrection,
            currentPlacement.Left);
        priceCheckerWindow.ShowInactive();

        return PriceCheckerWindowUpdateResult.Success();
    }

    private IPriceCheckerWindow EnsureWindow()
    {
        if (window is { IsClosed: false })
        {
            return window;
        }

        window = windowFactory.CreateWindow();
        window.Closed += OnWindowClosed;
        window.PanelActivated += OnWindowPanelActivated;
        window.PanelDeactivated += OnWindowPanelDeactivated;
        window.PanelInteraction += OnWindowPanelInteraction;
        window.PinStateChanged += OnWindowPinStateChanged;
        window.HorizontalDragDelta += OnWindowHorizontalDragDelta;
        window.HorizontalDragCompleted += OnWindowHorizontalDragCompleted;
        window.HorizontalResizeStarted += OnWindowHorizontalResizeStarted;
        window.HorizontalResizeDelta += OnWindowHorizontalResizeDelta;
        window.HorizontalResizeCompleted += OnWindowHorizontalResizeCompleted;
        window.ResetPositionRequested += OnWindowResetPositionRequested;
        searchController.AttachWindow(window);
        isAutoCloseArmed = false;
        isPinned = window.IsPinned;
        return window;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (window is null)
        {
            return;
        }

        var closedWindow = window;
        searchController.DetachWindow(closedWindow);
        closedWindow.Closed -= OnWindowClosed;
        closedWindow.PanelActivated -= OnWindowPanelActivated;
        closedWindow.PanelDeactivated -= OnWindowPanelDeactivated;
        closedWindow.PanelInteraction -= OnWindowPanelInteraction;
        closedWindow.PinStateChanged -= OnWindowPinStateChanged;
        closedWindow.HorizontalDragDelta -= OnWindowHorizontalDragDelta;
        closedWindow.HorizontalDragCompleted -= OnWindowHorizontalDragCompleted;
        closedWindow.HorizontalResizeStarted -= OnWindowHorizontalResizeStarted;
        closedWindow.HorizontalResizeDelta -= OnWindowHorizontalResizeDelta;
        closedWindow.HorizontalResizeCompleted -= OnWindowHorizontalResizeCompleted;
        closedWindow.ResetPositionRequested -= OnWindowResetPositionRequested;
        window = null;
        currentClientBounds = null;
        currentPlacementKey = null;
        currentPlacement = null;
        currentHorizontalCorrection = 0d;
        currentPanelWidth = null;
        horizontalResizeSession = null;
        isAutoCloseArmed = false;
        isPinned = false;
    }

    private void OnWindowPanelActivated(object? sender, EventArgs e)
    {
        isAutoCloseArmed = true;
    }

    private void OnWindowPanelInteraction(object? sender, EventArgs e)
    {
        isAutoCloseArmed = true;
    }

    private void OnWindowPinStateChanged(object? sender, bool isPinned)
    {
        isAutoCloseArmed = true;
        this.isPinned = isPinned;
    }

    private void OnWindowPanelDeactivated(object? sender, EventArgs e)
    {
        if (window is null || !isAutoCloseArmed || isPinned)
        {
            return;
        }

        var deactivatedWindow = window;
        deferredActionScheduler.Schedule(() =>
            CloseIfReturnedToPathOfExile(deactivatedWindow));
    }

    private void CloseIfReturnedToPathOfExile(IPriceCheckerWindow deactivatedWindow)
    {
        if (!ReferenceEquals(window, deactivatedWindow) ||
            deactivatedWindow.IsClosed ||
            !isAutoCloseArmed ||
            isPinned ||
            !foregroundWindowDetector.IsPathOfExileForegroundWindow())
        {
            return;
        }

        deactivatedWindow.Close();
    }

    private void OnWindowHorizontalDragDelta(
        object? sender,
        PriceCheckerHorizontalDragEventArgs e)
    {
        if (window is null || currentClientBounds is null || currentPlacement is null)
        {
            return;
        }

        isAutoCloseArmed = true;
        currentPlacement = placementCalculator.ApplyHorizontalDrag(
            currentClientBounds,
            currentPlacement,
            e.HorizontalChange);
        currentPanelWidth = currentPlacement.Width;
        window.ApplyPlacement(currentPlacement);
    }

    private void OnWindowHorizontalDragCompleted(object? sender, EventArgs e)
    {
        if (currentClientBounds is null ||
            currentPlacementKey is null ||
            currentPlacement is null)
        {
            return;
        }

        var correction = placementCalculator.CalculateHorizontalCorrection(
            currentClientBounds,
            currentPlacement);
        currentHorizontalCorrection = correction;
        placementStore.SaveHorizontalCorrection(currentPlacementKey, correction);
        Log.Information(
            "Price Checker horizontal placement correction saved: {Correction}",
            correction);
    }

    private void OnWindowHorizontalResizeDelta(
        object? sender,
        PriceCheckerHorizontalResizeEventArgs e)
    {
        if (window is null || currentClientBounds is null || currentPlacement is null)
        {
            return;
        }

        isAutoCloseArmed = true;
        horizontalResizeSession ??= CreateHorizontalResizeSession();
        if (horizontalResizeSession is null)
        {
            return;
        }

        horizontalResizeSession = horizontalResizeSession with
        {
            AccumulatedHorizontalChange =
                horizontalResizeSession.AccumulatedHorizontalChange + e.HorizontalChange,
        };

        var requestedLeft = horizontalResizeSession.StartingLeft +
            horizontalResizeSession.AccumulatedHorizontalChange;
        var requestedWidth = horizontalResizeSession.FixedRight - requestedLeft;
        var newWidth = Math.Clamp(
            requestedWidth,
            horizontalResizeSession.MinimumWidth,
            horizontalResizeSession.MaximumWidth);
        var newLeft = horizontalResizeSession.FixedRight - newWidth;
        currentPlacement = new PriceCheckerPlacement(
            newLeft,
            horizontalResizeSession.ClientBounds.Top,
            newWidth,
            horizontalResizeSession.ClientBounds.Height);
        currentPanelWidth = currentPlacement.Width;
        window.ApplyPlacement(currentPlacement);
    }

    private void OnWindowHorizontalResizeCompleted(object? sender, EventArgs e)
    {
        if (currentClientBounds is null ||
            currentPlacementKey is null ||
            currentPlacement is null)
        {
            horizontalResizeSession = null;
            return;
        }

        currentPanelWidth = currentPlacement.Width;
        placementStore.SavePanelWidth(currentPlacementKey, currentPlacement.Width);
        Log.Information(
            "Price Checker horizontal width saved: {PanelWidth}; correction preserved: {Correction}",
            currentPlacement.Width,
            currentHorizontalCorrection);
        horizontalResizeSession = null;
    }

    private void OnWindowHorizontalResizeStarted(object? sender, EventArgs e)
    {
        isAutoCloseArmed = true;
        horizontalResizeSession = CreateHorizontalResizeSession();
    }

    private void OnWindowResetPositionRequested(object? sender, EventArgs e)
    {
        if (window is null ||
            currentClientBounds is null ||
            currentPlacementKey is null)
        {
            return;
        }

        isAutoCloseArmed = true;
        placementStore.ResetHorizontalCorrection(currentPlacementKey);
        currentHorizontalCorrection = 0d;
        var requestedPanelWidth = currentPanelWidth ?? placementCalculator.CalculatePanelWidth(currentClientBounds);
        currentPlacement = placementCalculator.CalculatePlacement(
            currentClientBounds,
            currentHorizontalCorrection,
            requestedPanelWidth);
        currentPanelWidth = currentPlacement.Width;
        window.ApplyPlacement(currentPlacement);
    }

    private HorizontalResizeSession? CreateHorizontalResizeSession()
    {
        if (window is null || currentClientBounds is null)
        {
            return null;
        }

        var displayedPlacement = window.GetDisplayedPlacement() ?? currentPlacement;
        if (displayedPlacement is null ||
            !double.IsFinite(displayedPlacement.Left) ||
            !double.IsFinite(displayedPlacement.Width) ||
            displayedPlacement.Width <= 0d)
        {
            return null;
        }

        var fixedRight = displayedPlacement.Left + displayedPlacement.Width;
        var maximumWidth = placementCalculator.CalculateMaximumUserPanelWidth(
            currentClientBounds,
            fixedRight);
        var minimumWidth = Math.Min(
            PriceCheckerPlacementCalculator.UserPanelMinimumWidth,
            maximumWidth);

        Log.Debug(
            "Price Checker horizontal resize started. StartLeft={StartLeft}; StartWidth={StartWidth}; FixedRight={FixedRight}; MinWidth={MinWidth}; MaxWidth={MaxWidth}; Dpi=({DpiScaleX}, {DpiScaleY})",
            displayedPlacement.Left,
            displayedPlacement.Width,
            fixedRight,
            minimumWidth,
            maximumWidth,
            currentClientBounds.DpiScaleX,
            currentClientBounds.DpiScaleY);

        currentPlacement = displayedPlacement;
        currentPanelWidth = displayedPlacement.Width;

        return new HorizontalResizeSession(
            displayedPlacement.Left,
            displayedPlacement.Width,
            fixedRight,
            currentClientBounds,
            minimumWidth,
            maximumWidth,
            AccumulatedHorizontalChange: 0d);
    }

    private sealed record HorizontalResizeSession(
        double StartingLeft,
        double StartingWidth,
        double FixedRight,
        PathOfExileClientBounds ClientBounds,
        double MinimumWidth,
        double MaximumWidth,
        double AccumulatedHorizontalChange);
}
