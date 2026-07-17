namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerItemPropertyBoundsChangedEventArgs(
    int itemPropertyIndex,
    string? minimumText,
    string? maximumText) : EventArgs
{
    public int ItemPropertyIndex { get; } = itemPropertyIndex;

    public string MinimumText { get; } = minimumText ?? string.Empty;

    public string MaximumText { get; } = maximumText ?? string.Empty;
}
