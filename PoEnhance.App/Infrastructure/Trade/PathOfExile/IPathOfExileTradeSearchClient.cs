namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeSearchClient
{
    Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
        PathOfExileTradeSearchRequest? request,
        string? leagueIdentifier,
        CancellationToken cancellationToken = default);
}
