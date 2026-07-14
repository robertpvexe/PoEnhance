namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeItemEntry
{
    public required int ProviderOrder { get; init; }

    public string? GroupId { get; init; }

    public string? GroupLabel { get; init; }

    public string? Name { get; init; }

    public required string Type { get; init; }

    public bool IsUnique { get; init; }
}
