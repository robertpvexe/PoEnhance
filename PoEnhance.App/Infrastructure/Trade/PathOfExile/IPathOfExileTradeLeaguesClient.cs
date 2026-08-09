namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeLeaguesClient
{
    Task<PathOfExileTradeLeaguesExecutionResult> GetLeaguesAsync(
        CancellationToken cancellationToken = default);
}
