using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeRateLimitParserTests
{
    private readonly PathOfExileTradeRateLimitParser parser = new();

    [Fact]
    public void Parse_MissingHeaders_ReturnsEmptySuccessfulSnapshot()
    {
        var result = ParseSuccessful([]);

        Assert.Null(result.Snapshot?.Policy);
        Assert.Empty(result.Snapshot!.Rules);
        Assert.Null(result.Snapshot.RetryAfterSeconds);
    }

    [Fact]
    public void Parse_HeaderNamesAreCaseInsensitive()
    {
        var result = ParseSuccessful(Headers(
            ("x-rate-limit-policy", "trade-search-request-limit"),
            ("x-rate-limit-rules", "Ip"),
            ("x-rate-limit-ip", "20:5:10"),
            ("x-rate-limit-ip-state", "3:5:0")));

        var rule = Assert.Single(result.Snapshot!.Rules);
        Assert.Equal("trade-search-request-limit", result.Snapshot.Policy);
        Assert.Equal("Ip", rule.RuleName);
        Assert.Equal(3, rule.CurrentRequestCount);
    }

    [Fact]
    public void Parse_OnePolicyAndOneRule_ParseCorrectly()
    {
        var result = ParseSuccessful(Headers(
            ("X-Rate-Limit-Policy", "trade-search-request-limit"),
            ("X-Rate-Limit-Rules", "Account"),
            ("X-Rate-Limit-Account", "20:5:10")));

        var rule = Assert.Single(result.Snapshot!.Rules);
        Assert.Equal("trade-search-request-limit", result.Snapshot.Policy);
        Assert.Equal("Account", rule.RuleName);
        Assert.Equal(20, rule.MaximumRequestCount);
        Assert.Equal(5, rule.IntervalSeconds);
        Assert.Equal(10, rule.TimeoutSeconds);
        Assert.Null(rule.CurrentRequestCount);
    }

    [Fact]
    public void Parse_MultipleNamedRules_ParseCorrectly()
    {
        var result = ParseSuccessful(Headers(
            ("X-Rate-Limit-Rules", "Account,Ip"),
            ("X-Rate-Limit-Account", "20:5:10"),
            ("X-Rate-Limit-Ip", "100:60:60")));

        Assert.Equal(["Account", "Ip"], result.Snapshot!.Rules.Select(rule => rule.RuleName));
        Assert.Equal([20, 100], result.Snapshot.Rules.Select(rule => rule.MaximumRequestCount));
    }

    [Fact]
    public void Parse_MultipleEntriesWithinOneRule_ParseCorrectly()
    {
        var result = ParseSuccessful(Headers(
            ("X-Rate-Limit-Rules", "Ip"),
            ("X-Rate-Limit-Ip", "20:5:10,100:60:60")));

        Assert.Equal(2, result.Snapshot!.Rules.Count);
        Assert.Equal([5, 60], result.Snapshot.Rules.Select(rule => rule.IntervalSeconds));
    }

    [Fact]
    public void Parse_MatchingStateEntries_AreAssociatedCorrectly()
    {
        var result = ParseSuccessful(Headers(
            ("X-Rate-Limit-Rules", "Ip"),
            ("X-Rate-Limit-Ip", "20:5:10,100:60:60"),
            ("X-Rate-Limit-Ip-State", "3:5:0,20:60:0")));

        Assert.Equal([3, 20], result.Snapshot!.Rules.Select(rule => rule.CurrentRequestCount));
        Assert.Equal([5, 60], result.Snapshot.Rules.Select(rule => rule.CurrentIntervalSeconds));
        Assert.Equal([0, 0], result.Snapshot.Rules.Select(rule => rule.CurrentTimeoutSeconds));
    }

    [Fact]
    public void Parse_MissingState_IsAccepted()
    {
        var result = ParseSuccessful(Headers(
            ("X-Rate-Limit-Rules", "Ip"),
            ("X-Rate-Limit-Ip", "20:5:10")));

        var rule = Assert.Single(result.Snapshot!.Rules);
        Assert.Null(rule.CurrentRequestCount);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parse_MalformedRuleEntry_ProducesDiagnostic()
    {
        var result = parser.Parse(Headers(
            ("X-Rate-Limit-Rules", "Ip"),
            ("X-Rate-Limit-Ip", "not-a-rule")));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Snapshot!.Rules);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == PathOfExileTradeRateLimitDiagnosticCodes.MalformedRuleEntry);
    }

    [Fact]
    public void Parse_ValidEntriesSurviveBesideMalformedEntries()
    {
        var result = parser.Parse(Headers(
            ("X-Rate-Limit-Rules", "Ip"),
            ("X-Rate-Limit-Ip", "20:5:10,bad,100:60:60")));

        Assert.True(result.IsSuccess);
        Assert.Equal([20, 100], result.Snapshot!.Rules.Select(rule => rule.MaximumRequestCount));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == PathOfExileTradeRateLimitDiagnosticCodes.MalformedRuleEntry);
    }

    [Fact]
    public void Parse_UnknownRuleNames_ArePreserved()
    {
        var result = ParseSuccessful(Headers(
            ("X-Rate-Limit-Rules", "MysteriousRule"),
            ("X-Rate-Limit-MysteriousRule", "7:11:13")));

        Assert.Equal("MysteriousRule", Assert.Single(result.Snapshot!.Rules).RuleName);
    }

    [Fact]
    public void Parse_ValidRetryAfter_Parses()
    {
        var result = ParseSuccessful(Headers(("Retry-After", "42")));

        Assert.Equal(42, result.Snapshot?.RetryAfterSeconds);
    }

    [Fact]
    public void Parse_InvalidRetryAfter_ProducesDiagnostic()
    {
        var result = parser.Parse(Headers(("Retry-After", "soon")));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Snapshot?.RetryAfterSeconds);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == PathOfExileTradeRateLimitDiagnosticCodes.InvalidRetryAfter);
    }

    [Fact]
    public void Parse_NegativeRetryAfter_IsRejected()
    {
        var result = parser.Parse(Headers(("Retry-After", "-1")));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Snapshot?.RetryAfterSeconds);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == PathOfExileTradeRateLimitDiagnosticCodes.InvalidRetryAfter);
    }

    [Fact]
    public void Parse_NoFixedFallbackLimit_IsInvented()
    {
        var result = ParseSuccessful(Headers(("X-Rate-Limit-Policy", "dynamic-policy")));

        Assert.Empty(result.Snapshot!.Rules);
        Assert.Null(result.Snapshot.RetryAfterSeconds);
    }

    [Fact]
    public void Parse_ExtraStateEntry_ProducesDiagnosticWithoutThrowing()
    {
        var exception = Record.Exception(() => parser.Parse(Headers(
            ("X-Rate-Limit-Rules", "Ip"),
            ("X-Rate-Limit-Ip", "20:5:10"),
            ("X-Rate-Limit-Ip-State", "3:5:0,4:5:0"))));

        Assert.Null(exception);
        var result = parser.Parse(Headers(
            ("X-Rate-Limit-Rules", "Ip"),
            ("X-Rate-Limit-Ip", "20:5:10"),
            ("X-Rate-Limit-Ip-State", "3:5:0,4:5:0")));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Snapshot!.Rules);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == PathOfExileTradeRateLimitDiagnosticCodes.ExtraStateEntry);
    }

    private PathOfExileTradeRateLimitParseResult ParseSuccessful(
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        var result = parser.Parse(headers);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Snapshot);
        Assert.Empty(result.Diagnostics);
        return result;
    }

    private static IEnumerable<KeyValuePair<string, IEnumerable<string>>> Headers(
        params (string Name, string Value)[] headers)
    {
        return headers.Select(header =>
            new KeyValuePair<string, IEnumerable<string>>(
                header.Name,
                [header.Value]));
    }
}
