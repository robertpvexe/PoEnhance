using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradePriceCheckService : IPathOfExileTradePriceCheckService
{
    private readonly IPathOfExileTradeQueryBuilder queryBuilder;
    private readonly IPathOfExileTradeSearchClient searchClient;
    private readonly IPathOfExileTradeFetchClient fetchClient;

    public PathOfExileTradePriceCheckService(
        IPathOfExileTradeQueryBuilder queryBuilder,
        IPathOfExileTradeSearchClient searchClient,
        IPathOfExileTradeFetchClient fetchClient)
    {
        this.queryBuilder = queryBuilder ?? throw new ArgumentNullException(nameof(queryBuilder));
        this.searchClient = searchClient ?? throw new ArgumentNullException(nameof(searchClient));
        this.fetchClient = fetchClient ?? throw new ArgumentNullException(nameof(fetchClient));
    }

    public async Task<PathOfExileTradePriceCheckResult> CheckAsync(
        TradeSearchDraft? draft,
        TradeSearchValidationResult? validationResult,
        string? leagueIdentifier,
        CancellationToken cancellationToken = default)
    {
        var buildResult = queryBuilder.Build(draft, validationResult, leagueIdentifier);
        if (!buildResult.IsSuccess || buildResult.Request is null)
        {
            return new PathOfExileTradePriceCheckResult
            {
                Stage = PathOfExileTradePriceCheckStage.QueryBuild,
                Diagnostics = MapQueryDiagnostics(
                    buildResult.Diagnostics,
                    PathOfExileTradePriceCheckDiagnosticCodes.QueryBuildFailed,
                    PathOfExileTradePriceCheckStage.QueryBuild,
                    "The Path of Exile Trade search request could not be built."),
            };
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return CancelledBeforeHttp(PathOfExileTradePriceCheckStage.Search);
        }

        var searchResult = await searchClient.SearchAsync(
            buildResult.Request,
            buildResult.LeagueIdentifier ?? leagueIdentifier,
            cancellationToken);

        if (!searchResult.IsSuccess)
        {
            return new PathOfExileTradePriceCheckResult
            {
                Stage = PathOfExileTradePriceCheckStage.Search,
                SearchQueryId = searchResult.Response?.Id,
                ProviderTotal = searchResult.Response?.Total,
                Inexact = searchResult.Response?.Inexact,
                SearchRateLimitSnapshot = searchResult.RateLimitSnapshot,
                Diagnostics = SearchFailureDiagnostics(searchResult),
                IsCancelled = searchResult.IsCancelled,
                IsTimeout = searchResult.IsTimeout,
            };
        }

        var searchResponse = searchResult.Response;
        var searchDiagnostics = MapHttpDiagnostics(
                searchResult.Diagnostics,
                PathOfExileTradePriceCheckDiagnosticCodes.SearchDiagnostic,
                PathOfExileTradePriceCheckStage.Search)
            .Concat(MapQueryDiagnostics(
                searchResult.RateLimitDiagnostics,
                PathOfExileTradePriceCheckDiagnosticCodes.SearchDiagnostic,
                PathOfExileTradePriceCheckStage.Search))
            .ToArray();

        if (string.IsNullOrWhiteSpace(searchResponse?.Id))
        {
            return new PathOfExileTradePriceCheckResult
            {
                Stage = PathOfExileTradePriceCheckStage.Search,
                ProviderTotal = searchResponse?.Total,
                Inexact = searchResponse?.Inexact,
                SearchRateLimitSnapshot = searchResult.RateLimitSnapshot,
                Diagnostics =
                [
                    new PathOfExileTradePriceCheckDiagnostic(
                        PathOfExileTradePriceCheckDiagnosticCodes.MissingSearchQueryId,
                        "The Path of Exile Trade Search response did not include a query identifier.",
                        PathOfExileTradePriceCheckStage.Search),
                    .. searchDiagnostics,
                ],
            };
        }

        var searchQueryId = searchResponse.Id;
        var resultIds = searchResponse.Result ?? [];
        if (resultIds.Count == 0)
        {
            return new PathOfExileTradePriceCheckResult
            {
                IsSuccess = true,
                Stage = PathOfExileTradePriceCheckStage.Completed,
                SearchQueryId = searchQueryId,
                ProviderTotal = searchResponse.Total,
                Inexact = searchResponse.Inexact,
                SearchRateLimitSnapshot = searchResult.RateLimitSnapshot,
                Diagnostics = searchDiagnostics,
            };
        }

        var fetchIds = resultIds
            .Take(PathOfExileTradeEndpointBuilder.MaximumFetchResultIds)
            .ToArray();

        if (cancellationToken.IsCancellationRequested)
        {
            return CancelledBeforeFetch(searchResult, searchQueryId, searchDiagnostics);
        }

        var fetchResult = await fetchClient.FetchAsync(searchQueryId, fetchIds, cancellationToken);
        if (!fetchResult.IsSuccess)
        {
            return new PathOfExileTradePriceCheckResult
            {
                Stage = PathOfExileTradePriceCheckStage.Fetch,
                SearchQueryId = searchQueryId,
                ProviderTotal = searchResponse.Total,
                Inexact = searchResponse.Inexact,
                SearchRateLimitSnapshot = searchResult.RateLimitSnapshot,
                FetchRateLimitSnapshot = fetchResult.RateLimitSnapshot,
                Diagnostics = searchDiagnostics.Concat(FetchFailureDiagnostics(fetchResult)).ToArray(),
                IsCancelled = fetchResult.IsCancelled,
                IsTimeout = fetchResult.IsTimeout,
            };
        }

        var fetchDiagnostics = MapHttpDiagnostics(
                fetchResult.Diagnostics,
                PathOfExileTradePriceCheckDiagnosticCodes.FetchDiagnostic,
                PathOfExileTradePriceCheckStage.Fetch)
            .Concat(MapQueryDiagnostics(
                fetchResult.RateLimitDiagnostics,
                PathOfExileTradePriceCheckDiagnosticCodes.FetchDiagnostic,
                PathOfExileTradePriceCheckStage.Fetch))
            .ToArray();

        return new PathOfExileTradePriceCheckResult
        {
            IsSuccess = true,
            Stage = PathOfExileTradePriceCheckStage.Completed,
            SearchQueryId = searchQueryId,
            ProviderTotal = searchResponse.Total,
            Inexact = searchResponse.Inexact,
            Offers = fetchResult.Response?.Result ?? [],
            SearchRateLimitSnapshot = searchResult.RateLimitSnapshot,
            FetchRateLimitSnapshot = fetchResult.RateLimitSnapshot,
            Diagnostics = searchDiagnostics.Concat(fetchDiagnostics).ToArray(),
        };
    }

    private static PathOfExileTradePriceCheckResult CancelledBeforeHttp(
        PathOfExileTradePriceCheckStage stage)
    {
        return new PathOfExileTradePriceCheckResult
        {
            Stage = stage,
            Diagnostics =
            [
                new PathOfExileTradePriceCheckDiagnostic(
                    PathOfExileTradePriceCheckDiagnosticCodes.SearchCancelled,
                    "The price check was cancelled before Search was requested.",
                    stage),
            ],
            IsCancelled = true,
        };
    }

    private static PathOfExileTradePriceCheckResult CancelledBeforeFetch(
        PathOfExileTradeSearchExecutionResult searchResult,
        string searchQueryId,
        IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> searchDiagnostics)
    {
        return new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.Fetch,
            SearchQueryId = searchQueryId,
            ProviderTotal = searchResult.Response?.Total,
            Inexact = searchResult.Response?.Inexact,
            SearchRateLimitSnapshot = searchResult.RateLimitSnapshot,
            Diagnostics =
            [
                .. searchDiagnostics,
                new PathOfExileTradePriceCheckDiagnostic(
                    PathOfExileTradePriceCheckDiagnosticCodes.FetchCancelled,
                    "The price check was cancelled before Fetch was requested.",
                    PathOfExileTradePriceCheckStage.Fetch),
            ],
            IsCancelled = true,
        };
    }

    private static IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> SearchFailureDiagnostics(
        PathOfExileTradeSearchExecutionResult result)
    {
        var code = result.IsCancelled
            ? PathOfExileTradePriceCheckDiagnosticCodes.SearchCancelled
            : result.IsTimeout
                ? PathOfExileTradePriceCheckDiagnosticCodes.SearchTimeout
                : PathOfExileTradePriceCheckDiagnosticCodes.SearchFailed;
        return MapFailureDiagnostics(
            result.Diagnostics,
            result.RateLimitDiagnostics,
            code,
            PathOfExileTradePriceCheckStage.Search,
            result.IsCancelled
                ? "The Search request was cancelled."
                : result.IsTimeout
                    ? "The Search request timed out."
                    : "The Search request failed.");
    }

    private static IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> FetchFailureDiagnostics(
        PathOfExileTradeFetchExecutionResult result)
    {
        var code = result.IsCancelled
            ? PathOfExileTradePriceCheckDiagnosticCodes.FetchCancelled
            : result.IsTimeout
                ? PathOfExileTradePriceCheckDiagnosticCodes.FetchTimeout
                : PathOfExileTradePriceCheckDiagnosticCodes.FetchFailed;
        return MapFailureDiagnostics(
            result.Diagnostics,
            result.RateLimitDiagnostics,
            code,
            PathOfExileTradePriceCheckStage.Fetch,
            result.IsCancelled
                ? "The Fetch request was cancelled."
                : result.IsTimeout
                    ? "The Fetch request timed out."
                    : "The Fetch request failed.");
    }

    private static IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> MapFailureDiagnostics(
        IReadOnlyList<PathOfExileTradeHttpDiagnostic> httpDiagnostics,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic> queryDiagnostics,
        string code,
        PathOfExileTradePriceCheckStage stage,
        string fallbackMessage)
    {
        var diagnostics = MapHttpDiagnostics(httpDiagnostics, code, stage)
            .Concat(MapQueryDiagnostics(queryDiagnostics, code, stage))
            .ToArray();

        return diagnostics.Length == 0
            ? [new PathOfExileTradePriceCheckDiagnostic(code, fallbackMessage, stage)]
            : diagnostics;
    }

    private static IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> MapHttpDiagnostics(
        IReadOnlyList<PathOfExileTradeHttpDiagnostic> diagnostics,
        string code,
        PathOfExileTradePriceCheckStage stage)
    {
        return diagnostics
            .Select(diagnostic => new PathOfExileTradePriceCheckDiagnostic(
                code,
                diagnostic.Message,
                stage,
                diagnostic.Code,
                diagnostic.HttpStatusCode,
                diagnostic.ProviderCode,
                diagnostic.ResultIndex))
            .ToArray();
    }

    private static IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> MapQueryDiagnostics(
        IReadOnlyList<PathOfExileTradeQueryDiagnostic> diagnostics,
        string code,
        PathOfExileTradePriceCheckStage stage)
    {
        return diagnostics
            .Select(diagnostic => new PathOfExileTradePriceCheckDiagnostic(
                code,
                diagnostic.Message,
                stage,
                diagnostic.Code))
            .ToArray();
    }

    private static IReadOnlyList<PathOfExileTradePriceCheckDiagnostic> MapQueryDiagnostics(
        IReadOnlyList<PathOfExileTradeQueryDiagnostic> diagnostics,
        string code,
        PathOfExileTradePriceCheckStage stage,
        string fallbackMessage)
    {
        var mapped = MapQueryDiagnostics(diagnostics, code, stage);

        return mapped.Count == 0
            ? [new PathOfExileTradePriceCheckDiagnostic(code, fallbackMessage, stage)]
            : mapped;
    }
}
