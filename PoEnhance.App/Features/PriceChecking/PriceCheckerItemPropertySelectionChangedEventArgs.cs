namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerItemPropertySelectionChangedEventArgs(
    int itemPropertyIndex,
    bool isSelected) : EventArgs
{
    public int ItemPropertyIndex { get; } = itemPropertyIndex;

    public bool IsSelected { get; } = isSelected;
}
