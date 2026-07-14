namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeStatCandidateGroupKey
{
    public required string NormalizedTemplate { get; init; }

    public required string ProviderKind { get; init; }

    public override string ToString()
    {
        return $"{ProviderKind}:{NormalizedTemplate}";
    }
}
