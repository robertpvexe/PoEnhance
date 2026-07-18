using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PoEnhance.App.Features.PriceChecking;

public sealed record PriceCheckerModifierViewModel : INotifyPropertyChanged
{
    private string minimumText = string.Empty;
    private string maximumText = string.Empty;

    public required int SourceIndex { get; init; }

    public required string Text { get; init; }

    public string SectionLabel { get; init; } = string.Empty;

    public int SourceCount { get; init; } = 1;

    public string? SourceBreakdown { get; init; }

    public bool IsSelected { get; init; }

    public bool IsInteractionEnabled { get; init; } = true;

    public string? AvailabilityReason { get; init; }

    public bool SupportsValueBounds { get; init; }

    public string? ValueBoundsUnsupportedReason { get; init; }

    public bool CanEditBounds => IsSelected && SupportsValueBounds;

    public IReadOnlyList<PriceCheckerModifierFilterVariantViewModel> FilterVariants { get; init; } = [];

    public PriceCheckerModifierFilterVariantViewModel? SelectedFilterVariant { get; init; }

    public bool IsCanonicalImplicit { get; init; }

    public bool IsUniqueModifier { get; init; }

    public bool IsFoulbornUniqueModifier { get; init; }

    public bool IsFracturedModifier { get; init; }

    public bool IsVeiledModifier { get; init; }

    public bool HasStaticModType =>
        IsCanonicalImplicit || IsUniqueModifier || IsVeiledModifier;

    public string ModTypeLabel => IsCanonicalImplicit
        ? "Implicit"
        : IsVeiledModifier
                ? "Veiled"
                : IsFoulbornUniqueModifier
                    ? "Foulborn"
                    : IsUniqueModifier
                        ? "Unique"
                        : SelectedFilterVariant?.Label ??
                            (IsInteractionEnabled ? string.Empty : "Unsupported");

    public bool HasSingleFilterVariant => FilterVariants.Count == 1;

    public bool HasMultipleFilterVariants => FilterVariants.Count > 1;

    public bool CanSelectFilterVariant => !HasStaticModType && IsSelected && HasMultipleFilterVariants;

    public IReadOnlyList<PriceCheckerModifierContributorViewModel> Contributors { get; init; } = [];

    public bool HasContributors => Contributors.Count > 0;

    public bool ShowsExpansionControl { get; init; }

    public bool IsExpanded { get; init; }

    public bool ContributorsVisible => HasContributors && (!ShowsExpansionControl || IsExpanded);

    public int ActiveContributorCount { get; init; }

    public string MinimumText
    {
        get => minimumText;
        set => SetField(ref minimumText, value);
    }

    public string MaximumText
    {
        get => maximumText;
        set => SetField(ref maximumText, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        value ??= string.Empty;
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
