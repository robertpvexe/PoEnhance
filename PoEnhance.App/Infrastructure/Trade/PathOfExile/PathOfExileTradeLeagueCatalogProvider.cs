namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeLeagueCatalogProvider : IPathOfExileTradeLeagueCatalogProvider
{
    private readonly IPathOfExileTradeLeaguesClient client;
    private readonly TimeProvider timeProvider;
    private readonly object gate = new();
    private PathOfExileTradeLeagueCatalog? cachedCatalog;
    private Task<PathOfExileTradeLeagueCatalogProviderResult>? inFlightLoad;

    public PathOfExileTradeLeagueCatalogProvider(
        IPathOfExileTradeLeaguesClient client,
        TimeProvider? timeProvider = null)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<PathOfExileTradeLeagueCatalogProviderResult> GetCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }

        Task<PathOfExileTradeLeagueCatalogProviderResult> load;
        lock (gate)
        {
            if (cachedCatalog?.IsFresh(timeProvider.GetUtcNow()) == true)
            {
                return new PathOfExileTradeLeagueCatalogProviderResult { Catalog = cachedCatalog };
            }

            load = inFlightLoad ??= LoadAndCacheAsync();
        }

        try
        {
            return await load.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }
    }

    private async Task<PathOfExileTradeLeagueCatalogProviderResult> LoadAndCacheAsync()
    {
        await Task.Yield();
        try
        {
            var result = await client.GetLeaguesAsync(CancellationToken.None).ConfigureAwait(false);
            var providerResult = new PathOfExileTradeLeagueCatalogProviderResult
            {
                Catalog = result.IsSuccess ? result.Catalog : null,
                Diagnostics = result.Diagnostics,
                IsCancelled = result.IsCancelled,
            };
            if (providerResult.Catalog is not null)
            {
                lock (gate)
                {
                    cachedCatalog = providerResult.Catalog;
                }
            }

            return providerResult;
        }
        finally
        {
            lock (gate)
            {
                inFlightLoad = null;
            }
        }
    }

    private static PathOfExileTradeLeagueCatalogProviderResult Cancelled() => new()
    {
        IsCancelled = true,
        Diagnostics =
        [
            new PathOfExileTradeHttpDiagnostic(
                PathOfExileTradeHttpDiagnosticCodes.CallerCancellation,
                "The Trade leagues catalog load was cancelled by the caller."),
        ],
    };
}
