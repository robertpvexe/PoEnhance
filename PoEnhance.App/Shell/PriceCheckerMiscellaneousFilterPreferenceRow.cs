using System.ComponentModel;
using System.Runtime.CompilerServices;
using PoEnhance.App.Infrastructure.Settings;

namespace PoEnhance.App.Shell;

internal sealed class PriceCheckerMiscellaneousFilterPreferenceRow : INotifyPropertyChanged
{
    private bool isVisible;
    private bool canMoveUp = true;
    private bool canMoveDown = true;

    public PriceCheckerMiscellaneousFilterPreferenceRow(
        PriceCheckerMiscellaneousFilterDefinition definition,
        bool isVisible)
    {
        Key = definition.Key;
        DisplayName = definition.DisplayName;
        Description = definition.Description;
        CanToggleVisibility = definition.CanToggleVisibility;
        this.isVisible = definition.CanToggleVisibility ? isVisible : true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public bool CanToggleVisibility { get; }

    public bool IsVisible
    {
        get => isVisible;
        set
        {
            var normalized = CanToggleVisibility ? value : true;
            if (isVisible == normalized)
            {
                return;
            }

            isVisible = normalized;
            OnPropertyChanged();
        }
    }

    public bool CanMoveUp
    {
        get => canMoveUp;
        set
        {
            if (canMoveUp == value)
            {
                return;
            }

            canMoveUp = value;
            OnPropertyChanged();
        }
    }

    public bool CanMoveDown
    {
        get => canMoveDown;
        set
        {
            if (canMoveDown == value)
            {
                return;
            }

            canMoveDown = value;
            OnPropertyChanged();
        }
    }

    public PriceCheckerMiscellaneousFilterPreference ToPreference(int order)
    {
        return new PriceCheckerMiscellaneousFilterPreference(
            Key,
            CanToggleVisibility ? IsVisible : true,
            order);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
