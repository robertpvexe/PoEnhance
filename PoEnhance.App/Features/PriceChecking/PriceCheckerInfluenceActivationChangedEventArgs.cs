using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerInfluenceActivationChangedEventArgs(
    TradeSearchInfluenceKind kind,
    bool isEnabled) : EventArgs
{
    public TradeSearchInfluenceKind Kind { get; } = kind;

    public bool IsEnabled { get; } = isEnabled;
}
