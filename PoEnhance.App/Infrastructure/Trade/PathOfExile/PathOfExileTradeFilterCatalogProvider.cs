namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeFilterCatalogProvider : IPathOfExileTradeFilterCatalogProvider
{
    private readonly IPathOfExileTradeFiltersClient filtersClient;
    private readonly object gate = new();
    private PathOfExileTradeFilterCatalog? cachedCatalog;
    private Task<PathOfExileTradeFiltersExecutionResult>? inFlightLoad;

    public PathOfExileTradeFilterCatalogProvider(IPathOfExileTradeFiltersClient filtersClient)
    {
        this.filtersClient = filtersClient ?? throw new ArgumentNullException(nameof(filtersClient));
    }

    public bool TryGetCachedCatalog(out PathOfExileTradeFilterCatalog catalog)
    {
        lock (gate)
        {
            catalog = cachedCatalog!;
            return catalog is not null;
        }
    }

    public async Task<PathOfExileTradeFilterCatalogProviderResult> GetCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return CallerCancelled();
        }

        Task<PathOfExileTradeFiltersExecutionResult> loadTask;
        lock (gate)
        {
            if (cachedCatalog is not null)
            {
                return PathOfExileTradeFilterCatalogProviderResult.Success(cachedCatalog);
            }

            loadTask = inFlightLoad ??= filtersClient.GetFiltersAsync(CancellationToken.None);
        }

        PathOfExileTradeFiltersExecutionResult filtersResult;
        try
        {
            filtersResult = await loadTask
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
                if (filtersResult.IsSuccess && filtersResult.Catalog is not null)
                {
                    cachedCatalog = filtersResult.Catalog;
                }

                inFlightLoad = null;
            }
        }

        return PathOfExileTradeFilterCatalogProviderResult.FromFiltersResult(filtersResult);
    }

    private static PathOfExileTradeFilterCatalogProviderResult CallerCancelled()
    {
        return new PathOfExileTradeFilterCatalogProviderResult
        {
            IsCancelled = true,
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.CallerCancellation,
                    "The Trade filters catalog load was cancelled by the caller."),
            ],
        };
    }
}
