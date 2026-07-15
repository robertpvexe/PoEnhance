namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerModifierBoundsChangedEventArgs : EventArgs
{
    public PriceCheckerModifierBoundsChangedEventArgs(int modifierIndex, string? minimumText, string? maximumText)
    {
        ModifierIndex = modifierIndex;
        MinimumText = minimumText ?? string.Empty;
        MaximumText = maximumText ?? string.Empty;
    }

    public int ModifierIndex { get; }

    public string MinimumText { get; }

    public string MaximumText { get; }
}
