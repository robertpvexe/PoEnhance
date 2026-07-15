namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerModifierSelectionChangedEventArgs : EventArgs
{
    public PriceCheckerModifierSelectionChangedEventArgs(
        int modifierIndex,
        bool isSelected,
        int? contributorIndex = null)
    {
        ModifierIndex = modifierIndex;
        IsSelected = isSelected;
        ContributorIndex = contributorIndex;
    }

    public int ModifierIndex { get; }

    public bool IsSelected { get; }

    public int? ContributorIndex { get; }
}
