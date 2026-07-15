namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerOfferCapacityChangedEventArgs(int capacity) : EventArgs
{
    public int Capacity { get; } = Math.Max(0, capacity);
}
