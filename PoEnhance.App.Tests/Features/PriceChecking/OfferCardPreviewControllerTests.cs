using PoEnhance.App.Features.PriceChecking;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class OfferCardPreviewControllerTests
{
    [Fact]
    public void Construction_DoesNotCreateOrShowPreview()
    {
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);

        Assert.Empty(factory.Windows);
    }

    [Fact]
    public void Show_UsesExactSnapshotAndSecondOrRepeatedClickReusesOneWindow()
    {
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);
        var first = Snapshot("first");
        var second = Snapshot("second");

        controller.Show(first, PriceCheckerBounds(), ClientBounds());
        controller.Show(second, PriceCheckerBounds(), ClientBounds());
        controller.Show(second, PriceCheckerBounds(), ClientBounds());

        var window = Assert.Single(factory.Windows);
        Assert.Same(second, window.CurrentSnapshot);
        Assert.Same(second, controller.CurrentSnapshot);
        Assert.Equal(3, window.UpdateCount);
        Assert.Equal(3, window.ShowCount);
        Assert.Equal(3, window.Placements.Count);
        Assert.False(window.IsClosed);
    }

    [Fact]
    public void CloseRequest_HidesAndClearsOnlyPreviewThenAllowsSameWindowToBeReused()
    {
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);
        controller.Show(Snapshot("first"), PriceCheckerBounds(), ClientBounds());
        var window = Assert.Single(factory.Windows);

        window.RaiseCloseRequested();

        Assert.Equal(1, window.HideCount);
        Assert.Null(window.CurrentSnapshot);
        Assert.False(window.IsClosed);

        controller.Show(Snapshot("second"), PriceCheckerBounds(), ClientBounds());

        Assert.Single(factory.Windows);
        Assert.Equal("second", window.CurrentSnapshot?.OfferId);
    }

    [Fact]
    public void Clear_HidesPreviewAndDisposeClosesReusableWindow()
    {
        var factory = new FakeWindowFactory();
        var controller = CreateController(factory);
        controller.Show(Snapshot("first"), PriceCheckerBounds(), ClientBounds());
        var window = Assert.Single(factory.Windows);

        controller.Clear();
        controller.Dispose();

        Assert.Equal(1, window.HideCount);
        Assert.Null(window.CurrentSnapshot);
        Assert.True(window.IsClosed);
        Assert.Equal(1, window.CloseCount);
    }

    [Fact]
    public void PinRequest_ExposesExactSnapshotAndCurrentPlacementWithoutClearingPreview()
    {
        var factory = new FakeWindowFactory();
        using var controller = CreateController(factory);
        var snapshot = Snapshot("pin");
        OfferCardPinRequestedEventArgs? request = null;
        controller.PinRequested += (_, e) => request = e;
        controller.Show(snapshot, PriceCheckerBounds(), ClientBounds());
        var window = Assert.Single(factory.Windows);

        window.RaisePinRequested();

        Assert.NotNull(request);
        Assert.Same(snapshot, request.Snapshot);
        Assert.Equal(window.CurrentPlacement, request.Placement);
        Assert.Same(snapshot, window.CurrentSnapshot);
        Assert.Equal(0, window.HideCount);
    }

    private static OfferCardPreviewController CreateController(FakeWindowFactory factory)
    {
        return new OfferCardPreviewController(
            factory,
            new OfferCardPreviewPlacementCalculator());
    }

    private static OfferCardSnapshot Snapshot(string id) => new()
    {
        OfferId = id,
        Name = $"Item {id}",
    };

    private static PathOfExileClientBounds ClientBounds() => new(
        100,
        50,
        1200,
        800,
        @"\.\DISPLAY1",
        1,
        1);

    private static PriceCheckerPlacement PriceCheckerBounds() => new(800, 50, 300, 800);

    private sealed class FakeWindowFactory : IOfferCardPreviewWindowFactory
    {
        public List<FakeWindow> Windows { get; } = [];

        public IOfferCardPreviewWindow CreateWindow()
        {
            var window = new FakeWindow();
            Windows.Add(window);
            return window;
        }
    }

    private sealed class FakeWindow : IOfferCardPreviewWindow
    {
        public event EventHandler? CloseRequested;

        public event EventHandler? PinRequested;

        public bool IsClosed { get; private set; }

        public OfferCardSnapshot? CurrentSnapshot { get; private set; }

        public PriceCheckerPlacement? CurrentPlacement { get; private set; }

        public string? PinFeedback { get; private set; }

        public int UpdateCount { get; private set; }

        public int ShowCount { get; private set; }

        public int HideCount { get; private set; }

        public int CloseCount { get; private set; }

        public List<PriceCheckerPlacement> Placements { get; } = [];

        public OfferCardPreviewSize UpdateContent(OfferCardSnapshot snapshot, double maximumHeight)
        {
            CurrentSnapshot = snapshot;
            UpdateCount++;
            return new OfferCardPreviewSize(460, Math.Min(600, maximumHeight));
        }

        public void ApplyPlacement(PriceCheckerPlacement placement)
        {
            Placements.Add(placement);
            CurrentPlacement = placement;
        }

        public void ShowInactive()
        {
            ShowCount++;
        }

        public void HideAndClear()
        {
            CurrentSnapshot = null;
            CurrentPlacement = null;
            HideCount++;
        }

        public void SetPinFeedback(string? message)
        {
            PinFeedback = message;
        }

        public void Close()
        {
            CurrentSnapshot = null;
            IsClosed = true;
            CloseCount++;
        }

        public void RaiseCloseRequested()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaisePinRequested()
        {
            PinRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
