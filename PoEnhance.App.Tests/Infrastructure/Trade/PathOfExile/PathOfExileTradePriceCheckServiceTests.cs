using System.Net;
using System.Reflection;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradePriceCheckServiceTests
{
    private const string League = "Mercenaries";

    [Fact]
    public async Task CheckAsync_ValidDraftBuildsSearchFetchesFirstBatchAndReturnsOrderedOffers()
    {
        var fixture = ServiceFixture.Create();
        var ids = Enumerable.Range(1, 12).Select(index => $"id-{index}").ToArray();
        fixture.SearchClient.Enqueue(SearchSuccess(ids, total: 12, inexact: true));
        fixture.FetchClient.Enqueue(FetchSuccess(ids.Take(10).Select(Offer).ToArray()));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Completed, result.Stage);
        Assert.Equal("query-1", result.SearchQueryId);
        Assert.Equal(12, result.ProviderTotal);
        Assert.True(result.Inexact);
        Assert.Equal(ids.Take(10), result.Offers.Select(offer => offer.Id));
        Assert.Empty(result.Diagnostics);
        Assert.Single(fixture.QueryBuilder.Calls);
        Assert.Single(fixture.SearchClient.Calls);
        Assert.Single(fixture.FetchClient.Calls);
        Assert.Equal(ids.Take(10), fixture.FetchClient.Calls[0].ResultIds);
        Assert.Equal("query-1", fixture.FetchClient.Calls[0].QueryId);
    }

    [Fact]
    public async Task CheckAsync_ZeroSearchResultsIsSuccessfulAndDoesNotFetch()
    {
        var fixture = ServiceFixture.Create();
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Completed, result.Stage);
        Assert.Equal("query-1", result.SearchQueryId);
        Assert.Equal(0, result.ProviderTotal);
        Assert.NotNull(result.Offers);
        Assert.Empty(result.Offers);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_QueryBuildFailureReturnsStructuredFailureAndSendsNoHttp()
    {
        var fixture = ServiceFixture.Create();
        fixture.QueryBuilder.Result = PathOfExileTradeQueryBuildResult.Failure(
            new PathOfExileTradeQueryDiagnostic("LOCAL_INVALID", "Local validation failed."));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.QueryBuild, result.Stage);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.QueryBuildFailed, diagnostic.Code);
        Assert.Equal("LOCAL_INVALID", diagnostic.SourceCode);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Theory]
    [InlineData(false, false, PathOfExileTradePriceCheckDiagnosticCodes.SearchFailed)]
    [InlineData(true, false, PathOfExileTradePriceCheckDiagnosticCodes.SearchCancelled)]
    [InlineData(false, true, PathOfExileTradePriceCheckDiagnosticCodes.SearchTimeout)]
    public async Task CheckAsync_SearchFailureReturnsFailureAndDoesNotFetch(
        bool isCancelled,
        bool isTimeout,
        string expectedCode)
    {
        var fixture = ServiceFixture.Create();
        fixture.SearchClient.Enqueue(new PathOfExileTradeSearchExecutionResult
        {
            IsSuccess = false,
            IsCancelled = isCancelled,
            IsTimeout = isTimeout,
            HttpStatusCode = HttpStatusCode.BadGateway,
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.NonSuccessStatus,
                    "Search failed.",
                    HttpStatusCode.BadGateway),
            ],
        });

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Search, result.Stage);
        Assert.Equal(isCancelled, result.IsCancelled);
        Assert.Equal(isTimeout, result.IsTimeout);
        Assert.Equal(expectedCode, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.NonSuccessStatus, result.Diagnostics[0].SourceCode);
        Assert.Equal(HttpStatusCode.BadGateway, result.Diagnostics[0].HttpStatusCode);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_MissingSearchQueryIdReturnsFailureAndDoesNotFetch()
    {
        var fixture = ServiceFixture.Create();
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1"], queryId: " "));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Search, result.Stage);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.MissingSearchQueryId, result.Diagnostics[0].Code);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Theory]
    [InlineData(false, false, PathOfExileTradePriceCheckDiagnosticCodes.FetchFailed)]
    [InlineData(true, false, PathOfExileTradePriceCheckDiagnosticCodes.FetchCancelled)]
    [InlineData(false, true, PathOfExileTradePriceCheckDiagnosticCodes.FetchTimeout)]
    public async Task CheckAsync_FetchFailureReturnsFailureAfterOneFetch(
        bool isCancelled,
        bool isTimeout,
        string expectedCode)
    {
        var fixture = ServiceFixture.Create();
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1"], total: 1));
        fixture.FetchClient.Enqueue(new PathOfExileTradeFetchExecutionResult
        {
            IsSuccess = false,
            IsCancelled = isCancelled,
            IsTimeout = isTimeout,
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                    "Fetch failed."),
            ],
        });

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Fetch, result.Stage);
        Assert.Equal(isCancelled, result.IsCancelled);
        Assert.Equal(isTimeout, result.IsTimeout);
        Assert.Equal(expectedCode, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.NetworkFailure, result.Diagnostics[0].SourceCode);
        Assert.Single(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_PreCancelledTokenBuildsNoSearchOrFetch()
    {
        var fixture = ServiceFixture.Create();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League, cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsCancelled);
        Assert.Equal(PathOfExileTradePriceCheckStage.Search, result.Stage);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.SearchCancelled, Assert.Single(result.Diagnostics).Code);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_CancelledBeforeFetchDoesNotFetch()
    {
        var fixture = ServiceFixture.Create();
        using var cancellation = new CancellationTokenSource();
        fixture.SearchClient.AfterSearch = () => cancellation.Cancel();
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1"], total: 1));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League, cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsCancelled);
        Assert.Equal(PathOfExileTradePriceCheckStage.Fetch, result.Stage);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.FetchCancelled, Assert.Single(result.Diagnostics).Code);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_PreservesSeparateSearchAndFetchRateLimitSnapshots()
    {
        var fixture = ServiceFixture.Create();
        var searchRateLimit = RateLimit("trade-search");
        var fetchRateLimit = RateLimit("trade-fetch");
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1"], total: 1) with
        {
            RateLimitSnapshot = searchRateLimit,
        });
        fixture.FetchClient.Enqueue(FetchSuccess([Offer("id-1")]) with
        {
            RateLimitSnapshot = fetchRateLimit,
        });

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        Assert.Same(searchRateLimit, result.SearchRateLimitSnapshot);
        Assert.Same(fetchRateLimit, result.FetchRateLimitSnapshot);
    }

    [Fact]
    public async Task CheckAsync_PreservesPartialFetchDiagnosticsWhileRemainingSuccessful()
    {
        var fixture = ServiceFixture.Create();
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1", "bad"], total: 2));
        fixture.FetchClient.Enqueue(FetchSuccess([Offer("id-1")]) with
        {
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.MalformedOffer,
                    "Offer could not be parsed.",
                    ResultIndex: 1),
            ],
        });

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.FetchDiagnostic, diagnostic.Code);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.MalformedOffer, diagnostic.SourceCode);
        Assert.Equal(1, diagnostic.ResultIndex);
    }

    [Fact]
    public async Task CheckAsync_DoesNotRetrySearchFetchOrRequestAdditionalBatches()
    {
        var fixture = ServiceFixture.Create();
        var ids = Enumerable.Range(1, 25).Select(index => $"id-{index}").ToArray();
        fixture.SearchClient.Enqueue(SearchSuccess(ids, total: 25));
        fixture.FetchClient.Enqueue(FetchSuccess(ids.Take(10).Select(Offer).ToArray()));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        Assert.Single(fixture.SearchClient.Calls);
        Assert.Single(fixture.FetchClient.Calls);
        var fetchedIds = Assert.IsAssignableFrom<IReadOnlyList<string?>>(fixture.FetchClient.Calls[0].ResultIds);
        Assert.Equal(10, fetchedIds.Count);
        Assert.Empty(fixture.SearchClient.PendingResults);
        Assert.Empty(fixture.FetchClient.PendingResults);
    }

    [Fact]
    public async Task CheckAsync_PassesValidatedDraftValidationAndLeagueToQueryBuilder()
    {
        var fixture = ServiceFixture.Create();
        var draft = Draft();
        var validation = ValidationSuccess();
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        await fixture.Service.CheckAsync(draft, validation, League);

        var call = Assert.Single(fixture.QueryBuilder.Calls);
        Assert.Same(draft, call.Draft);
        Assert.Same(validation, call.ValidationResult);
        Assert.Equal(League, call.LeagueIdentifier);
    }

    [Fact]
    public void PriceCheckService_DoesNotConstructHttpClientOrDependOnUi()
    {
        var dependencyTypes = ReferencedMemberTypes(typeof(PathOfExileTradePriceCheckService)).ToArray();

        Assert.DoesNotContain(dependencyTypes, type => type == typeof(HttpClient));
        Assert.DoesNotContain(dependencyTypes, type => Contains(type, "PriceChecker"));
        Assert.DoesNotContain(dependencyTypes, type => Contains(type, "Wpf"));
    }

    [Fact]
    public void PriceCheckerWpfCodeBehind_DoesNotInvokeTradeServicesOrClients()
    {
        var wpfCodeBehindTypes = new[]
        {
            typeof(PriceCheckerWindow),
            typeof(PriceCheckerWindowFactory),
        };

        Assert.DoesNotContain(wpfCodeBehindTypes.SelectMany(ReferencedMemberTypes), type =>
            Contains(type, "PathOfExileTradePriceCheckService") ||
            Contains(type, "PathOfExileTradeSearchClient") ||
            Contains(type, "PathOfExileTradeFetchClient"));
    }

    [Fact]
    public void CoreAssembly_GainsNoProviderSpecificDependency()
    {
        var coreAssembly = typeof(TradeSearchDraft).Assembly;

        Assert.DoesNotContain(coreAssembly.GetTypes(), type => Contains(type, "PathOfExileTrade"));
        Assert.DoesNotContain(coreAssembly.GetReferencedAssemblies(), assembly =>
            string.Equals(assembly.Name, "PoEnhance.App", StringComparison.Ordinal));
    }

    [Fact]
    public void PriceCheckService_DoesNotIntroduceCurrencyPublicStashCacheQueueSchedulerOrWaitTypes()
    {
        var providerTypes = typeof(PathOfExileTradePriceCheckService).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "PoEnhance.App.Infrastructure.Trade.PathOfExile")
            .Where(type => !type.IsNested && !type.Name.StartsWith("<", StringComparison.Ordinal))
            .Where(type => type.Name.Contains("PriceCheck", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.DoesNotContain(providerTypes, type =>
            Contains(type, "Currency") ||
            Contains(type, "PublicStash") ||
            Contains(type, "Cache") ||
            Contains(type, "Queue") ||
            Contains(type, "Scheduler") ||
            Contains(type, "Wait"));
        Assert.DoesNotContain(
            typeof(PathOfExileTradePriceCheckService).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.Name.Contains("Retry", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Batch", StringComparison.OrdinalIgnoreCase));
    }

    private static TradeSearchDraft Draft()
    {
        return new TradeSearchDraft
        {
            ItemClass = "Body Armours",
            Rarity = "Rare",
            DisplayName = "Armoured Shell",
            ParsedBaseType = "Titan Plate",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.titan-plate",
                ResolvedBaseName = "Titan Plate",
            },
        };
    }

    private static TradeSearchValidationResult ValidationSuccess()
    {
        return TradeSearchValidationResult.FromDiagnostics([]);
    }

    private static PathOfExileTradeSearchRequest SearchRequest()
    {
        return new PathOfExileTradeSearchRequest
        {
            Query = new PathOfExileTradeSearchQuery
            {
                Status = new PathOfExileTradeSearchStatus
                {
                    Option = "securable",
                },
                Type = "Titan Plate",
            },
            Sort = new PathOfExileTradeSearchSort(),
        };
    }

    private static PathOfExileTradeSearchExecutionResult SearchSuccess(
        IReadOnlyList<string> ids,
        int total = 1,
        bool? inexact = null,
        string queryId = "query-1")
    {
        return new PathOfExileTradeSearchExecutionResult
        {
            IsSuccess = true,
            Response = new PathOfExileTradeSearchResponse
            {
                Id = queryId,
                Result = ids,
                Total = total,
                Inexact = inexact,
            },
        };
    }

    private static PathOfExileTradeFetchExecutionResult FetchSuccess(
        IReadOnlyList<PathOfExileTradeFetchedOffer> offers)
    {
        return new PathOfExileTradeFetchExecutionResult
        {
            IsSuccess = true,
            Response = new PathOfExileTradeFetchResponse
            {
                Result = offers,
            },
        };
    }

    private static PathOfExileTradeFetchedOffer Offer(string id)
    {
        return new PathOfExileTradeFetchedOffer
        {
            Id = id,
            Item = new PathOfExileTradeFetchedItem(),
            Listing = new PathOfExileTradeListing(),
        };
    }

    private static PathOfExileTradeRateLimitSnapshot RateLimit(string policy)
    {
        return new PathOfExileTradeRateLimitSnapshot
        {
            Policy = policy,
            Rules =
            [
                new PathOfExileTradeRateLimitRule
                {
                    RuleName = "Ip",
                    MaximumRequestCount = 30,
                    IntervalSeconds = 60,
                    TimeoutSeconds = 0,
                    CurrentRequestCount = 2,
                    CurrentTimeoutSeconds = 0,
                },
            ],
        };
    }

    private static IEnumerable<Type> ReferencedMemberTypes(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        return type.GetConstructors(flags).SelectMany(constructor =>
                constructor.GetParameters().Select(parameter => parameter.ParameterType))
            .Concat(type.GetFields(flags).Select(field => field.FieldType))
            .Concat(type.GetProperties(flags).Select(property => property.PropertyType))
            .Concat(type.GetMethods(flags).Select(method => method.ReturnType))
            .Concat(type.GetMethods(flags).SelectMany(method =>
                method.GetParameters().Select(parameter => parameter.ParameterType)));
    }

    private static bool Contains(Type type, string value)
    {
        return type.FullName?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
    }

    private sealed record QueryBuildCall(
        TradeSearchDraft? Draft,
        TradeSearchValidationResult? ValidationResult,
        string? LeagueIdentifier);

    private sealed record SearchCall(
        PathOfExileTradeSearchRequest? Request,
        string? LeagueIdentifier,
        CancellationToken CancellationToken);

    private sealed record FetchCall(
        string? QueryId,
        IReadOnlyList<string?>? ResultIds,
        CancellationToken CancellationToken);

    private sealed class ServiceFixture
    {
        private ServiceFixture(
            FakeQueryBuilder queryBuilder,
            FakeSearchClient searchClient,
            FakeFetchClient fetchClient)
        {
            QueryBuilder = queryBuilder;
            SearchClient = searchClient;
            FetchClient = fetchClient;
            Service = new PathOfExileTradePriceCheckService(queryBuilder, searchClient, fetchClient);
        }

        public PathOfExileTradePriceCheckService Service { get; }

        public FakeQueryBuilder QueryBuilder { get; }

        public FakeSearchClient SearchClient { get; }

        public FakeFetchClient FetchClient { get; }

        public static ServiceFixture Create()
        {
            var queryBuilder = new FakeQueryBuilder
            {
                Result = PathOfExileTradeQueryBuildResult.Success(
                    League,
                    SearchRequest(),
                    "{}",
                    "Titan Plate",
                    ItemBaseResolutionStatus.Exact),
            };

            return new ServiceFixture(
                queryBuilder,
                new FakeSearchClient(),
                new FakeFetchClient());
        }
    }

    private sealed class FakeQueryBuilder : IPathOfExileTradeQueryBuilder
    {
        public PathOfExileTradeQueryBuildResult Result { get; set; } =
            PathOfExileTradeQueryBuildResult.Failure();

        public List<QueryBuildCall> Calls { get; } = [];

        public PathOfExileTradeQueryBuildResult Build(
            TradeSearchDraft? draft,
            TradeSearchValidationResult? validationResult,
            string? leagueIdentifier)
        {
            Calls.Add(new QueryBuildCall(draft, validationResult, leagueIdentifier));
            return Result;
        }
    }

    private sealed class FakeSearchClient : IPathOfExileTradeSearchClient
    {
        public Queue<PathOfExileTradeSearchExecutionResult> PendingResults { get; } = [];

        public List<SearchCall> Calls { get; } = [];

        public Action? AfterSearch { get; set; }

        public void Enqueue(PathOfExileTradeSearchExecutionResult result)
        {
            PendingResults.Enqueue(result);
        }

        public Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
            PathOfExileTradeSearchRequest? request,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new SearchCall(request, leagueIdentifier, cancellationToken));
            if (PendingResults.Count == 0)
            {
                throw new InvalidOperationException("No fake Search result was configured.");
            }

            var result = PendingResults.Dequeue();
            AfterSearch?.Invoke();
            return Task.FromResult(result);
        }
    }

    private sealed class FakeFetchClient : IPathOfExileTradeFetchClient
    {
        public Queue<PathOfExileTradeFetchExecutionResult> PendingResults { get; } = [];

        public List<FetchCall> Calls { get; } = [];

        public void Enqueue(PathOfExileTradeFetchExecutionResult result)
        {
            PendingResults.Enqueue(result);
        }

        public Task<PathOfExileTradeFetchExecutionResult> FetchAsync(
            string? queryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new FetchCall(queryId, resultIds, cancellationToken));
            if (PendingResults.Count == 0)
            {
                throw new InvalidOperationException("No fake Fetch result was configured.");
            }

            return Task.FromResult(PendingResults.Dequeue());
        }
    }
}
