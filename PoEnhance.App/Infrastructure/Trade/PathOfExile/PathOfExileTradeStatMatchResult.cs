namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeStatMatchResult
{
    public required PathOfExileTradeStatMatchStatus Status { get; init; }

    public string NormalizedItemTemplate { get; init; } = string.Empty;

    public IReadOnlyList<decimal> ExtractedNumericValues { get; init; } = [];

    public PathOfExileTradeStatMatchCandidate? ExactCandidate { get; init; }

    public IReadOnlyList<PathOfExileTradeStatMatchCandidate> Candidates { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeStatMatchDiagnostic> Diagnostics { get; init; } = [];
}
