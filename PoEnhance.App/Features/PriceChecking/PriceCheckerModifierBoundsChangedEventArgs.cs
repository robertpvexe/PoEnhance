namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerModifierBoundsChangedEventArgs : EventArgs
{
    public PriceCheckerModifierBoundsChangedEventArgs(
        int modifierIndex,
        string? minimumText,
        string? maximumText,
        int? contributorIndex = null)
    {
        ModifierIndex = modifierIndex;
        MinimumText = minimumText ?? string.Empty;
        MaximumText = maximumText ?? string.Empty;
        ContributorIndex = contributorIndex;
    }

    public int ModifierIndex { get; }

    public string MinimumText { get; }

    public string MaximumText { get; }

    public int? ContributorIndex { get; }
}
