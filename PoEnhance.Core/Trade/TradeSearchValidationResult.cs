namespace PoEnhance.Core.Trade;

public sealed record TradeSearchValidationResult
{
    public IReadOnlyList<TradeSearchValidationDiagnostic> Diagnostics { get; init; } = [];

    public bool HasErrors => Diagnostics.Any(diagnostic =>
        diagnostic.Severity == TradeSearchValidationSeverity.Error);

    public bool IsValid => !HasErrors;

    public static TradeSearchValidationResult FromDiagnostics(
        IReadOnlyList<TradeSearchValidationDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return new TradeSearchValidationResult
        {
            Diagnostics = diagnostics,
        };
    }
}
