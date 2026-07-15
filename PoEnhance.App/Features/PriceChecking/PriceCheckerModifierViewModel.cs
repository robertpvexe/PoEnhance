namespace PoEnhance.App.Features.PriceChecking;

public sealed record PriceCheckerModifierViewModel
{
    public required int SourceIndex { get; init; }

    public required string Text { get; init; }

    public string SectionLabel { get; init; } = string.Empty;

    public bool IsSelected { get; init; }

    public bool SupportsValueBounds { get; init; }

    public string? ValueBoundsUnsupportedReason { get; init; }

    public bool CanEditBounds => IsSelected && SupportsValueBounds;

    public IReadOnlyList<PriceCheckerModifierFilterVariantViewModel> FilterVariants { get; init; } = [];

    public PriceCheckerModifierFilterVariantViewModel? SelectedFilterVariant { get; init; }

    public bool HasSingleFilterVariant => FilterVariants.Count == 1;

    public bool HasMultipleFilterVariants => FilterVariants.Count > 1;

    public bool CanSelectFilterVariant => IsSelected && HasMultipleFilterVariants;

    public string MinimumText { get; set; } = string.Empty;

    public string MaximumText { get; set; } = string.Empty;
}
