namespace PoEnhance.App.Features.PriceChecking;

internal sealed record PriceCheckerDeveloperDiagnostic(string Code, string Message);

internal sealed record PriceCheckerDeveloperDiagnosticsSnapshot(
    string State,
    IReadOnlyList<PriceCheckerDeveloperDiagnostic> Diagnostics)
{
    public static PriceCheckerDeveloperDiagnosticsSnapshot Idle { get; } = new("Idle", []);

    public PriceCheckerDeveloperDiagnostic? LatestDiagnostic => Diagnostics.LastOrDefault();
}
