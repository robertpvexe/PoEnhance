namespace PoEnhance.App.Infrastructure.Shortcuts;

internal sealed record ShortcutBinding(ShortcutKey PrimaryKey, ShortcutModifiers Modifiers)
{
    public static ShortcutBinding DefaultPriceChecker { get; } =
        new(ShortcutKey.D, ShortcutModifiers.Control);

    public static ShortcutBinding DeveloperWindow { get; } =
        new(
            ShortcutKey.OemBackslash,
            ShortcutModifiers.Control | ShortcutModifiers.Shift);

    public static IReadOnlyList<ShortcutBinding> DevelopmentChoices { get; } =
    [
        DefaultPriceChecker,
        new(ShortcutKey.X, ShortcutModifiers.None),
        new(ShortcutKey.F6, ShortcutModifiers.None),
        new(ShortcutKey.F7, ShortcutModifiers.None),
        new(ShortcutKey.F8, ShortcutModifiers.None),
        new(ShortcutKey.F9, ShortcutModifiers.None),
        new(ShortcutKey.F10, ShortcutModifiers.None),
        new(ShortcutKey.F11, ShortcutModifiers.None),
        new(ShortcutKey.F12, ShortcutModifiers.None),
    ];

    public override string ToString()
    {
        var parts = new List<string>();

        if (Modifiers.HasFlag(ShortcutModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(ShortcutModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(ShortcutModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(ShortcutModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(PrimaryKey == ShortcutKey.OemBackslash ? "\\" : PrimaryKey.ToString());

        return string.Join(" + ", parts);
    }
}
