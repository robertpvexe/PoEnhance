using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.PathOfExile;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PinnedOfferCardSessionControllerTests
{
    [Fact]
    public void ConstructionCreatesNoWindowsAndDoesNotRestoreCards()
    {
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);

        Assert.Empty(factory.Windows);
        Assert.Equal(0, controller.Count);
        Assert.Empty(controller.Snapshots);
    }

    [Fact]
    public void TryPinCreatesIndependentWindowFromExactSnapshotAtPreviewPosition()
    {
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);
        var snapshot = Snapshot("one");
        var previewPlacement = Placement(left: 220, top: 90);
        controller.UpdateGameOverlayContext(true, Bounds());

        var result = controller.TryPin(snapshot, previewPlacement, Bounds());

        var window = Assert.Single(factory.Windows);
        Assert.True(result.IsSuccess);
        Assert.Same(snapshot, window.CurrentSnapshot);
        Assert.Same(snapshot, Assert.Single(controller.Snapshots));
        Assert.Equal(previewPlacement, window.CurrentPlacement);
        Assert.Equal(1, window.ShowCount);
        Assert.False(window.IsClosed);
    }

    [Fact]
    public void TryPinPreservesValidPreviewSizeInsteadOfUsingFreshWindowMeasurement()
    {
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);
        var previewPlacement = Placement(
            left: 220,
            top: 90,
            width: 430,
            height: 515);
        controller.UpdateGameOverlayContext(true, Bounds());

        controller.TryPin(Snapshot("one"), previewPlacement, Bounds());

        Assert.Equal(previewPlacement, Assert.Single(factory.Windows).CurrentPlacement);
    }

    [Fact]
    public void MultiplePinsUseDeterministicOffsetAndMaximumIsFour()
    {
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);
        controller.UpdateGameOverlayContext(true, Bounds());

        for (var index = 1; index <= 4; index++)
        {
            Assert.True(controller.TryPin(
                Snapshot(index.ToString()),
                Placement(left: 220, top: 90),
                Bounds()).IsSuccess);
        }

        Assert.Equal(4, controller.Count);
        Assert.Equal(4, factory.Windows.Count);
        Assert.Equal(220, factory.Windows[0].CurrentPlacement?.Left);
        Assert.Equal(244, factory.Windows[1].CurrentPlacement?.Left);
        Assert.Equal(268, factory.Windows[2].CurrentPlacement?.Left);
        Assert.Equal(292, factory.Windows[3].CurrentPlacement?.Left);

        var blocked = controller.TryPin(
            Snapshot("five"),
            Placement(left: 220, top: 90),
            Bounds());

        Assert.False(blocked.IsSuccess);
        Assert.Equal(PinnedOfferCardSessionController.MaximumPinnedCardsFeedback, blocked.Feedback);
        Assert.Equal(4, factory.Windows.Count);
        Assert.All(factory.Windows, window => Assert.False(window.IsClosed));
    }

    [Fact]
    public void ClosingOnePinnedCardClosesOnlyItAndImmediatelyFreesSlot()
    {
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);
        for (var index = 1; index <= 4; index++)
        {
            controller.TryPin(Snapshot(index.ToString()), Placement(), Bounds());
        }

        var closed = factory.Windows[1];
        closed.RaiseCloseRequested();

        Assert.True(closed.IsClosed);
        Assert.Equal(1, closed.CloseCount);
        Assert.Equal(3, controller.Count);
        Assert.All(factory.Windows.Where(window => !ReferenceEquals(window, closed)), window =>
            Assert.False(window.IsClosed));

        var replacement = controller.TryPin(Snapshot("replacement"), Placement(), Bounds());

        Assert.True(replacement.IsSuccess);
        Assert.Equal(4, controller.Count);
        Assert.Equal(5, factory.Windows.Count);
    }

    [Fact]
    public void UnpinClosesOnlyRequestedCardFreesSlotAndPublishesExactSnapshotAndPlacement()
    {
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);
        controller.UpdateGameOverlayContext(true, Bounds());
        var snapshot = Snapshot("one");
        var placement = Placement(left: 275, top: 145);
        PinnedOfferCardUnpinnedEventArgs? unpinned = null;
        controller.Unpinned += (_, e) => unpinned = e;

        controller.TryPin(snapshot, placement, Bounds());
        var window = Assert.Single(factory.Windows);
        window.RaiseUnpinRequested();

        Assert.True(window.IsClosed);
        Assert.Equal(1, window.CloseCount);
        Assert.Equal(0, controller.Count);
        Assert.NotNull(unpinned);
        Assert.Same(snapshot, unpinned.Snapshot);
        Assert.Equal(placement, unpinned.Placement);
        Assert.True(controller.TryPin(Snapshot("replacement"), Placement(), Bounds()).IsSuccess);
    }

    [Fact]
    public void TryPinSameOfferIdDoesNotConsumeAnotherSlotAndBringsExistingCardForward()
    {
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);
        controller.UpdateGameOverlayContext(true, Bounds());
        var first = Snapshot("listing-1");
        var duplicate = first with { Name = "A differently rendered copy" };

        controller.TryPin(first, Placement(), Bounds());
        var existingWindow = Assert.Single(factory.Windows);
        var result = controller.TryPin(duplicate, Placement(left: 300), Bounds());

        Assert.False(result.IsSuccess);
        Assert.True(result.IsAlreadyPinned);
        Assert.Equal(1, controller.Count);
        Assert.Single(factory.Windows);
        Assert.Same(first, existingWindow.CurrentSnapshot);
        Assert.Equal(2, existingWindow.ShowCount);

        Assert.True(controller.TryPin(
            duplicate with { OfferId = "listing-2" },
            Placement(left: 300),
            Bounds()).IsSuccess);
        Assert.Equal(2, controller.Count);
    }

    [Fact]
    public void ForegroundLossKeepsPinnedCardVisibleWithoutRecreatingOrRepositioningIt()
    {
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);
        controller.UpdateGameOverlayContext(true, Bounds());
        var snapshot = Snapshot("one");
        controller.TryPin(snapshot, Placement(), Bounds());
        var window = Assert.Single(factory.Windows);
        var placement = window.CurrentPlacement;
        var applyCount = window.ApplyCount;
        var showCount = window.ShowCount;

        controller.UpdateGameOverlayContext(false, Bounds());

        Assert.Equal(0, window.HideCount);
        Assert.False(window.IsClosed);
        Assert.Same(snapshot, window.CurrentSnapshot);
        Assert.Equal(placement, window.CurrentPlacement);
        Assert.Equal(applyCount, window.ApplyCount);
        Assert.Equal(showCount, window.ShowCount);
        Assert.Same(window, Assert.Single(factory.Windows));

        controller.UpdateGameOverlayContext(true, Bounds());

        Assert.Equal(0, window.HideCount);
        Assert.Equal(showCount, window.ShowCount);
        Assert.Same(snapshot, window.CurrentSnapshot);
        Assert.Equal(placement, window.CurrentPlacement);
        Assert.Equal(applyCount, window.ApplyCount);
    }

    [Fact]
    public void BrowserVisualStudioCodeAndDesktopForegroundKeepPinnedCardVisible()
    {
        var pathOfExile = new IntPtr(101);
        var priceChecker = new IntPtr(201);
        var preview = new IntPtr(202);
        var pinnedCard = new IntPtr(203);
        var browser = new IntPtr(301);
        var visualStudioCode = new IntPtr(302);
        var desktop = IntPtr.Zero;
        var foreground = pathOfExile;
        var registry = new PathOfExileOverlayWindowRegistry();
        registry.Register(priceChecker);
        registry.Register(preview);
        registry.Register(pinnedCard);
        var detector = new PathOfExileForegroundWindowDetector(
            registry,
            () => foreground,
            handle => handle == pathOfExile);
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);
        controller.UpdateGameOverlayContext(
            detector.IsPathOfExileOverlayContextActive(),
            Bounds());
        var snapshot = Snapshot("one");
        controller.TryPin(snapshot, Placement(), Bounds());
        var window = Assert.Single(factory.Windows);
        var placement = window.CurrentPlacement;
        var showCount = window.ShowCount;
        var applyCount = window.ApplyCount;

        foreach (var overlay in new[] { priceChecker, pinnedCard, preview, pathOfExile })
        {
            foreground = overlay;
            controller.UpdateGameOverlayContext(
                detector.IsPathOfExileOverlayContextActive(),
                clientBounds: null);
        }

        foreach (var otherApplication in new[] { browser, visualStudioCode, desktop })
        {
            foreground = otherApplication;
            Assert.False(detector.IsPathOfExileOverlayContextActive());
            controller.UpdateGameOverlayContext(
                detector.IsPathOfExileOverlayContextActive(),
                clientBounds: null);

            Assert.Equal(0, window.HideCount);
            Assert.Equal(showCount, window.ShowCount);
            Assert.False(window.IsClosed);
            Assert.Same(snapshot, window.CurrentSnapshot);
            Assert.Equal(placement, window.CurrentPlacement);
            Assert.Equal(applyCount, window.ApplyCount);
            Assert.Same(window, Assert.Single(factory.Windows));
        }

        foreground = pathOfExile;
        controller.UpdateGameOverlayContext(
            detector.IsPathOfExileOverlayContextActive(),
            clientBounds: null);

        Assert.Equal(0, window.HideCount);
        Assert.Equal(showCount, window.ShowCount);
        Assert.Same(snapshot, window.CurrentSnapshot);
        Assert.Equal(placement, window.CurrentPlacement);
        Assert.Equal(applyCount, window.ApplyCount);
    }

    [Fact]
    public void ActiveOverlayWithoutFreshBoundsDoesNotRecreateOrRepositionPinnedCard()
    {
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);
        controller.UpdateGameOverlayContext(true, Bounds());
        var snapshot = Snapshot("one");
        controller.TryPin(snapshot, Placement(), Bounds());
        var window = Assert.Single(factory.Windows);
        var placement = window.CurrentPlacement;
        var showCount = window.ShowCount;
        var applyCount = window.ApplyCount;

        controller.UpdateGameOverlayContext(true, clientBounds: null);

        Assert.Equal(0, window.HideCount);
        Assert.Equal(showCount, window.ShowCount);
        Assert.False(window.IsClosed);
        Assert.Same(snapshot, window.CurrentSnapshot);
        Assert.Equal(placement, window.CurrentPlacement);
        Assert.Equal(applyCount, window.ApplyCount);
    }

    [Fact]
    public void DragMovesPinnedCardOnlyAndClampsEveryPositionInsideClient()
    {
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);
        controller.TryPin(Snapshot("one"), Placement(left: 220, top: 90), Bounds());
        var window = Assert.Single(factory.Windows);

        window.RaiseDragDelta(horizontal: 50, vertical: 35);
        Assert.Equal(270, window.CurrentPlacement?.Left);
        Assert.Equal(125, window.CurrentPlacement?.Top);

        window.RaiseDragDelta(horizontal: 5000, vertical: 5000);

        var placement = Assert.IsType<PriceCheckerPlacement>(window.CurrentPlacement);
        Assert.Equal(Bounds().Right, placement.Right);
        Assert.Equal(Bounds().Bottom, placement.Top + placement.Height);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void DragUsesDipCoordinatesConsistentlyAcrossDpi(double dpiScale)
    {
        var bounds = Bounds(dpiScale: dpiScale);
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);
        controller.TryPin(Snapshot("one"), Placement(left: 220, top: 90), bounds);
        var window = Assert.Single(factory.Windows);

        window.RaiseDragDelta(horizontal: 30, vertical: 20);

        var placement = Assert.IsType<PriceCheckerPlacement>(window.CurrentPlacement);
        Assert.Equal(250, placement.Left);
        Assert.Equal(110, placement.Top);
        Assert.Equal(250 * dpiScale, placement.Left * bounds.DpiScaleX, precision: 6);
        Assert.Equal(110 * dpiScale, placement.Top * bounds.DpiScaleY, precision: 6);
    }

    [Fact]
    public void ChangedClientBoundsClampOnlyCardsThatNeedIt()
    {
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);
        var originalBounds = Bounds(width: 1400, height: 900);
        controller.TryPin(
            Snapshot("inside"),
            Placement(left: 150, top: 80, width: 300, height: 300),
            originalBounds);
        controller.TryPin(
            Snapshot("outside"),
            Placement(left: 1000, top: 550, width: 300, height: 300),
            originalBounds);
        var inside = factory.Windows[0];
        var outside = factory.Windows[1];
        var insideApplyCount = inside.ApplyCount;
        var smallerBounds = Bounds(width: 900, height: 650);

        controller.UpdateGameOverlayContext(false, smallerBounds);

        Assert.Equal(insideApplyCount, inside.ApplyCount);
        Assert.Equal(150, inside.CurrentPlacement?.Left);
        Assert.Equal(smallerBounds.Right, outside.CurrentPlacement?.Right);
        Assert.Equal(smallerBounds.Bottom, outside.CurrentPlacement?.Top + outside.CurrentPlacement?.Height);
    }

    [Fact]
    public void DisposeClosesEveryPinnedWindowExactlyOnce()
    {
        var factory = new FakeWindowFactory();
        var controller = CreateController(factory);
        controller.TryPin(Snapshot("one"), Placement(), Bounds());
        controller.TryPin(Snapshot("two"), Placement(), Bounds());

        controller.Dispose();
        controller.Dispose();

        Assert.Equal(2, factory.Windows.Count);
        Assert.All(factory.Windows, window =>
        {
            Assert.True(window.IsClosed);
            Assert.Equal(1, window.CloseCount);
        });
        Assert.Equal(0, controller.Count);
    }

    [Fact]
    public void SessionSourceHasNoTradeClipboardGameDataOrPersistenceDependency()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "PinnedOfferCardSessionController.cs"));

        Assert.DoesNotContain("Search", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fetch", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Clipboard", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GameData", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PriceCheckerOfferViewModel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Repository", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Store", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
    }

    private static PinnedOfferCardSessionController CreateController(FakeWindowFactory factory)
    {
        return new PinnedOfferCardSessionController(
            factory,
            new PinnedOfferCardPlacementCalculator());
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static OfferCardSnapshot Snapshot(string id) => new()
    {
        OfferId = id,
        Name = $"Item {id}",
    };

    private static PriceCheckerPlacement Placement(
        double left = 220,
        double top = 90,
        double width = 460,
        double height = 600) => new(left, top, width, height);

    private static PathOfExileClientBounds Bounds(
        double width = 1200,
        double height = 800,
        double dpiScale = 1) => new(
            Left: 100,
            Top: 50,
            Width: width,
            Height: height,
            DisplayDeviceName: @"\\.\DISPLAY1",
            DpiScaleX: dpiScale,
            DpiScaleY: dpiScale);

    private sealed class FakeWindowFactory : IPinnedOfferCardWindowFactory
    {
        public List<FakeWindow> Windows { get; } = [];

        public IPinnedOfferCardWindow CreateWindow()
        {
            var window = new FakeWindow();
            Windows.Add(window);
            return window;
        }
    }

    private sealed class FakeWindow : IPinnedOfferCardWindow
    {
        public event EventHandler? CloseRequested;

        public event EventHandler? UnpinRequested;

        public event EventHandler<OfferCardDragDeltaEventArgs>? DragDelta;

        public bool IsClosed { get; private set; }

        public OfferCardSnapshot? CurrentSnapshot { get; private set; }

        public PriceCheckerPlacement? CurrentPlacement { get; private set; }

        public int ApplyCount { get; private set; }

        public int ShowCount { get; private set; }

        public int HideCount { get; private set; }

        public int CloseCount { get; private set; }

        public OfferCardPreviewSize UpdateContent(OfferCardSnapshot snapshot, double maximumHeight)
        {
            CurrentSnapshot = snapshot;
            return new OfferCardPreviewSize(460, Math.Min(600, maximumHeight));
        }

        public void ApplyPlacement(PriceCheckerPlacement placement)
        {
            CurrentPlacement = placement;
            ApplyCount++;
        }

        public void ShowInactive()
        {
            ShowCount++;
        }

        public void Close()
        {
            if (IsClosed)
            {
                return;
            }

            IsClosed = true;
            CloseCount++;
        }

        public void RaiseCloseRequested()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseUnpinRequested()
        {
            UnpinRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseDragDelta(double horizontal, double vertical)
        {
            DragDelta?.Invoke(this, new OfferCardDragDeltaEventArgs(horizontal, vertical));
        }
    }
}
