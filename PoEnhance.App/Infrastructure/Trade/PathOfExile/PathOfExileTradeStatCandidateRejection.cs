namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeStatCandidateRejection
{
    public required PathOfExileTradeStatMatchCandidate Candidate { get; init; }

    public required string Reason { get; init; }
}
