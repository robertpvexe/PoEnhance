using PoEnhance.App.Infrastructure.Shortcuts;

namespace PoEnhance.App.Infrastructure.Settings;

internal sealed record QuickUseCommandSetting(
    string Command,
    bool PressEnter,
    ShortcutBinding? Hotkey,
    bool IsCustom)
{
    public static IReadOnlyList<QuickUseCommandSetting> CreateDefaults()
    {
        return
        [
            new("/hideout", true, new ShortcutBinding(ShortcutKey.F5, ShortcutModifiers.None), false),
            new("/kingsmarch", true, new ShortcutBinding(ShortcutKey.F6, ShortcutModifiers.None), false),
            new("/monastery of the keepers", true, new ShortcutBinding(ShortcutKey.F7, ShortcutModifiers.None), false),
            new(string.Empty, true, null, true),
            new(string.Empty, true, null, true),
            new(string.Empty, true, null, true),
        ];
    }
}
