using System.ComponentModel;
using System.Runtime.CompilerServices;
using PoEnhance.App.Infrastructure.Settings;
using PoEnhance.App.Infrastructure.Shortcuts;

namespace PoEnhance.App.Shell;

internal sealed class QuickUseCommandRow : INotifyPropertyChanged
{
    private string command;
    private bool pressEnter;
    private ShortcutBinding? hotkey;
    private bool isCapturing;

    public QuickUseCommandRow(QuickUseCommandSetting setting)
    {
        command = setting.Command;
        pressEnter = setting.PressEnter;
        hotkey = setting.Hotkey;
        IsCustom = setting.IsCustom;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Command
    {
        get => command;
        set
        {
            if (command == value)
            {
                return;
            }

            command = value;
            OnPropertyChanged();
        }
    }

    public bool PressEnter
    {
        get => pressEnter;
        set
        {
            if (pressEnter == value)
            {
                return;
            }

            pressEnter = value;
            OnPropertyChanged();
        }
    }

    public ShortcutBinding? Hotkey
    {
        get => hotkey;
        set
        {
            if (hotkey == value)
            {
                return;
            }

            hotkey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HotkeyLabel));
        }
    }

    public bool IsCustom { get; }

    public bool IsCapturing
    {
        get => isCapturing;
        set
        {
            if (isCapturing == value)
            {
                return;
            }

            isCapturing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HotkeyLabel));
        }
    }

    public string HotkeyLabel => IsCapturing
        ? "Press a key..."
        : Hotkey?.ToCompactString() ?? "Not set";

    public QuickUseCommandSetting ToSetting()
    {
        return new QuickUseCommandSetting(Command, PressEnter, Hotkey, IsCustom);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
