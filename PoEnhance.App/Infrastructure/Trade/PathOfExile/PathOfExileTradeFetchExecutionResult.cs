using System.Net;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeFetchExecutionResult
{
    public bool IsSuccess { get; init; }

    public HttpStatusCode? HttpStatusCode { get; init; }

    public PathOfExileTradeFetchResponse? Response { get; init; }

    public PathOfExileTradeProviderError? ProviderError { get; init; }

    public PathOfExileTradeRateLimitSnapshot? RateLimitSnapshot { get; init; }

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> RateLimitDiagnostics { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeHttpDiagnostic> Diagnostics { get; init; } = [];

    public bool IsCancelled { get; init; }

    public bool IsTimeout { get; init; }
}
