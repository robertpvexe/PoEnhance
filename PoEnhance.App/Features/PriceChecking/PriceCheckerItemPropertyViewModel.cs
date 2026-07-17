using System.ComponentModel;
using System.Runtime.CompilerServices;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

public sealed record PriceCheckerItemPropertyViewModel : INotifyPropertyChanged
{
    private string minimumText = string.Empty;
    private string maximumText = string.Empty;

    public required int SourceIndex { get; init; }

    public required TradeSearchItemPropertyKind Kind { get; init; }

    public required string Label { get; init; }

    public string? CalculationBasisLabel { get; init; }

    public bool HasCalculationBasisLabel => !string.IsNullOrWhiteSpace(CalculationBasisLabel);

    public bool IsSelected { get; init; }

    public bool IsAvailable { get; init; }

    public string? AvailabilityReason { get; init; }

    public bool CanEditBounds => IsAvailable && IsSelected;

    public bool IsExpanded { get; init; }

    public IReadOnlyList<PriceCheckerModifierViewModel> Children { get; init; } = [];

    public bool HasChildren => Children.Count > 0;

    public int SelectedChildCount => Children.Count(child => child.IsSelected);

    public bool HasSelectedChildren => SelectedChildCount > 0;

    public string SelectedChildSummary => SelectedChildCount == 1
        ? "1 child selected"
        : $"{SelectedChildCount} children selected";

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
