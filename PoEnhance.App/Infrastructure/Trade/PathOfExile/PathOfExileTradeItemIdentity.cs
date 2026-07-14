using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeItemIdentity
{
    public required string CanonicalName { get; init; }

    public required string CanonicalType { get; init; }

    public TradeTriState Foulborn { get; init; } = TradeTriState.Auto;
}
