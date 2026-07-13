using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeResponseParserTests
{
    private readonly PathOfExileTradeResponseParser parser = new();

    [Fact]
    public void ParseSearchResponse_StandardSuccessfulResponse_Parses()
    {
        var result = ParseSuccessful("""
{
  "id": "abc123",
  "result": ["id1", "id2"],
  "total": 2,
  "inexact": false
}
""");

        Assert.Equal("abc123", result.Response?.Id);
        Assert.Equal(["id1", "id2"], result.Response?.Result);
        Assert.Equal(2, result.Response?.Total);
        Assert.False(result.Response?.Inexact);
    }

    [Fact]
    public void ParseSearchResponse_ResultOrderIsPreserved()
    {
        var result = ParseSuccessful("""
{ "id": "abc123", "result": ["id3", "id1", "id2"], "total": 3 }
""");

        Assert.Equal(["id3", "id1", "id2"], result.Response?.Result);
    }

    [Fact]
    public void ParseSearchResponse_ZeroResultResponse_IsSuccessful()
    {
        var result = ParseSuccessful("""
{ "id": "abc123", "result": [], "total": 0, "inexact": false }
""");

        Assert.Empty(result.Response!.Result);
        Assert.Equal(0, result.Response.Total);
    }

    [Fact]
    public void ParseSearchResponse_MissingInexact_IsValid()
    {
        var result = ParseSuccessful("""
{ "id": "abc123", "result": ["id1"], "total": 1 }
""");

        Assert.Null(result.Response?.Inexact);
    }

    [Fact]
    public void ParseSearchResponse_ProviderError_ParsesCodeAndMessage()
    {
        var result = parser.ParseSearchResponse("""
{ "error": { "code": 1, "message": "Invalid query" } }
""");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ProviderError);
        Assert.Equal("1", result.ProviderError.Code);
        Assert.Equal("Invalid query", result.ProviderError.Message);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ParseSearchResponse_TextualProviderErrorCode_IsPreserved()
    {
        var result = parser.ParseSearchResponse("""
{ "error": { "code": "bad_query", "message": "Invalid query" } }
""");

        Assert.False(result.IsSuccess);
        Assert.Equal("bad_query", result.ProviderError?.Code);
        Assert.Equal("Invalid query", result.ProviderError?.Message);
    }

    [Fact]
    public void ParseSearchResponse_MalformedJson_FailsWithoutThrowing()
    {
        var exception = Record.Exception(() => parser.ParseSearchResponse("{ nope"));

        Assert.Null(exception);
        AssertFailure(
            parser.ParseSearchResponse("{ nope"),
            PathOfExileTradeResponseDiagnosticCodes.MalformedJson);
    }

    [Fact]
    public void ParseSearchResponse_MissingRequiredId_Fails()
    {
        var result = parser.ParseSearchResponse("""
{ "result": ["id1"], "total": 1 }
""");

        AssertFailure(result, PathOfExileTradeResponseDiagnosticCodes.MissingSearchId);
    }

    [Fact]
    public void ParseSearchResponse_MissingRequiredResultCollection_Fails()
    {
        var result = parser.ParseSearchResponse("""
{ "id": "abc123", "total": 1 }
""");

        AssertFailure(result, PathOfExileTradeResponseDiagnosticCodes.MissingResultCollection);
    }

    [Fact]
    public void ParseSearchResponse_RepeatedParsing_IsEquivalent()
    {
        const string json = """
{ "id": "abc123", "result": ["id1", "id2"], "total": 2, "inexact": true }
""";

        var first = ParseSuccessful(json);
        var second = ParseSuccessful(json);

        Assert.Equal(first.Response?.Id, second.Response?.Id);
        Assert.Equal(first.Response?.Result, second.Response?.Result);
        Assert.Equal(first.Response?.Total, second.Response?.Total);
        Assert.Equal(first.Response?.Inexact, second.Response?.Inexact);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
    }

    private PathOfExileTradeResponseParseResult ParseSuccessful(string json)
    {
        var result = parser.ParseSearchResponse(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Null(result.ProviderError);
        Assert.Empty(result.Diagnostics);
        return result;
    }

    private static void AssertFailure(
        PathOfExileTradeResponseParseResult result,
        string expectedCode)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Response);
        Assert.Null(result.ProviderError);
        Assert.Equal(expectedCode, Assert.Single(result.Diagnostics).Code);
    }
}
