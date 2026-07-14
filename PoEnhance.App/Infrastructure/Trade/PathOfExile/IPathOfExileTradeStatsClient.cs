namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeStatsClient
{
    Task<PathOfExileTradeStatsExecutionResult> GetStatsAsync(
        CancellationToken cancellationToken = default);
}
