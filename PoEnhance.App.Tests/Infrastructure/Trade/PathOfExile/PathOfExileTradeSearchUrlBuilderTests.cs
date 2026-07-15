using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeSearchUrlBuilderTests
{
    [Fact]
    public void TryBuild_CreatesTheOfficialEscapedSearchUrl()
    {
        var builder = new PathOfExileTradeSearchUrlBuilder();

        var built = builder.TryBuild("Standard SSF", "query/id?one", out var uri);

        Assert.True(built);
        Assert.Equal(
            "https://www.pathofexile.com/trade/search/Standard%20SSF/query%2Fid%3Fone",
            uri?.AbsoluteUri);
    }

    [Theory]
    [InlineData(null, "query-1")]
    [InlineData("Mirage", null)]
    [InlineData(" ", "query-1")]
    [InlineData("Mirage", " ")]
    public void TryBuild_RejectsMissingSearchIdentity(string? league, string? queryId)
    {
        var builder = new PathOfExileTradeSearchUrlBuilder();

        Assert.False(builder.TryBuild(league, queryId, out var uri));
        Assert.Null(uri);
    }

    [Fact]
    public void UrlConstruction_IsOwnedByThePathOfExileInfrastructureLayer()
    {
        var builderPath = Path.Combine(
            FindRepositoryRoot(),
            "PoEnhance.App",
            "Infrastructure",
            "Trade",
            "PathOfExile",
            "PathOfExileTradeSearchUrlBuilder.cs");
        var coreFiles = Directory.GetFiles(
            Path.Combine(FindRepositoryRoot(), "PoEnhance.Core"),
            "*.cs",
            SearchOption.AllDirectories);

        Assert.True(File.Exists(builderPath));
        Assert.DoesNotContain(
            coreFiles.Select(File.ReadAllText),
            source => source.Contains("pathofexile.com/trade/search", StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
