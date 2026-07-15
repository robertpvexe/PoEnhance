using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeFilterCatalogProviderTests
{
    [Fact]
    public async Task GetCatalogAsync_ReusesTheSuccessfulSharedCatalog()
    {
        var client = new FakeFiltersClient();
        var catalog = Catalog();
        client.Enqueue(Success(catalog));
        var provider = new PathOfExileTradeFilterCatalogProvider(client);

        var result = await provider.GetCatalogAsync();
        var cachedResult = await provider.GetCatalogAsync();

        Assert.True(result.IsSuccess);
        Assert.True(cachedResult.IsSuccess);
        Assert.Same(catalog, cachedResult.Catalog);
        Assert.Single(client.Calls);
    }

    [Fact]
    public async Task GetCatalogAsync_FailureDoesNotPopulateTheCachedCatalog()
    {
        var client = new FakeFiltersClient();
        client.Enqueue(new PathOfExileTradeFiltersExecutionResult
        {
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                    "Filters failed."),
            ],
        });
        client.Enqueue(Success(Catalog()));
        var provider = new PathOfExileTradeFilterCatalogProvider(client);

        var result = await provider.GetCatalogAsync();
        var retry = await provider.GetCatalogAsync();

        Assert.False(result.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(2, client.Calls.Count);
    }

    [Fact]
    public async Task GetCatalogAsync_ConcurrentInitializationAndFirstItemShareOneProviderRequest()
    {
        var client = new FakeFiltersClient();
        var completion = new TaskCompletionSource<PathOfExileTradeFiltersExecutionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.Enqueue(completion.Task);
        var provider = new PathOfExileTradeFilterCatalogProvider(client);

        var initialization = provider.GetCatalogAsync();
        var firstItem = provider.GetCatalogAsync();

        Assert.Single(client.Calls);
        completion.SetResult(Success(Catalog()));
        var results = await Task.WhenAll(initialization, firstItem);

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Same(results[0].Catalog, results[1].Catalog);
        Assert.Single(client.Calls);
    }

    private static PathOfExileTradeFiltersExecutionResult Success(PathOfExileTradeFilterCatalog catalog)
    {
        return new PathOfExileTradeFiltersExecutionResult
        {
            IsSuccess = true,
            Catalog = catalog,
        };
    }

    private static PathOfExileTradeFilterCatalog Catalog()
    {
        return new PathOfExileTradeFilterCatalog(
        [
            new PathOfExileTradeFilterOption
            {
                ProviderOrder = 0,
                GroupId = "type_filters",
                FilterId = "category",
                Id = "weapon.wand",
                Text = "Wand",
            },
        ]);
    }

    private sealed class FakeFiltersClient : IPathOfExileTradeFiltersClient
    {
        private readonly Queue<Task<PathOfExileTradeFiltersExecutionResult>> pendingResults = [];

        public List<CancellationToken> Calls { get; } = [];

        public void Enqueue(PathOfExileTradeFiltersExecutionResult result)
        {
            pendingResults.Enqueue(Task.FromResult(result));
        }

        public void Enqueue(Task<PathOfExileTradeFiltersExecutionResult> result)
        {
            pendingResults.Enqueue(result);
        }

        public Task<PathOfExileTradeFiltersExecutionResult> GetFiltersAsync(
            CancellationToken cancellationToken = default)
        {
            Calls.Add(cancellationToken);
            return pendingResults.Dequeue();
        }
    }
}
