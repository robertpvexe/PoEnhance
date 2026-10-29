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
        this.isVisible = isVisible;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public bool IsVisible
    {
        get => isVisible;
        set
        {
            if (isVisible == value)
            {
                return;
            }

            isVisible = value;
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
        return new PriceCheckerMiscellaneousFilterPreference(Key, IsVisible, order);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
