namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeEndpointBuilder
{
    public const int MaximumFetchResultIds = 10;

    public static readonly Uri DefaultBaseHost = new("https://www.pathofexile.com");

    private readonly Uri baseHost;

    public PathOfExileTradeEndpointBuilder()
        : this(DefaultBaseHost)
    {
    }

    public PathOfExileTradeEndpointBuilder(Uri baseHost)
    {
        ArgumentNullException.ThrowIfNull(baseHost);
        this.baseHost = baseHost;
    }

    public PathOfExileTradeEndpointBuildResult BuildSearchEndpoint(
        string? leagueIdentifier)
    {
        var league = TrimToNull(leagueIdentifier);
        if (league is null)
        {
            return Failure(
                PathOfExileTradeEndpointDiagnosticCodes.MissingLeague,
                "A league identifier is required for the Trade search endpoint.");
        }

        return PathOfExileTradeEndpointBuildResult.Success(
            baseHost,
            $"/api/trade/search/{Uri.EscapeDataString(league)}");
    }

    public PathOfExileTradeEndpointBuildResult BuildFetchEndpoint(
        string? queryId,
        IReadOnlyList<string?>? resultIds)
    {
        var trimmedQueryId = TrimToNull(queryId);
        if (trimmedQueryId is null)
        {
            return Failure(
                PathOfExileTradeEndpointDiagnosticCodes.MissingQueryId,
                "A query identifier is required for the Trade fetch endpoint.");
        }

        if (resultIds is null || resultIds.Count == 0)
        {
            return Failure(
                PathOfExileTradeEndpointDiagnosticCodes.EmptyResultBatch,
                "At least one result identifier is required for the Trade fetch endpoint.");
        }

        if (resultIds.Count > MaximumFetchResultIds)
        {
            return Failure(
                PathOfExileTradeEndpointDiagnosticCodes.TooManyResultIds,
                $"At most {MaximumFetchResultIds} result identifiers can be fetched at once.");
        }

        var encodedResultIds = new List<string>(resultIds.Count);
        for (var index = 0; index < resultIds.Count; index++)
        {
            var resultId = TrimToNull(resultIds[index]);
            if (resultId is null)
            {
                return Failure(
                    PathOfExileTradeEndpointDiagnosticCodes.BlankResultId,
                    $"Result identifier at index {index} is empty.");
            }

            encodedResultIds.Add(Uri.EscapeDataString(resultId));
        }

        var path = string.Join(",", encodedResultIds);
        return PathOfExileTradeEndpointBuildResult.Success(
            baseHost,
            $"/api/trade/fetch/{path}?query={Uri.EscapeDataString(trimmedQueryId)}");
    }

    public PathOfExileTradeEndpointBuildResult BuildStatsEndpoint()
    {
        return PathOfExileTradeEndpointBuildResult.Success(
            baseHost,
            "/api/trade/data/stats");
    }

    public PathOfExileTradeEndpointBuildResult BuildItemsEndpoint()
    {
        return PathOfExileTradeEndpointBuildResult.Success(
            baseHost,
            "/api/trade/data/items");
    }

    public PathOfExileTradeEndpointBuildResult BuildFiltersEndpoint()
    {
        return PathOfExileTradeEndpointBuildResult.Success(
            baseHost,
            "/api/trade/data/filters");
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static PathOfExileTradeEndpointBuildResult Failure(
        string code,
        string message)
    {
        return PathOfExileTradeEndpointBuildResult.Failure(
            new PathOfExileTradeQueryDiagnostic(code, message));
    }
}
