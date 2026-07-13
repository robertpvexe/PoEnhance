namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeFetchResponseParseResult
{
    public bool IsSuccess => Response is not null;

    public PathOfExileTradeFetchResponse? Response { get; init; }

    public PathOfExileTradeProviderError? ProviderError { get; init; }

    public IReadOnlyList<PathOfExileTradeHttpDiagnostic> Diagnostics { get; init; } = [];

    public static PathOfExileTradeFetchResponseParseResult Success(
        PathOfExileTradeFetchResponse response,
        IReadOnlyList<PathOfExileTradeHttpDiagnostic> diagnostics)
    {
        return new PathOfExileTradeFetchResponseParseResult
        {
            Response = response,
            Diagnostics = diagnostics,
        };
    }

    public static PathOfExileTradeFetchResponseParseResult Failure(
        params PathOfExileTradeHttpDiagnostic[] diagnostics)
    {
        return new PathOfExileTradeFetchResponseParseResult
        {
            Diagnostics = diagnostics,
        };
    }

    public static PathOfExileTradeFetchResponseParseResult ProviderFailure(
        PathOfExileTradeProviderError providerError)
    {
        return new PathOfExileTradeFetchResponseParseResult
        {
            ProviderError = providerError,
        };
    }
}
