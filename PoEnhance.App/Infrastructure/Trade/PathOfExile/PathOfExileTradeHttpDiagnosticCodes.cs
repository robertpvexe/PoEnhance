namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradeHttpDiagnosticCodes
{
    public const string NullRequest = "POE_TRADE_HTTP_NULL_REQUEST";
    public const string InvalidEndpoint = "POE_TRADE_HTTP_INVALID_ENDPOINT";
    public const string SerializationFailed = "POE_TRADE_HTTP_SERIALIZATION_FAILED";
    public const string NetworkFailure = "POE_TRADE_HTTP_NETWORK_FAILURE";
    public const string CallerCancellation = "POE_TRADE_HTTP_CALLER_CANCELLATION";
    public const string Timeout = "POE_TRADE_HTTP_TIMEOUT";
    public const string NonSuccessStatus = "POE_TRADE_HTTP_NON_SUCCESS_STATUS";
    public const string MalformedResponse = "POE_TRADE_HTTP_MALFORMED_RESPONSE";
    public const string ProviderDeclaredError = "POE_TRADE_HTTP_PROVIDER_DECLARED_ERROR";
    public const string ResponseTooLarge = "POE_TRADE_HTTP_RESPONSE_TOO_LARGE";
}
