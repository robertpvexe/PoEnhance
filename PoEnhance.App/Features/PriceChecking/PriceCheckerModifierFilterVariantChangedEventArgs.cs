namespace PoEnhance.App.Features.PriceChecking;

public sealed class PriceCheckerModifierFilterVariantChangedEventArgs(
    int modifierIndex,
    string variantIdentity) : EventArgs
{
    public int ModifierIndex { get; } = modifierIndex;

    public string VariantIdentity { get; } = variantIdentity ?? string.Empty;
}
