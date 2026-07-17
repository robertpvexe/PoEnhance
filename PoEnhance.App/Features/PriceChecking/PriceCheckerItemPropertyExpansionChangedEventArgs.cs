namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerItemPropertyExpansionChangedEventArgs(
    int itemPropertyIndex,
    bool isExpanded) : EventArgs
{
    public int ItemPropertyIndex { get; } = itemPropertyIndex;

    public bool IsExpanded { get; } = isExpanded;
}
