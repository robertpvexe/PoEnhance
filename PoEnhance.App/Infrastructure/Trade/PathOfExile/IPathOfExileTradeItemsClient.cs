namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeItemsClient
{
    Task<PathOfExileTradeItemsExecutionResult> GetItemsAsync(
        CancellationToken cancellationToken = default);
}
