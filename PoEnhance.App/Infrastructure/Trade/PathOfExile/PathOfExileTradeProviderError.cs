namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeProviderError
{
    public required string Code { get; init; }

    public required string Message { get; init; }
}
