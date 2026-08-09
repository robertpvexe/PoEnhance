using PoEnhance.App.Infrastructure.Settings;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests;

internal sealed class TestTradeLeagueResolver : IPathOfExileTradeLeagueResolver
{
    private readonly Func<ApplicationLeagueSelection?, CancellationToken,
        Task<PathOfExileTradeLeagueResolutionResult>> resolve;

    public TestTradeLeagueResolver()
        : this((selection, _) => Task.FromResult(selection is null
            ? new PathOfExileTradeLeagueResolutionResult
            {
                Diagnostics =
                [
                    new PathOfExileTradeHttpDiagnostic(
                        PathOfExileTradeLeaguesDiagnosticCodes.SelectionMissing,
                        "Select a league."),
                ],
            }
            : new PathOfExileTradeLeagueResolutionResult
            {
                League = new PathOfExileTradeLeagueEntry(
                    selection.ProviderId ?? selection.DisplayText,
                    selection.DisplayText,
                    "pc",
                    0),
            }))
    {
    }

    public TestTradeLeagueResolver(
        Func<ApplicationLeagueSelection?, CancellationToken,
            Task<PathOfExileTradeLeagueResolutionResult>> resolve)
    {
        this.resolve = resolve;
    }

    public Task<PathOfExileTradeLeagueResolutionResult> ResolveAsync(
        ApplicationLeagueSelection? selection,
        CancellationToken cancellationToken = default) => resolve(selection, cancellationToken);
}

internal sealed class TestTradeLeagueCatalogProvider : IPathOfExileTradeLeagueCatalogProvider
{
    private readonly PathOfExileTradeLeagueCatalogProviderResult result;

    public TestTradeLeagueCatalogProvider(params PathOfExileTradeLeagueEntry[] entries)
    {
        var now = DateTimeOffset.UtcNow;
        result = new PathOfExileTradeLeagueCatalogProviderResult
        {
            Catalog = new PathOfExileTradeLeagueCatalog(
                entries,
                now,
                now.AddMinutes(2)),
        };
    }

    public TestTradeLeagueCatalogProvider(PathOfExileTradeLeagueCatalogProviderResult result)
    {
        this.result = result;
    }

    public Task<PathOfExileTradeLeagueCatalogProviderResult> GetCatalogAsync(
        CancellationToken cancellationToken = default) => Task.FromResult(result);
}
