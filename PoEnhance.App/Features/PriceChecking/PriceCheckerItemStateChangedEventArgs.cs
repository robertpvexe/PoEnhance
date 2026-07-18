using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerItemStateChangedEventArgs(
    TradeItemStateKind kind,
    TradeTriState state) : EventArgs
{
    public TradeItemStateKind Kind { get; } = kind;

    public TradeTriState State { get; } = state;
}
