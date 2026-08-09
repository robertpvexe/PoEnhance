using PoEnhance.App.Infrastructure.Settings;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeLeagueResolver : IPathOfExileTradeLeagueResolver
{
    private readonly IPathOfExileTradeLeagueCatalogProvider catalogProvider;

    public PathOfExileTradeLeagueResolver(IPathOfExileTradeLeagueCatalogProvider catalogProvider)
    {
        this.catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
    }

    public async Task<PathOfExileTradeLeagueResolutionResult> ResolveAsync(
        ApplicationLeagueSelection? selection,
        CancellationToken cancellationToken = default)
    {
        if (selection is null || string.IsNullOrWhiteSpace(selection.DisplayText))
        {
            return Failure(
                PathOfExileTradeLeaguesDiagnosticCodes.SelectionMissing,
                "Select a Trade league in Settings before searching.");
        }

        var catalogResult = await catalogProvider.GetCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!catalogResult.IsSuccess || catalogResult.Catalog is null)
        {
            return new PathOfExileTradeLeagueResolutionResult
            {
                IsCancelled = catalogResult.IsCancelled,
                Diagnostics = catalogResult.Diagnostics.Count > 0
                    ? catalogResult.Diagnostics
                    :
                    [
                        new PathOfExileTradeHttpDiagnostic(
                            PathOfExileTradeLeaguesDiagnosticCodes.CatalogUnavailable,
                            "The official Trade league catalog is unavailable. Try again later."),
                    ],
            };
        }

        var providerId = TrimToNull(selection.ProviderId);
        var displayText = selection.DisplayText.Trim();
        var matches = catalogResult.Catalog.PcEntries
            .Where(entry => providerId is not null
                ? string.Equals(entry.ProviderId, providerId, StringComparison.Ordinal)
                : string.Equals(entry.ProviderId, displayText, StringComparison.Ordinal) ||
                  string.Equals(entry.DisplayText, displayText, StringComparison.Ordinal))
            .ToArray();

        if (matches.Length == 1)
        {
            return new PathOfExileTradeLeagueResolutionResult { League = matches[0] };
        }

        return matches.Length == 0
            ? Failure(
                PathOfExileTradeLeaguesDiagnosticCodes.SelectionNotFound,
                "The selected league is not present in the current PC Trade catalog. Select a league in Settings.")
            : Failure(
                PathOfExileTradeLeaguesDiagnosticCodes.SelectionAmbiguous,
                "The selected league is ambiguous in the current PC Trade catalog. Select it again in Settings.");
    }

    private static PathOfExileTradeLeagueResolutionResult Failure(string code, string message) =>
        new() { Diagnostics = [new PathOfExileTradeHttpDiagnostic(code, message)] };

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
