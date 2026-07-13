namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradePriceCheckDiagnosticCodes
{
    public const string QueryBuildFailed = "POE_TRADE_PRICE_CHECK_QUERY_BUILD_FAILED";
    public const string SearchFailed = "POE_TRADE_PRICE_CHECK_SEARCH_FAILED";
    public const string SearchCancelled = "POE_TRADE_PRICE_CHECK_SEARCH_CANCELLED";
    public const string SearchTimeout = "POE_TRADE_PRICE_CHECK_SEARCH_TIMEOUT";
    public const string MissingSearchQueryId = "POE_TRADE_PRICE_CHECK_MISSING_SEARCH_QUERY_ID";
    public const string SearchDiagnostic = "POE_TRADE_PRICE_CHECK_SEARCH_DIAGNOSTIC";
    public const string FetchFailed = "POE_TRADE_PRICE_CHECK_FETCH_FAILED";
    public const string FetchCancelled = "POE_TRADE_PRICE_CHECK_FETCH_CANCELLED";
    public const string FetchTimeout = "POE_TRADE_PRICE_CHECK_FETCH_TIMEOUT";
    public const string FetchDiagnostic = "POE_TRADE_PRICE_CHECK_FETCH_DIAGNOSTIC";
}
