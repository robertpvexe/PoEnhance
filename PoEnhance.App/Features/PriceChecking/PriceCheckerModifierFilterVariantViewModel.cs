namespace PoEnhance.App.Features.PriceChecking;

public sealed record PriceCheckerModifierFilterVariantViewModel
{
    public required string Identity { get; init; }

    public required string Label { get; init; }

    public required string Description { get; init; }

    public bool SupportsValueBounds { get; init; }
}
