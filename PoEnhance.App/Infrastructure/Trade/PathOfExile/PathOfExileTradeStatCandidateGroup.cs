namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeStatCandidateGroup
{
    public required PathOfExileTradeStatCandidateGroupKey Key { get; init; }

    public IReadOnlyList<PathOfExileTradeStatMatchCandidate> Candidates { get; init; } = [];
}
