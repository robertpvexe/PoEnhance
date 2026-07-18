using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed record PriceCheckerDeveloperDiagnostic(string Code, string Message);

internal sealed record PriceCheckerDeveloperDiagnosticsSnapshot(
    string State,
    IReadOnlyList<PriceCheckerDeveloperDiagnostic> Diagnostics)
{
    public static PriceCheckerDeveloperDiagnosticsSnapshot Idle { get; } = new("Idle", []);

    public PriceCheckerDeveloperDiagnostic? LatestDiagnostic => Diagnostics.LastOrDefault();

    public IReadOnlyList<PriceCheckerDeveloperDiagnostic> UserFacingDiagnostics => Diagnostics
        .Where(diagnostic => diagnostic.Code != TradeSearchDraftDiagnosticCodes.ModifierAggregationSkipped)
        .ToArray();

    public PriceCheckerDeveloperDiagnostic? LatestUserFacingDiagnostic =>
        UserFacingDiagnostics.LastOrDefault();
}
