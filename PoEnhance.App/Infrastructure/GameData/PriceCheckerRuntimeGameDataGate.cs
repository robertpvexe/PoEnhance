using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.GameData;

/// <summary>
/// Gates Price Checker draft construction on Runtime GameData readiness after clipboard text
/// has already been captured. Does not read the clipboard.
/// </summary>
internal static class PriceCheckerRuntimeGameDataGate
{
    public const string WaitingForGameDataStatus = "Waiting for game data";

    public static async Task<PriceCheckerRuntimeGameDataGateResult> EnsureCatalogReadyAsync(
        RuntimeGameDataService runtimeGameDataService,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeGameDataService);

        var status = await runtimeGameDataService
            .WaitForLoadCompletionAsync(cancellationToken)
            .ConfigureAwait(false);

        if (status.State == RuntimeGameDataState.Loaded && status.Catalog is not null)
        {
            return PriceCheckerRuntimeGameDataGateResult.Ready(status);
        }

        return PriceCheckerRuntimeGameDataGateResult.Unavailable(status);
    }
}

internal sealed record PriceCheckerRuntimeGameDataGateResult
{
    public bool IsReady { get; private init; }

    public GameDataCatalog? Catalog { get; private init; }

    public RuntimeGameDataStatus Status { get; private init; } = new();

    public string UserFacingStatus { get; private init; } = string.Empty;

    public static PriceCheckerRuntimeGameDataGateResult Ready(RuntimeGameDataStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(status.Catalog);

        return new PriceCheckerRuntimeGameDataGateResult
        {
            IsReady = true,
            Catalog = status.Catalog,
            Status = status,
            UserFacingStatus = "Game data ready",
        };
    }

    public static PriceCheckerRuntimeGameDataGateResult Unavailable(RuntimeGameDataStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        var message = status.State switch
        {
            RuntimeGameDataState.Failed =>
                string.IsNullOrWhiteSpace(status.FailureMessage)
                    ? "Game data failed to load"
                    : status.FailureMessage,
            RuntimeGameDataState.NotConfigured =>
                string.IsNullOrWhiteSpace(status.FailureMessage)
                    ? "Game data not loaded"
                    : status.FailureMessage,
            _ => "Game data unavailable",
        };

        return new PriceCheckerRuntimeGameDataGateResult
        {
            IsReady = false,
            Catalog = null,
            Status = status,
            UserFacingStatus = message,
        };
    }
}
