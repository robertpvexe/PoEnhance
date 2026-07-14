namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeFiltersClient
{
    Task<PathOfExileTradeFiltersExecutionResult> GetFiltersAsync(
        CancellationToken cancellationToken = default);
}
