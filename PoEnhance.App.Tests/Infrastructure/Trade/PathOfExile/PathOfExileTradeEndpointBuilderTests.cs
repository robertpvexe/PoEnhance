using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeEndpointBuilderTests
{
    private readonly PathOfExileTradeEndpointBuilder builder = new();

    [Fact]
    public void BuildSearchEndpoint_NormalLeague_ReturnsSearchPath()
    {
        var result = builder.BuildSearchEndpoint("Mercenaries");

        Assert.True(result.IsSuccess);
        Assert.Equal(new Uri("https://www.pathofexile.com"), result.BaseHost);
        Assert.Equal("/api/trade/search/Mercenaries", result.PathAndQuery);
    }

    [Fact]
    public void BuildSearchEndpoint_LeagueWithSpaces_IsEncodedSafely()
    {
        var result = builder.BuildSearchEndpoint("Hardcore Mercenaries");

        Assert.True(result.IsSuccess);
        Assert.Equal("/api/trade/search/Hardcore%20Mercenaries", result.PathAndQuery);
    }

    [Fact]
    public void BuildSearchEndpoint_EmptyLeague_Fails()
    {
        var result = builder.BuildSearchEndpoint(" ");

        AssertFailure(result, PathOfExileTradeEndpointDiagnosticCodes.MissingLeague);
    }

    [Fact]
    public void BuildFetchEndpoint_PreservesResultIdOrder()
    {
        var result = builder.BuildFetchEndpoint("query123", ["id3", "id1", "id2"]);

        Assert.True(result.IsSuccess);
        Assert.Equal("/api/trade/fetch/id3,id1,id2?query=query123", result.PathAndQuery);
    }

    [Fact]
    public void BuildFetchEndpoint_IncludesEncodedQueryId()
    {
        var result = builder.BuildFetchEndpoint("query id/with symbols", ["result1"]);

        Assert.True(result.IsSuccess);
        Assert.Equal("/api/trade/fetch/result1?query=query%20id%2Fwith%20symbols", result.PathAndQuery);
    }

    [Fact]
    public void BuildFetchEndpoint_EmptyResultBatch_Fails()
    {
        var result = builder.BuildFetchEndpoint("query123", []);

        AssertFailure(result, PathOfExileTradeEndpointDiagnosticCodes.EmptyResultBatch);
    }

    [Fact]
    public void BuildFetchEndpoint_BlankResultId_Fails()
    {
        var result = builder.BuildFetchEndpoint("query123", ["id1", " "]);

        AssertFailure(result, PathOfExileTradeEndpointDiagnosticCodes.BlankResultId);
    }

    [Fact]
    public void BuildFetchEndpoint_MoreThanTenResultIds_FailsWithoutTruncation()
    {
        var resultIds = Enumerable.Range(1, PathOfExileTradeEndpointBuilder.MaximumFetchResultIds + 1)
            .Select(index => $"id{index}")
            .ToArray();

        var result = builder.BuildFetchEndpoint("query123", resultIds);

        AssertFailure(result, PathOfExileTradeEndpointDiagnosticCodes.TooManyResultIds);
        Assert.Null(result.PathAndQuery);
    }

    [Fact]
    public void ProviderTradeInfrastructure_DoesNotIntroduceExchangeEndpoint()
    {
        var search = builder.BuildSearchEndpoint("Mercenaries");
        var fetch = builder.BuildFetchEndpoint("query123", ["id1"]);

        Assert.DoesNotContain("/api/trade/exchange", search.PathAndQuery, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/trade/exchange", fetch.PathAndQuery, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            typeof(PathOfExileTradeEndpointBuilder).Assembly.GetTypes(),
            type => type.FullName?.Contains("Exchange", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static void AssertFailure(
        PathOfExileTradeEndpointBuildResult result,
        string expectedCode)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.PathAndQuery);
        Assert.Equal(expectedCode, Assert.Single(result.Diagnostics).Code);
    }
}
