using System.Net;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeLeaguesExecutionResult
{
    public bool IsSuccess { get; init; }

    public HttpStatusCode? HttpStatusCode { get; init; }

    public PathOfExileTradeLeagueCatalog? Catalog { get; init; }

    public IReadOnlyList<PathOfExileTradeHttpDiagnostic> Diagnostics { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> ParserDiagnostics { get; init; } = [];

    public bool IsCancelled { get; init; }

    public bool IsTimeout { get; init; }
}
