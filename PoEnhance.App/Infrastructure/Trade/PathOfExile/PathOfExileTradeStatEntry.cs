namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeStatEntry
{
    public required int ProviderOrder { get; init; }

    public string? GroupId { get; init; }

    public string? GroupLabel { get; init; }

    public required string Id { get; init; }

    public required string Text { get; init; }

    public string? Type { get; init; }

    public IReadOnlyDictionary<string, string> OptionMetadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
