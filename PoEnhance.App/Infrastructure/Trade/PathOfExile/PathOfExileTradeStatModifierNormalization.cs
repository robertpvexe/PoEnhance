namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeStatModifierNormalization
{
    public required string NormalizedTemplate { get; init; }

    public IReadOnlyList<decimal> ExtractedNumericValues { get; init; } = [];

    public PathOfExileTradeStatMatchDiagnostic? Diagnostic { get; init; }
}
