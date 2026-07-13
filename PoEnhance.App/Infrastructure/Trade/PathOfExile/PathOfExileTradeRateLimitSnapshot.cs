namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeRateLimitSnapshot
{
    public string? Policy { get; init; }

    public IReadOnlyList<PathOfExileTradeRateLimitRule> Rules { get; init; } = [];

    public int? RetryAfterSeconds { get; init; }
}
