using PoEnhance.App.Infrastructure.Settings;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeLeagueResolver
{
    Task<PathOfExileTradeLeagueResolutionResult> ResolveAsync(
        ApplicationLeagueSelection? selection,
        CancellationToken cancellationToken = default);
}
