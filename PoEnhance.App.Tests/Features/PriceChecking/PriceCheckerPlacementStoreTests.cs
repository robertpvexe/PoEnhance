using PoEnhance.App.Features.PriceChecking;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerPlacementStoreTests
{
    [Fact]
    public void SaveHorizontalCorrection_StoresRelativeCorrectionNotAbsoluteScreenX()
    {
        using var temp = TempDirectory.Create();
        var path = Path.Combine(temp.Path, "placement.json");
        var store = new PriceCheckerPlacementStore(path);
        var key = Key();

        store.SaveHorizontalCorrection(key, correction: 25);

        var json = File.ReadAllText(path);
        Assert.Contains("25", json);
        Assert.DoesNotContain("650", json);
    }

    [Fact]
    public void PlacementKeys_DistinguishDifferentClientAndDisplayContexts()
    {
        var first = PriceCheckerPlacementKey.FromClientBounds(Bounds(
            display: @"\\.\DISPLAY1",
            width: 1920,
            height: 1080,
            dpiScale: 1));
        var second = PriceCheckerPlacementKey.FromClientBounds(Bounds(
            display: @"\\.\DISPLAY2",
            width: 2560,
            height: 1440,
            dpiScale: 1.25));

        Assert.NotEqual(first.ToStorageKey(), second.ToStorageKey());
    }

    [Fact]
    public void LoadHorizontalCorrection_MissingPlacementDataReturnsZero()
    {
        using var temp = TempDirectory.Create();
        var store = new PriceCheckerPlacementStore(Path.Combine(temp.Path, "missing.json"));

        var correction = store.LoadHorizontalCorrection(Key());

        Assert.Equal(0, correction);
    }

    [Fact]
    public void LoadHorizontalCorrection_MalformedPlacementDataFailsSafely()
    {
        using var temp = TempDirectory.Create();
        var path = Path.Combine(temp.Path, "placement.json");
        File.WriteAllText(path, "{ malformed");
        var store = new PriceCheckerPlacementStore(path);

        var exception = Record.Exception(() => store.LoadHorizontalCorrection(Key()));

        Assert.Null(exception);
        Assert.Equal(0, store.LoadHorizontalCorrection(Key()));
    }

    [Fact]
    public void SaveAndReload_PreservesCorrection()
    {
        using var temp = TempDirectory.Create();
        var path = Path.Combine(temp.Path, "placement.json");
        var key = Key();

        new PriceCheckerPlacementStore(path).SaveHorizontalCorrection(key, -42.5);
        var correction = new PriceCheckerPlacementStore(path).LoadHorizontalCorrection(key);

        Assert.Equal(-42.5, correction);
    }

    [Fact]
    public void ResetHorizontalCorrection_ClearsCorrection()
    {
        using var temp = TempDirectory.Create();
        var path = Path.Combine(temp.Path, "placement.json");
        var store = new PriceCheckerPlacementStore(path);
        var key = Key();
        store.SaveHorizontalCorrection(key, 31);

        store.ResetHorizontalCorrection(key);

        Assert.Equal(0, store.LoadHorizontalCorrection(key));
    }

    [Fact]
    public void ResolveDefaultPath_UsesLocalApplicationData()
    {
        var resolver = new PriceCheckerPlacementStorePathResolver(
            folder => folder == Environment.SpecialFolder.LocalApplicationData
                ? @"C:\Users\Test\AppData\Local"
                : throw new InvalidOperationException());

        var path = resolver.ResolveDefaultPath();

        Assert.Equal(
            Path.Combine(
                @"C:\Users\Test\AppData\Local",
                "PoEnhance",
                "price-checker-placement.json"),
            path);
    }

    private static PriceCheckerPlacementKey Key()
    {
        return PriceCheckerPlacementKey.FromClientBounds(Bounds(
            display: @"\\.\DISPLAY1",
            width: 1920,
            height: 1080,
            dpiScale: 1));
    }

    private static PathOfExileClientBounds Bounds(
        string display,
        double width,
        double height,
        double dpiScale)
    {
        return new PathOfExileClientBounds(
            Left: 100,
            Top: 50,
            Width: width,
            Height: height,
            DisplayDeviceName: display,
            DpiScaleX: dpiScale,
            DpiScaleY: dpiScale);
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"poenhance-price-checker-placement-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
