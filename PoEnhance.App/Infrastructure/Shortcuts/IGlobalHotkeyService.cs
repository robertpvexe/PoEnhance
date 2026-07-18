using System.Windows;

namespace PoEnhance.App.Infrastructure.Shortcuts;

internal interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? Triggered;

    ShortcutBinding SelectedShortcut { get; }

    ShortcutRegistrationState RegistrationState { get; }

    bool RequiresPathOfExileForeground { get; }

    bool SuppressesKeyRepeat { get; }

    void Attach(Window window);

    void SetShortcut(ShortcutBinding shortcut);

    void UpdatePathOfExileForegroundState(bool isForeground);
}
