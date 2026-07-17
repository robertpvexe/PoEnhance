using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerRequestedItemFilterActivationChangedEventArgs(
    TradeSearchRequestedItemFilterKind kind,
    bool isActive) : EventArgs
{
    public TradeSearchRequestedItemFilterKind Kind { get; } = kind;

    public bool IsActive { get; } = isActive;
}
