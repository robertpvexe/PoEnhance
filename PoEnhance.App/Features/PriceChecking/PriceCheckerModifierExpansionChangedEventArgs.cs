namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerModifierExpansionChangedEventArgs(
    int modifierIndex,
    bool isExpanded) : EventArgs
{
    public int ModifierIndex { get; } = modifierIndex;

    public bool IsExpanded { get; } = isExpanded;
}
