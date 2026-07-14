using System.Net;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeItemCatalogProviderResult
{
    public bool IsSuccess { get; init; }

    public HttpStatusCode? HttpStatusCode { get; init; }

    public PathOfExileTradeItemCatalog? Catalog { get; init; }

    public PathOfExileTradeRateLimitSnapshot? RateLimitSnapshot { get; init; }

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> RateLimitDiagnostics { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> ParserDiagnostics { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeHttpDiagnostic> Diagnostics { get; init; } = [];

    public bool IsCancelled { get; init; }

    public bool IsTimeout { get; init; }

    public static PathOfExileTradeItemCatalogProviderResult Success(
        PathOfExileTradeItemCatalog catalog,
        HttpStatusCode? httpStatusCode = null,
        PathOfExileTradeRateLimitSnapshot? rateLimitSnapshot = null,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic>? rateLimitDiagnostics = null,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic>? parserDiagnostics = null,
        IReadOnlyList<PathOfExileTradeHttpDiagnostic>? diagnostics = null)
    {
        return new PathOfExileTradeItemCatalogProviderResult
        {
            IsSuccess = true,
            HttpStatusCode = httpStatusCode,
            Catalog = catalog,
            RateLimitSnapshot = rateLimitSnapshot,
            RateLimitDiagnostics = rateLimitDiagnostics ?? [],
            ParserDiagnostics = parserDiagnostics ?? [],
            Diagnostics = diagnostics ?? [],
        };
    }

    public static PathOfExileTradeItemCatalogProviderResult FromItemsResult(
        PathOfExileTradeItemsExecutionResult result)
    {
        return new PathOfExileTradeItemCatalogProviderResult
        {
            IsSuccess = result.IsSuccess && result.Catalog is not null,
            HttpStatusCode = result.HttpStatusCode,
            Catalog = result.Catalog,
            RateLimitSnapshot = result.RateLimitSnapshot,
            RateLimitDiagnostics = result.RateLimitDiagnostics,
            ParserDiagnostics = result.ParserDiagnostics,
            Diagnostics = result.Diagnostics,
            IsCancelled = result.IsCancelled,
            IsTimeout = result.IsTimeout,
        };
    }
}
