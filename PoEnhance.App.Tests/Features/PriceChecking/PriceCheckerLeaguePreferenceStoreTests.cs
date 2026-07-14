using PoEnhance.App.Features.PriceChecking;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerLeaguePreferenceStoreTests
{
    [Fact]
    public void LoadLeagueIdentifier_MissingPreferenceReturnsNull()
    {
        using var temp = TempDirectory.Create();
        var store = new PriceCheckerLeaguePreferenceStore(Path.Combine(temp.Path, "missing.json"));

        var leagueIdentifier = store.LoadLeagueIdentifier();

        Assert.Null(leagueIdentifier);
    }

    [Fact]
    public void SaveAndReloadPreference_PreservesTrimmedLeague()
    {
        using var temp = TempDirectory.Create();
        var path = Path.Combine(temp.Path, "league.json");

        new PriceCheckerLeaguePreferenceStore(path).SaveLeagueIdentifier("  Mirage  ");
        var leagueIdentifier = new PriceCheckerLeaguePreferenceStore(path).LoadLeagueIdentifier();

        Assert.Equal("Mirage", leagueIdentifier);
    }

    [Fact]
    public void SavePreference_EmptyLeagueDoesNotOverwriteExistingValue()
    {
        using var temp = TempDirectory.Create();
        var path = Path.Combine(temp.Path, "league.json");
        var store = new PriceCheckerLeaguePreferenceStore(path);
        store.SaveLeagueIdentifier("Standard");

        store.SaveLeagueIdentifier("   ");

        Assert.Equal("Standard", store.LoadLeagueIdentifier());
    }

    [Fact]
    public void LoadLeagueIdentifier_MalformedPreferenceFailsSafely()
    {
        using var temp = TempDirectory.Create();
        var path = Path.Combine(temp.Path, "league.json");
        File.WriteAllText(path, "{ malformed");
        var store = new PriceCheckerLeaguePreferenceStore(path);

        var exception = Record.Exception(() => store.LoadLeagueIdentifier());

        Assert.Null(exception);
        Assert.Null(store.LoadLeagueIdentifier());
    }

    [Fact]
    public void ResolveDefaultPath_UsesLocalApplicationData()
    {
        var resolver = new PriceCheckerLeaguePreferenceStorePathResolver(
            folder => folder == Environment.SpecialFolder.LocalApplicationData
                ? @"C:\Users\Test\AppData\Local"
                : throw new InvalidOperationException());

        var path = resolver.ResolveDefaultPath();

        Assert.Equal(
            Path.Combine(
                @"C:\Users\Test\AppData\Local",
                "PoEnhance",
                "price-checker-league.json"),
            path);
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
                $"poenhance-price-checker-league-{Guid.NewGuid():N}");
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
