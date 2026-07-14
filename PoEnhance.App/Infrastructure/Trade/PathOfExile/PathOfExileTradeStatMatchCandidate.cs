namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeStatMatchCandidate
{
    public int ProviderOrder { get; init; }

    public required string StatId { get; init; }

    public required string Text { get; init; }

    public required string NormalizedTemplate { get; init; }

    public required string LookupTemplate { get; init; }

    public string? GroupId { get; init; }

    public string? GroupLabel { get; init; }

    public string? Type { get; init; }

    public required string ProviderKind { get; init; }

    public PathOfExileTradeProviderStatLocality ProviderLocality { get; init; } =
        PathOfExileTradeProviderStatLocality.Unmarked;

    public IReadOnlyDictionary<string, string> OptionMetadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
