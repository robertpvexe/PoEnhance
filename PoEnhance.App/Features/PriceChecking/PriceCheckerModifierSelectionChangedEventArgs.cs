namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerModifierSelectionChangedEventArgs : EventArgs
{
    public PriceCheckerModifierSelectionChangedEventArgs(
        int modifierIndex,
        bool isSelected)
    {
        ModifierIndex = modifierIndex;
        IsSelected = isSelected;
    }

    public int ModifierIndex { get; }

    public bool IsSelected { get; }
}
