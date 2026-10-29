using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerRequestedItemFilterValueChangedEventArgs : EventArgs
{
    public PriceCheckerRequestedItemFilterValueChangedEventArgs(
        TradeSearchRequestedItemFilterKind kind,
        string text)
        : this(kind, text, colour: null, bound: null)
    {
    }

    public PriceCheckerRequestedItemFilterValueChangedEventArgs(
        TradeSearchRequestedItemFilterKind kind,
        string text,
        TradeSearchRequestedItemFilterColour? colour)
        : this(kind, text, colour, bound: null)
    {
    }

    public PriceCheckerRequestedItemFilterValueChangedEventArgs(
        TradeSearchRequestedItemFilterKind kind,
        string text,
        TradeSearchRequestedItemFilterBound bound)
        : this(kind, text, colour: null, bound)
    {
    }

    public PriceCheckerRequestedItemFilterValueChangedEventArgs(
        TradeSearchRequestedItemFilterKind kind,
        string text,
        TradeSearchRequestedItemFilterColour? colour,
        TradeSearchRequestedItemFilterBound? bound)
    {
        Kind = kind;
        Text = text;
        Colour = colour;
        Bound = bound;
    }

    public TradeSearchRequestedItemFilterKind Kind { get; }

    public string Text { get; }

    public TradeSearchRequestedItemFilterColour? Colour { get; }

    public TradeSearchRequestedItemFilterBound? Bound { get; }
}
