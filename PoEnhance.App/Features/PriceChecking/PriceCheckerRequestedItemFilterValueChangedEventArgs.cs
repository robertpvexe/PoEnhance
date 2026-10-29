using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerRequestedItemFilterValueChangedEventArgs : EventArgs
{
    public PriceCheckerRequestedItemFilterValueChangedEventArgs(
        TradeSearchRequestedItemFilterKind kind,
        string text)
        : this(kind, text, colour: null)
    {
    }

    public PriceCheckerRequestedItemFilterValueChangedEventArgs(
        TradeSearchRequestedItemFilterKind kind,
        string text,
        TradeSearchRequestedItemFilterColour? colour)
    {
        Kind = kind;
        Text = text;
        Colour = colour;
    }

    public TradeSearchRequestedItemFilterKind Kind { get; }

    public string Text { get; }

    public TradeSearchRequestedItemFilterColour? Colour { get; }
}
