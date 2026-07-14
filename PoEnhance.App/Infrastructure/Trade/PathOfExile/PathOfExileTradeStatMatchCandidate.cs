namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeStatMatchCandidate
{
    public required string StatId { get; init; }

    public required string Text { get; init; }

    public required string NormalizedTemplate { get; init; }

    public string? GroupId { get; init; }

    public string? GroupLabel { get; init; }

    public string? Type { get; init; }

    public IReadOnlyDictionary<string, string> OptionMetadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
