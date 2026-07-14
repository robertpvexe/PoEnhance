using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeItemCatalogProviderTests
{
    [Fact]
    public void Constructor_DoesNotLoadItemsCatalog()
    {
        var client = new FakeItemsClient();

        _ = new PathOfExileTradeItemCatalogProvider(client);

        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task GetCatalogAsync_SuccessfulCatalogIsReusedForSession()
    {
        var catalog = Catalog();
        var client = new FakeItemsClient();
        client.Enqueue(new PathOfExileTradeItemsExecutionResult
        {
            IsSuccess = true,
            Catalog = catalog,
        });
        var provider = new PathOfExileTradeItemCatalogProvider(client);

        var first = await provider.GetCatalogAsync();
        var second = await provider.GetCatalogAsync();

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Same(catalog, first.Catalog);
        Assert.Same(catalog, second.Catalog);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task GetCatalogAsync_FailedLoadIsRetryable()
    {
        var catalog = Catalog();
        var client = new FakeItemsClient();
        client.Enqueue(new PathOfExileTradeItemsExecutionResult
        {
            IsSuccess = false,
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                    "Network failed."),
            ],
        });
        client.Enqueue(new PathOfExileTradeItemsExecutionResult
        {
            IsSuccess = true,
            Catalog = catalog,
        });
        var provider = new PathOfExileTradeItemCatalogProvider(client);

        var first = await provider.GetCatalogAsync();
        var second = await provider.GetCatalogAsync();

        Assert.False(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Same(catalog, second.Catalog);
        Assert.Equal(2, client.CallCount);
    }

    private static PathOfExileTradeItemCatalog Catalog()
    {
        return new PathOfExileTradeItemCatalog(
        [
            new PathOfExileTradeItemEntry
            {
                ProviderOrder = 0,
                GroupId = "weapon",
                GroupLabel = "Weapons",
                Name = "Moonbender's Wing",
                Type = "Tomahawk",
                IsUnique = true,
            },
        ]);
    }

    private sealed class FakeItemsClient : IPathOfExileTradeItemsClient
    {
        private readonly Queue<PathOfExileTradeItemsExecutionResult> results = [];

        public int CallCount { get; private set; }

        public void Enqueue(PathOfExileTradeItemsExecutionResult result)
        {
            results.Enqueue(result);
        }

        public Task<PathOfExileTradeItemsExecutionResult> GetItemsAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (results.Count == 0)
            {
                throw new InvalidOperationException("No fake items result was configured.");
            }

            return Task.FromResult(results.Dequeue());
        }
    }
}
