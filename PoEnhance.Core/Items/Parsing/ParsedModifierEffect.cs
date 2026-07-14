namespace PoEnhance.Core.Items.Parsing;

public sealed record ParsedModifierEffect(
    string Text,
    IReadOnlyList<string> ReminderLines,
    bool HasUnscalableValue);
