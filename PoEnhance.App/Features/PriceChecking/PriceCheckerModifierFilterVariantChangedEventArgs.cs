namespace PoEnhance.App.Features.PriceChecking;

public sealed class PriceCheckerModifierFilterVariantChangedEventArgs(
    int modifierIndex,
    string variantIdentity,
    int? contributorIndex = null) : EventArgs
{
    public int ModifierIndex { get; } = modifierIndex;

    public string VariantIdentity { get; } = variantIdentity ?? string.Empty;

    public int? ContributorIndex { get; } = contributorIndex;
}
