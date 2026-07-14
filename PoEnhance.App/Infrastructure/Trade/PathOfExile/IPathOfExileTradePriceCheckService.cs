using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradePriceCheckService
{
    Task<PathOfExileTradePriceCheckResult> CheckAsync(
        TradeSearchDraft? draft,
        TradeSearchValidationResult? validationResult,
        string? leagueIdentifier,
        CancellationToken cancellationToken = default);

    Task<PathOfExileTradePriceCheckResult> FetchMoreAsync(
        string? searchQueryId,
        IReadOnlyList<string?>? resultIds,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PathOfExileTradePriceCheckResult
        {
            Stage = PathOfExileTradePriceCheckStage.Fetch,
            Diagnostics =
            [
                new PathOfExileTradePriceCheckDiagnostic(
                    PathOfExileTradePriceCheckDiagnosticCodes.FetchFailed,
                    "Loading more Trade offers is not available.",
                    PathOfExileTradePriceCheckStage.Fetch),
            ],
        });
    }
}
