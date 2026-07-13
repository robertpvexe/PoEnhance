namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeRateLimitRule
{
    public required string RuleName { get; init; }

    public required int MaximumRequestCount { get; init; }

    public required int IntervalSeconds { get; init; }

    public required int TimeoutSeconds { get; init; }

    public int? CurrentRequestCount { get; init; }

    public int? CurrentIntervalSeconds { get; init; }

    public int? CurrentTimeoutSeconds { get; init; }
}
