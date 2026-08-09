using System.Net;
using System.Net.Http.Headers;
using System.Text;
using PoEnhance.App.Infrastructure.Settings;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeLeaguesResponseParserTests
{
    private readonly PathOfExileTradeLeaguesResponseParser parser = new();

    [Fact]
    public void IdAndTextRemainDistinctAndUnknownFieldsAreIgnored()
    {
        var result = parser.ParseLeaguesResponse(
            """{"result":[{"id":"provider-id","text":"Display name","realm":"pc","future":{"x":1}}]}""");

        var entry = Assert.Single(result.Entries!);
        Assert.Equal("provider-id", entry.ProviderId);
        Assert.Equal("Display name", entry.DisplayText);
        Assert.Equal("pc", entry.Realm);
        Assert.Equal(0, entry.ProviderOrder);
    }

    [Fact]
    public void PcAndNonPcRowsWithSameTextRemainSeparateAndOrdered()
    {
        var result = parser.ParseLeaguesResponse(
            """{"result":[{"id":"pc-id","text":"Same","realm":"pc"},{"id":"xbox-id","text":"Same","realm":"xbox"}]}""");

        Assert.Collection(result.Entries!,
            entry => Assert.Equal(("pc-id", "pc", 0), (entry.ProviderId, entry.Realm, entry.ProviderOrder)),
            entry => Assert.Equal(("xbox-id", "xbox", 1), (entry.ProviderId, entry.Realm, entry.ProviderOrder)));
    }

    [Theory]
    [InlineData("{\"result\":[{\"text\":\"Name\",\"realm\":\"pc\"}]}", PathOfExileTradeLeaguesDiagnosticCodes.MissingProviderId)]
    [InlineData("{\"result\":[{\"id\":\"id\",\"realm\":\"pc\"}]}", PathOfExileTradeLeaguesDiagnosticCodes.MissingDisplayText)]
    [InlineData("{\"result\":[{\"id\":\"id\",\"text\":\"Name\"}]}", PathOfExileTradeLeaguesDiagnosticCodes.MissingRealm)]
    public void MissingRequiredFieldCannotCreateSelectableRow(string json, string code)
    {
        var result = parser.ParseLeaguesResponse(json);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == PathOfExileTradeLeaguesDiagnosticCodes.UnusableEmptyCatalog);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"result\":[]}")]
    public void MalformedOrEmptyCatalogFailsVisibly(string json)
    {
        var result = parser.ParseLeaguesResponse(json);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Diagnostics);
    }
}

public sealed class PathOfExileTradeLeagueResolverTests
{
    [Fact]
    public async Task LegacyTextResolvesOnlyWithinPcRealm()
    {
        var resolver = Resolver(
            Entry("pc-id", "Same", "pc", 0),
            Entry("console-id", "Same", "xbox", 1));

        var result = await resolver.ResolveAsync(new ApplicationLeagueSelection(null, "Same"));

        Assert.Equal("pc-id", result.League?.ProviderId);
    }

    [Fact]
    public async Task DuplicateDisplayTextWithinPcRealmIsAmbiguous()
    {
        var resolver = Resolver(Entry("one", "Same"), Entry("two", "Same"));

        var result = await resolver.ResolveAsync(new ApplicationLeagueSelection(null, "Same"));

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradeLeaguesDiagnosticCodes.SelectionAmbiguous,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task ModernSelectionUsesProviderIdAndAcceptsDisplayRename()
    {
        var resolver = Resolver(Entry("stable-id", "New display"));

        var result = await resolver.ResolveAsync(
            new ApplicationLeagueSelection("stable-id", "Old display"));

        Assert.Equal("New display", result.League?.DisplayText);
    }

    [Fact]
    public async Task RemovedProviderIdFailsClosedWithoutMatchingStaleDisplay()
    {
        var resolver = Resolver(Entry("replacement-id", "Same display"));

        var result = await resolver.ResolveAsync(
            new ApplicationLeagueSelection("removed-id", "Same display"));

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradeLeaguesDiagnosticCodes.SelectionNotFound,
            Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("ambiguous")]
    public async Task UnsafeLegacySelectionFailsClosed(string mode)
    {
        var entries = mode == "zero"
            ? new[] { Entry("other", "Other") }
            : new[] { Entry("legacy", "One"), Entry("two", "legacy") };
        var result = await Resolver(entries).ResolveAsync(
            new ApplicationLeagueSelection(null, "legacy"));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CatalogFailureIsReturnedAsStructuredResolutionFailure()
    {
        var provider = new TestTradeLeagueCatalogProvider(
            new PathOfExileTradeLeagueCatalogProviderResult
            {
                Diagnostics =
                [
                    new PathOfExileTradeHttpDiagnostic(
                        PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                        "failed"),
                ],
            });

        var result = await new PathOfExileTradeLeagueResolver(provider).ResolveAsync(
            new ApplicationLeagueSelection(null, "Anything"));

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
            Assert.Single(result.Diagnostics).Code);
    }

    private static PathOfExileTradeLeagueResolver Resolver(
        params PathOfExileTradeLeagueEntry[] entries) =>
        new(new TestTradeLeagueCatalogProvider(entries));

    private static PathOfExileTradeLeagueEntry Entry(
        string id,
        string text,
        string realm = "pc",
        int order = 0) => new(id, text, realm, order);
}

public sealed class PathOfExileTradeLeagueCatalogProviderTests
{
    [Fact]
    public async Task ConcurrentLoadsAreCoalesced()
    {
        var client = new FakeLeaguesClient();
        var pending = new TaskCompletionSource<PathOfExileTradeLeaguesExecutionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.Enqueue(_ => pending.Task);
        var provider = new PathOfExileTradeLeagueCatalogProvider(client);

        var first = provider.GetCatalogAsync();
        var second = provider.GetCatalogAsync();
        pending.SetResult(SuccessCatalog(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2)));

        Assert.True((await first).IsSuccess);
        Assert.True((await second).IsSuccess);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task FreshCatalogIsReusedAndExpiredCatalogRefreshes()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var client = new FakeLeaguesClient();
        client.Enqueue(_ => Task.FromResult(SuccessCatalog(clock.GetUtcNow(), TimeSpan.FromMinutes(2))));
        client.Enqueue(_ => Task.FromResult(SuccessCatalog(clock.GetUtcNow(), TimeSpan.FromMinutes(2), "second")));
        var provider = new PathOfExileTradeLeagueCatalogProvider(client, clock);

        var first = await provider.GetCatalogAsync();
        clock.Advance(TimeSpan.FromMinutes(1));
        var cached = await provider.GetCatalogAsync();
        clock.Advance(TimeSpan.FromMinutes(2));
        var refreshed = await provider.GetCatalogAsync();

        Assert.Same(first.Catalog, cached.Catalog);
        Assert.Equal("second", Assert.Single(refreshed.Catalog!.Entries).ProviderId);
        Assert.Equal(2, client.CallCount);
    }

    [Fact]
    public async Task FailedRefreshDoesNotReturnExpiredCatalog()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var client = new FakeLeaguesClient();
        client.Enqueue(_ => Task.FromResult(SuccessCatalog(clock.GetUtcNow(), TimeSpan.FromSeconds(1))));
        client.Enqueue(_ => Task.FromResult(new PathOfExileTradeLeaguesExecutionResult
        {
            Diagnostics = [new PathOfExileTradeHttpDiagnostic("refresh", "failed")],
        }));
        var provider = new PathOfExileTradeLeagueCatalogProvider(client, clock);
        _ = await provider.GetCatalogAsync();
        clock.Advance(TimeSpan.FromSeconds(2));

        var refresh = await provider.GetCatalogAsync();

        Assert.False(refresh.IsSuccess);
        Assert.Equal("refresh", Assert.Single(refresh.Diagnostics).Code);
    }

    [Fact]
    public async Task CallerCancellationIsRespectedWithoutStartingPolling()
    {
        var client = new FakeLeaguesClient();
        var pending = new TaskCompletionSource<PathOfExileTradeLeaguesExecutionResult>();
        client.Enqueue(_ => pending.Task);
        var provider = new PathOfExileTradeLeagueCatalogProvider(client);
        using var cancellation = new CancellationTokenSource();

        var load = provider.GetCatalogAsync(cancellation.Token);
        cancellation.Cancel();
        var result = await load;

        Assert.True(result.IsCancelled);
        Assert.Equal(1, client.CallCount);
        await Task.Delay(20);
        Assert.Equal(1, client.CallCount);
        pending.SetResult(SuccessCatalog(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1)));
    }

    private static PathOfExileTradeLeaguesExecutionResult SuccessCatalog(
        DateTimeOffset now,
        TimeSpan lifetime,
        string id = "first") => new()
    {
        IsSuccess = true,
        Catalog = new PathOfExileTradeLeagueCatalog(
            [new PathOfExileTradeLeagueEntry(id, "Display", "pc", 0)],
            now,
            now + lifetime),
    };

    private sealed class FakeLeaguesClient : IPathOfExileTradeLeaguesClient
    {
        private readonly Queue<Func<CancellationToken, Task<PathOfExileTradeLeaguesExecutionResult>>> loads = [];
        public int CallCount { get; private set; }
        public void Enqueue(Func<CancellationToken, Task<PathOfExileTradeLeaguesExecutionResult>> load) => loads.Enqueue(load);
        public Task<PathOfExileTradeLeaguesExecutionResult> GetLeaguesAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return loads.Dequeue()(cancellationToken);
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
        public void Advance(TimeSpan duration) => utcNow += duration;
    }
}

public sealed class PathOfExileTradeLeaguesClientTests
{
    [Fact]
    public async Task UsesOfficialEndpointAndHonorsProviderMaxAge()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var response = Response();
        response.Headers.CacheControl = new CacheControlHeaderValue { Public = true, MaxAge = TimeSpan.FromSeconds(120) };
        response.Headers.Age = TimeSpan.FromSeconds(20);
        var handler = new SingleResponseHandler(response);
        using var http = new HttpClient(handler);
        var client = new PathOfExileTradeLeaguesClient(
            http,
            new PathOfExileTradeEndpointBuilder(),
            new PathOfExileTradeLeaguesResponseParser(),
            new FixedTimeProvider(now));

        var result = await client.GetLeaguesAsync();

        Assert.Equal("https://www.pathofexile.com/api/trade/data/leagues", handler.RequestUri?.ToString());
        Assert.Equal(now, result.Catalog?.RetrievedAtUtc);
        Assert.Equal(now.AddSeconds(120), result.Catalog?.FreshUntilUtc);
    }

    [Fact]
    public async Task MissingMaxAgeUsesConservativeBoundedFallback()
    {
        var now = DateTimeOffset.UtcNow;
        var handler = new SingleResponseHandler(Response());
        using var http = new HttpClient(handler);
        var client = new PathOfExileTradeLeaguesClient(
            http,
            new PathOfExileTradeEndpointBuilder(),
            new PathOfExileTradeLeaguesResponseParser(),
            new FixedTimeProvider(now));

        var result = await client.GetLeaguesAsync();

        Assert.Equal(now + PathOfExileTradeLeaguesClient.FallbackFreshnessLifetime,
            result.Catalog?.FreshUntilUtc);
    }

    private static HttpResponseMessage Response() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            """{"result":[{"id":"id","text":"Display","realm":"pc"}]}""",
            Encoding.UTF8,
            "application/json"),
    };

    private sealed class SingleResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(response);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
