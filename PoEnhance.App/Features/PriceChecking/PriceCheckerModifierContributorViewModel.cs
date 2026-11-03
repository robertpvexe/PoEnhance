using PoEnhance.Core.Trade;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PoEnhance.App.Features.PriceChecking;

public sealed record PriceCheckerModifierContributorViewModel : INotifyPropertyChanged
{
    private SearchComponentContributorInactiveReason inactiveReason;
    public required int ParentSourceIndex { get; init; }

    public required int ContributorIndex { get; init; }

    public required string Text { get; init; }

    public string ProvenanceLabel { get; init; } = string.Empty;

    public string? SourceBreakdown { get; init; }

    /// <summary>
    /// When set, this child is a mirrored presentation of a draft-level
    /// <see cref="SharedSearchCriterion"/> (one selection / Min/Max / Trade filter).
    /// </summary>
    public string? SharedCriterionId { get; init; }

    public bool IsSharedCriterionMirror => !string.IsNullOrWhiteSpace(SharedCriterionId);

    public bool IsSelected { get; init; }

    public bool SupportsValueBounds { get; init; }

    public string? ValueBoundsUnsupportedReason { get; init; }

    public bool IsInteractionEnabled { get; init; }

    public IReadOnlyList<PriceCheckerModifierFilterVariantViewModel> FilterVariants { get; init; } = [];

    public PriceCheckerModifierFilterVariantViewModel? SelectedFilterVariant { get; init; }

    public bool HasFilterVariants => FilterVariants.Count > 0;

    public bool HasSingleFilterVariant => FilterVariants.Count == 1;

    public bool HasMultipleFilterVariants => FilterVariants.Count > 1;

    /// <summary>
    /// Real ComboBox whenever official FilterVariants exist — including a single Pseudo-only
    /// shared criterion — so Mod Type chrome matches multi-option rows (not a flat text box).
    /// </summary>
    public bool CanSelectFilterVariant => IsInteractionEnabled && HasFilterVariants;

    public string ModTypeLabel => SelectedFilterVariant?.Label ?? ProvenanceLabel;

    public SearchComponentContributorInactiveReason InactiveReason
    {
        get => inactiveReason;
        set
        {
            if (inactiveReason == value)
            {
                return;
            }

            inactiveReason = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsInactive));
            OnPropertyChanged(nameof(CanEditBounds));
        }
    }

    public bool IsInactive => InactiveReason != SearchComponentContributorInactiveReason.None;

    public bool CanEditBounds => IsInteractionEnabled &&
        !IsInactive &&
        IsSelected &&
        SupportsValueBounds;

    public string MinimumText { get; set; } = string.Empty;

    public string MaximumText { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
