using PoEnhance.App.Features.PriceChecking;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerDeveloperDiagnosticsTests
{
    [Fact]
    public void DiagnosticsXaml_DefinesASeparateNonActivatingTopLevelToolWindow()
    {
        var xaml = LoadRepositoryFile(
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "PriceCheckerDeveloperDiagnosticsWindow.xaml");
        var priceCheckerXaml = LoadRepositoryFile(
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "PriceCheckerWindow.xaml");
        var presenterCode = LoadRepositoryFile(
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "PriceCheckerDeveloperDiagnosticsPresenter.cs");

        Assert.StartsWith("<Window", xaml.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("ShowActivated=\"False\"", xaml);
        Assert.Contains("ShowInTaskbar=\"False\"", xaml);
        Assert.Contains("Topmost=\"True\"", xaml);
        Assert.Contains("ResizeMode=\"NoResize\"", xaml);
        Assert.Contains("WindowStyle=\"None\"", xaml);
        Assert.Contains("Focusable=\"False\"", xaml);
        Assert.DoesNotContain("PriceCheckerDeveloperDiagnosticsWindow", priceCheckerXaml);
        Assert.DoesNotContain("Debug", priceCheckerXaml);
        Assert.DoesNotContain("Validation", priceCheckerXaml);
        Assert.Contains("#if DEBUG", presenterCode);
        Assert.Contains("#else", presenterCode);
        Assert.Contains("return null;", presenterCode);
    }

    [Fact]
    public void Placement_TargetsTopRightOfPathOfExileMonitorWorkingArea()
    {
        var placement = PriceCheckerDeveloperDiagnosticsPresenter.CalculatePlacement(
            new PriceCheckerMonitorWorkingArea(1920d, 40d, 2560d, 1400d),
            windowWidth: 320d,
            margin: 12d);

        Assert.Equal(4148d, placement.Left);
        Assert.Equal(52d, placement.Top);
    }

    [Fact]
    public void Presenter_UpdatesAndShowsInactiveWithoutActivatingTheWindow()
    {
        var workingArea = new FakeWorkingAreaProvider(
            new PriceCheckerMonitorWorkingArea(0d, 0d, 1920d, 1040d));
        var window = new FakeDiagnosticsWindow();
        var presenter = new PriceCheckerDeveloperDiagnosticsPresenter(
            workingArea,
            new FakeWindowFactory(window));
        var snapshot = new PriceCheckerDeveloperDiagnosticsSnapshot(
            "Ready",
            [new PriceCheckerDeveloperDiagnostic("TEST_CODE", "Test diagnostic")]);

        presenter.ShowOrUpdate(snapshot, ClientBounds());
        presenter.ShowOrUpdate(snapshot with { State = "Complete" }, ClientBounds());

        Assert.Equal(1, window.CreatedCount);
        Assert.Equal(2, window.UpdateCount);
        Assert.Equal(2, window.ShowInactiveCount);
        Assert.Equal(0, window.ActivateCount);
        Assert.Equal(1588d, window.Placement?.Left);
        Assert.Equal(12d, window.Placement?.Top);
    }

    private static PathOfExileClientBounds ClientBounds() =>
        new(100d, 100d, 1200d, 800d, "DISPLAY1", 1d, 1d);

    private static string LoadRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory!.FullName, .. parts]));
    }

    private sealed class FakeWorkingAreaProvider(PriceCheckerMonitorWorkingArea workingArea) :
        IPathOfExileMonitorWorkingAreaProvider
    {
        public bool TryGetWorkingArea(
            PathOfExileClientBounds clientBounds,
            out PriceCheckerMonitorWorkingArea result)
        {
            result = workingArea;
            return true;
        }
    }

    private sealed class FakeWindowFactory(FakeDiagnosticsWindow window) :
        IPriceCheckerDeveloperDiagnosticsWindowFactory
    {
        public IPriceCheckerDeveloperDiagnosticsWindow CreateWindow()
        {
            window.CreatedCount++;
            return window;
        }
    }

    private sealed class FakeDiagnosticsWindow : IPriceCheckerDeveloperDiagnosticsWindow
    {
        public double Width => 320d;
        public double Height => 72d;
        public bool IsClosed { get; private set; }
        public int CreatedCount { get; set; }
        public int UpdateCount { get; private set; }
        public int ShowInactiveCount { get; private set; }
        public int ActivateCount { get; private set; }
        public PriceCheckerDeveloperDiagnosticsPlacement? Placement { get; private set; }

        public void UpdateContent(PriceCheckerDeveloperDiagnosticsSnapshot snapshot) => UpdateCount++;

        public void ApplyPlacement(PriceCheckerDeveloperDiagnosticsPlacement placement) =>
            Placement = placement;

        public void ShowInactive() => ShowInactiveCount++;

        public void Close() => IsClosed = true;
    }
}
