using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerRequestedItemFilterValueChangedEventArgs(
    TradeSearchRequestedItemFilterKind kind,
    string text) : EventArgs
{
    public TradeSearchRequestedItemFilterKind Kind { get; } = kind;

    public string Text { get; } = text;
}
