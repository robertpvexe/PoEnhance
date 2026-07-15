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

    public bool IsSelected { get; init; }

    public bool SupportsValueBounds { get; init; }

    public string? ValueBoundsUnsupportedReason { get; init; }

    public bool IsInteractionEnabled { get; init; }

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
