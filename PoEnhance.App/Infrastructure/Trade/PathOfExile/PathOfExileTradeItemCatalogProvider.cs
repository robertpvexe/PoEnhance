namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeItemCatalogProvider : IPathOfExileTradeItemCatalogProvider
{
    private readonly IPathOfExileTradeItemsClient itemsClient;
    private readonly object gate = new();
    private PathOfExileTradeItemCatalog? cachedCatalog;
    private Task<PathOfExileTradeItemsExecutionResult>? inFlightLoad;

    public PathOfExileTradeItemCatalogProvider(IPathOfExileTradeItemsClient itemsClient)
    {
        this.itemsClient = itemsClient ?? throw new ArgumentNullException(nameof(itemsClient));
    }

    public async Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return CallerCancelled();
        }

        Task<PathOfExileTradeItemsExecutionResult> loadTask;
        lock (gate)
        {
            if (cachedCatalog is not null)
            {
                return PathOfExileTradeItemCatalogProviderResult.Success(cachedCatalog);
            }

            loadTask = inFlightLoad ??= itemsClient.GetItemsAsync(CancellationToken.None);
        }

        PathOfExileTradeItemsExecutionResult itemsResult;
        try
        {
            itemsResult = await loadTask
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
                if (itemsResult.IsSuccess && itemsResult.Catalog is not null)
                {
                    cachedCatalog = itemsResult.Catalog;
                }

                inFlightLoad = null;
            }
        }

        return PathOfExileTradeItemCatalogProviderResult.FromItemsResult(itemsResult);
    }

    private static PathOfExileTradeItemCatalogProviderResult CallerCancelled()
    {
        return new PathOfExileTradeItemCatalogProviderResult
        {
            IsCancelled = true,
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.CallerCancellation,
                    "The Trade items catalog load was cancelled by the caller."),
            ],
        };
    }
}
