using PoEnhance.App.Infrastructure.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.PathOfExile;

public sealed class PathOfExileForegroundWindowDetectorTests
{
    [Fact]
    public void ActiveContextUsesExactGameAndRegisteredOverlayWindowIdentities()
    {
        var pathOfExile = new IntPtr(101);
        var priceChecker = new IntPtr(201);
        var preview = new IntPtr(202);
        var pinnedCard = new IntPtr(203);
        var browser = new IntPtr(301);
        var foreground = pathOfExile;
        var registry = new PathOfExileOverlayWindowRegistry();
        registry.Register(priceChecker);
        registry.Register(preview);
        registry.Register(pinnedCard);
        var detector = new PathOfExileForegroundWindowDetector(
            registry,
            () => foreground,
            handle => handle == pathOfExile);

        Assert.True(detector.IsPathOfExileForegroundWindow());
        Assert.True(detector.IsPathOfExileOverlayContextActive());

        foreground = priceChecker;
        Assert.False(detector.IsPathOfExileForegroundWindow());
        Assert.True(detector.IsPathOfExileOverlayContextActive());

        foreground = preview;
        Assert.True(detector.IsPathOfExileOverlayContextActive());

        foreground = pinnedCard;
        Assert.True(detector.IsPathOfExileOverlayContextActive());

        foreground = browser;
        Assert.False(detector.IsPathOfExileForegroundWindow());
        Assert.False(detector.IsPathOfExileOverlayContextActive());
    }

    [Fact]
    public void OverlayToGameAndOverlayToOverlayTransitionsStayActiveButUnregisteringEndsIdentity()
    {
        var pathOfExile = new IntPtr(101);
        var priceChecker = new IntPtr(201);
        var pinnedCard = new IntPtr(203);
        var foreground = priceChecker;
        var registry = new PathOfExileOverlayWindowRegistry();
        registry.Register(priceChecker);
        registry.Register(pinnedCard);
        var detector = new PathOfExileForegroundWindowDetector(
            registry,
            () => foreground,
            handle => handle == pathOfExile);

        Assert.True(detector.IsPathOfExileOverlayContextActive());
        foreground = pinnedCard;
        Assert.True(detector.IsPathOfExileOverlayContextActive());
        foreground = pathOfExile;
        Assert.True(detector.IsPathOfExileOverlayContextActive());
        foreground = priceChecker;
        Assert.True(detector.IsPathOfExileOverlayContextActive());

        registry.Unregister(priceChecker);

        Assert.False(detector.IsPathOfExileOverlayContextActive());
    }

    [Fact]
    public void OnlyPriceCheckerAndOfferCardWindowsRegisterAsGameOverlays()
    {
        var repositoryRoot = RepositoryRoot();
        var priceCheckerCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "PriceCheckerWindow.xaml.cs"));
        var offerCardCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "ItemCardPreviewWindow.xaml.cs"));

        Assert.Contains("PathOfExileOverlayWindowRegistry.Shared.Register", priceCheckerCode);
        Assert.Contains("PathOfExileOverlayWindowRegistry.Shared.Unregister", priceCheckerCode);
        Assert.Contains("PathOfExileOverlayWindowRegistry.Shared.Register", offerCardCode);
        Assert.Contains("PathOfExileOverlayWindowRegistry.Shared.Unregister", offerCardCode);
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
}
