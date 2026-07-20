using PoEnhance.App.Infrastructure.Shortcuts;

namespace PoEnhance.App.Infrastructure.Settings;

internal static class QuickUseSettingsValidator
{
    private const ShortcutModifiers AllowedModifiers =
        ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Shift;

    private static readonly HashSet<ShortcutBinding> ReservedBindings =
    [
        ShortcutBinding.DefaultPriceChecker,
        ShortcutBinding.MultitoolMenu,
        ShortcutBinding.DeveloperWindow,
    ];

    public static bool TryValidate(
        IReadOnlyList<QuickUseCommandSetting> commands,
        out string validationError)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var bindings = new HashSet<ShortcutBinding>();

        foreach (var command in commands)
        {
            if (command.Hotkey is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(command.Command))
            {
                validationError = "Enter a command before assigning its hotkey.";
                return false;
            }

            if ((command.Hotkey.Modifiers & ~AllowedModifiers) != 0)
            {
                validationError = "Quick Use hotkeys may use only Ctrl, Alt, or Shift modifiers.";
                return false;
            }

            if (ReservedBindings.Contains(command.Hotkey))
            {
                validationError = $"{command.Hotkey.ToCompactString()} is reserved by PoEnhance.";
                return false;
            }

            if (!bindings.Add(command.Hotkey))
            {
                validationError = $"{command.Hotkey.ToCompactString()} is already assigned to another Quick Use command.";
                return false;
            }
        }

        validationError = string.Empty;
        return true;
    }
}
