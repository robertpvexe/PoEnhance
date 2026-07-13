namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeResponseParseResult
{
    public bool IsSuccess => Response is not null;

    public PathOfExileTradeSearchResponse? Response { get; init; }

    public PathOfExileTradeProviderError? ProviderError { get; init; }

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> Diagnostics { get; init; } = [];

    public static PathOfExileTradeResponseParseResult Success(
        PathOfExileTradeSearchResponse response)
    {
        return new PathOfExileTradeResponseParseResult
        {
            Response = response,
        };
    }

    public static PathOfExileTradeResponseParseResult Failure(
        params PathOfExileTradeQueryDiagnostic[] diagnostics)
    {
        return new PathOfExileTradeResponseParseResult
        {
            Diagnostics = diagnostics,
        };
    }

    public static PathOfExileTradeResponseParseResult ProviderFailure(
        PathOfExileTradeProviderError providerError)
    {
        return new PathOfExileTradeResponseParseResult
        {
            ProviderError = providerError,
        };
    }
}
