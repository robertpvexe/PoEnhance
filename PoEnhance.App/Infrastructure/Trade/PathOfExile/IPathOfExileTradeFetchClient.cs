namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeFetchClient
{
    Task<PathOfExileTradeFetchExecutionResult> FetchAsync(
        string? queryId,
        IReadOnlyList<string?>? resultIds,
        CancellationToken cancellationToken = default);
}
