namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeStatCatalogProvider : IPathOfExileTradeStatCatalogProvider
{
    private readonly IPathOfExileTradeStatsClient statsClient;
    private readonly object gate = new();
    private PathOfExileTradeStatCatalog? cachedCatalog;
    private Task<PathOfExileTradeStatsExecutionResult>? inFlightLoad;

    public PathOfExileTradeStatCatalogProvider(IPathOfExileTradeStatsClient statsClient)
    {
        this.statsClient = statsClient ?? throw new ArgumentNullException(nameof(statsClient));
    }

    public bool TryGetCachedCatalog(out PathOfExileTradeStatCatalog catalog)
    {
        lock (gate)
        {
            if (cachedCatalog is not null)
            {
                catalog = cachedCatalog;
                return true;
            }
        }

        catalog = null!;
        return false;
    }

    public async Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return CallerCancelled();
        }

        Task<PathOfExileTradeStatsExecutionResult> loadTask;
        lock (gate)
        {
            if (cachedCatalog is not null)
            {
                return PathOfExileTradeStatCatalogProviderResult.Success(cachedCatalog);
            }

            loadTask = inFlightLoad ??= statsClient.GetStatsAsync(CancellationToken.None);
        }

        PathOfExileTradeStatsExecutionResult statsResult;
        try
        {
            statsResult = await loadTask
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CallerCancelled();
        }

        lock (gate)
        {
            if (ReferenceEquals(inFlightLoad, loadTask))
            {
                if (statsResult.IsSuccess && statsResult.Catalog is not null)
                {
                    cachedCatalog = statsResult.Catalog;
                }

                inFlightLoad = null;
            }
        }

        return PathOfExileTradeStatCatalogProviderResult.FromStatsResult(statsResult);
    }

    private static PathOfExileTradeStatCatalogProviderResult CallerCancelled()
    {
        return new PathOfExileTradeStatCatalogProviderResult
        {
            IsCancelled = true,
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.CallerCancellation,
                    "The Trade stats catalog load was cancelled by the caller."),
            ],
        };
    }
}
