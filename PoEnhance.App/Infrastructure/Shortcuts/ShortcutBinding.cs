namespace PoEnhance.App.Infrastructure.Shortcuts;

internal sealed record ShortcutBinding(ShortcutKey PrimaryKey, ShortcutModifiers Modifiers)
{
    public static ShortcutBinding DefaultPriceChecker { get; } =
        new(ShortcutKey.D, ShortcutModifiers.Control);

    public static ShortcutBinding DeveloperWindow { get; } =
        new(
            ShortcutKey.OemBackslash,
            ShortcutModifiers.Control | ShortcutModifiers.Shift);

    public static ShortcutBinding MultitoolMenu { get; } =
        new(ShortcutKey.OemBackslash, ShortcutModifiers.None);

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

        parts.Add(PrimaryKeyLabel());

        return string.Join(" + ", parts);
    }

    public string ToCompactString()
    {
        return ToString().Replace(" + ", "+", StringComparison.Ordinal);
    }

    private string PrimaryKeyLabel()
    {
        return PrimaryKey switch
        {
            >= ShortcutKey.D0 and <= ShortcutKey.D9 =>
                ((int)PrimaryKey - (int)ShortcutKey.D0).ToString(),
            ShortcutKey.OemBackslash => "\\",
            ShortcutKey.OemSemicolon => ";",
            ShortcutKey.OemPlus => "+",
            ShortcutKey.OemComma => ",",
            ShortcutKey.OemMinus => "-",
            ShortcutKey.OemPeriod => ".",
            ShortcutKey.OemQuestion => "/",
            ShortcutKey.OemTilde => "`",
            ShortcutKey.OemOpenBrackets => "[",
            ShortcutKey.OemCloseBrackets => "]",
            ShortcutKey.OemQuotes => "'",
            _ => PrimaryKey.ToString(),
        };
    }
}
