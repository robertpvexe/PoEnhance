using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeStatCatalogProviderTests
{
    [Fact]
    public async Task GetCatalogAsync_CachesSuccessfulCatalogForSession()
    {
        var statsClient = new FakeStatsClient();
        var catalog = Catalog("explicit.stat_life");
        statsClient.Enqueue(Success(catalog));
        var provider = new PathOfExileTradeStatCatalogProvider(statsClient);

        var first = await provider.GetCatalogAsync();
        var second = await provider.GetCatalogAsync();

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Same(catalog, first.Catalog);
        Assert.Same(catalog, second.Catalog);
        Assert.Single(statsClient.Calls);
    }

    [Fact]
    public async Task GetCatalogAsync_DoesNotCacheFailedLoadAndAllowsLaterRetry()
    {
        var statsClient = new FakeStatsClient();
        var catalog = Catalog("explicit.stat_life");
        statsClient.Enqueue(new PathOfExileTradeStatsExecutionResult
        {
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                    "Stats failed."),
            ],
        });
        statsClient.Enqueue(Success(catalog));
        var provider = new PathOfExileTradeStatCatalogProvider(statsClient);

        var first = await provider.GetCatalogAsync();
        var second = await provider.GetCatalogAsync();

        Assert.False(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Same(catalog, second.Catalog);
        Assert.Equal(2, statsClient.Calls.Count);
    }

    [Fact]
    public async Task GetCatalogAsync_ConcurrentCallersShareOneInFlightStatsLoad()
    {
        var statsClient = new FakeStatsClient();
        var completion = new TaskCompletionSource<PathOfExileTradeStatsExecutionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = Catalog("explicit.stat_life");
        statsClient.Handler = _ => completion.Task;
        var provider = new PathOfExileTradeStatCatalogProvider(statsClient);

        var first = provider.GetCatalogAsync();
        var second = provider.GetCatalogAsync();
        await WaitUntilAsync(() => statsClient.Calls.Count == 1);
        completion.SetResult(Success(catalog));

        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.All(results, result => Assert.Same(catalog, result.Catalog));
        Assert.Single(statsClient.Calls);
    }

    [Fact]
    public async Task GetCatalogAsync_CancelledCallerDoesNotCancelOrPoisonSharedLoad()
    {
        var statsClient = new FakeStatsClient();
        var completion = new TaskCompletionSource<PathOfExileTradeStatsExecutionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = Catalog("explicit.stat_life");
        statsClient.Handler = _ => completion.Task;
        var provider = new PathOfExileTradeStatCatalogProvider(statsClient);
        using var cancellation = new CancellationTokenSource();

        var cancelledTask = provider.GetCatalogAsync(cancellation.Token);
        await WaitUntilAsync(() => statsClient.Calls.Count == 1);
        await cancellation.CancelAsync();
        var cancelled = await cancelledTask;
        completion.SetResult(Success(catalog));

        var later = await provider.GetCatalogAsync();

        Assert.True(cancelled.IsCancelled);
        Assert.True(later.IsSuccess);
        Assert.Same(catalog, later.Catalog);
        Assert.Single(statsClient.Calls);
        Assert.False(statsClient.Calls[0].CanBeCanceled);
    }

    private static PathOfExileTradeStatsExecutionResult Success(PathOfExileTradeStatCatalog catalog)
    {
        return new PathOfExileTradeStatsExecutionResult
        {
            IsSuccess = true,
            Catalog = catalog,
        };
    }

    private static PathOfExileTradeStatCatalog Catalog(string statId)
    {
        return new PathOfExileTradeStatCatalog(
        [
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 0,
                GroupId = "explicit",
                GroupLabel = "Explicit",
                Id = statId,
                Text = "+# to maximum Life",
                Type = "explicit",
            },
        ]);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class FakeStatsClient : IPathOfExileTradeStatsClient
    {
        public Queue<PathOfExileTradeStatsExecutionResult> PendingResults { get; } = [];

        public List<CancellationToken> Calls { get; } = [];

        public Func<CancellationToken, Task<PathOfExileTradeStatsExecutionResult>>? Handler { get; set; }

        public void Enqueue(PathOfExileTradeStatsExecutionResult result)
        {
            PendingResults.Enqueue(result);
        }

        public Task<PathOfExileTradeStatsExecutionResult> GetStatsAsync(
            CancellationToken cancellationToken = default)
        {
            Calls.Add(cancellationToken);
            if (Handler is not null)
            {
                return Handler(cancellationToken);
            }

            if (PendingResults.Count == 0)
            {
                throw new InvalidOperationException("No fake stats result was configured.");
            }

            return Task.FromResult(PendingResults.Dequeue());
        }
    }
}
