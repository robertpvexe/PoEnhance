namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeEndpointBuildResult
{
    public bool IsSuccess => PathAndQuery is not null;

    public Uri BaseHost { get; init; } = PathOfExileTradeEndpointBuilder.DefaultBaseHost;

    public string? PathAndQuery { get; init; }

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> Diagnostics { get; init; } = [];

    public static PathOfExileTradeEndpointBuildResult Success(
        Uri baseHost,
        string pathAndQuery)
    {
        return new PathOfExileTradeEndpointBuildResult
        {
            BaseHost = baseHost,
            PathAndQuery = pathAndQuery,
        };
    }

    public static PathOfExileTradeEndpointBuildResult Failure(
        params PathOfExileTradeQueryDiagnostic[] diagnostics)
    {
        return new PathOfExileTradeEndpointBuildResult
        {
            Diagnostics = diagnostics,
        };
    }
}
