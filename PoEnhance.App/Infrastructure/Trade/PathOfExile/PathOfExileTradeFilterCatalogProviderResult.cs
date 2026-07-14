namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeFilterCatalogProviderResult
{
    public bool IsSuccess { get; init; }

    public PathOfExileTradeFilterCatalog? Catalog { get; init; }

    public IReadOnlyList<PathOfExileTradeHttpDiagnostic> Diagnostics { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> ParserDiagnostics { get; init; } = [];

    public PathOfExileTradeRateLimitSnapshot? RateLimitSnapshot { get; init; }

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> RateLimitDiagnostics { get; init; } = [];

    public bool IsCancelled { get; init; }

    public bool IsTimeout { get; init; }

    public static PathOfExileTradeFilterCatalogProviderResult Success(
        PathOfExileTradeFilterCatalog catalog,
        PathOfExileTradeRateLimitSnapshot? rateLimitSnapshot = null,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic>? rateLimitDiagnostics = null)
    {
        return new PathOfExileTradeFilterCatalogProviderResult
        {
            IsSuccess = true,
            Catalog = catalog,
            RateLimitSnapshot = rateLimitSnapshot,
            RateLimitDiagnostics = rateLimitDiagnostics ?? [],
        };
    }

    public static PathOfExileTradeFilterCatalogProviderResult FromFiltersResult(
        PathOfExileTradeFiltersExecutionResult result)
    {
        return new PathOfExileTradeFilterCatalogProviderResult
        {
            IsSuccess = result.IsSuccess && result.Catalog is not null,
            Catalog = result.Catalog,
            Diagnostics = result.Diagnostics,
            ParserDiagnostics = result.ParserDiagnostics,
            RateLimitSnapshot = result.RateLimitSnapshot,
            RateLimitDiagnostics = result.RateLimitDiagnostics,
            IsCancelled = result.IsCancelled,
            IsTimeout = result.IsTimeout,
        };
    }
}
