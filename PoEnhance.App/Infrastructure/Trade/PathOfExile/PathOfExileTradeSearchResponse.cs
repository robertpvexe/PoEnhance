namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeSearchResponse
{
    public required string Id { get; init; }

    public required IReadOnlyList<string> Result { get; init; }

    public required int Total { get; init; }

    public bool? Inexact { get; init; }
}
