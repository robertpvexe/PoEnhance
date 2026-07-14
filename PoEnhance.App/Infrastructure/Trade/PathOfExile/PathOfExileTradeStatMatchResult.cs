using PoEnhance.Core.Items.GameData;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeStatMatchResult
{
    public required PathOfExileTradeStatMatchStatus Status { get; init; }

    public string NormalizedItemTemplate { get; init; } = string.Empty;

    public IReadOnlyList<decimal> ExtractedNumericValues { get; init; } = [];

    public ModifierLocality RequestedLocality { get; init; } = ModifierLocality.Unknown;

    public PathOfExileTradeStatMatchCandidate? ExactCandidate { get; init; }

    public IReadOnlyList<PathOfExileTradeStatMatchCandidate> InitialCandidates { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeStatMatchCandidate> Candidates { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeStatMatchCandidate> RejectedCandidates { get; init; } = [];

    public IReadOnlyList<PathOfExileTradeStatMatchDiagnostic> Diagnostics { get; init; } = [];

    public PathOfExileTradeStatResolutionTrace? Trace { get; init; }
}
