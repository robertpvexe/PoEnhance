namespace PoEnhance.Core.Trade;

public sealed record TradeItemVariantCriteria
{
    public TradeTriState Foulborn { get; init; } = TradeTriState.Auto;
}
