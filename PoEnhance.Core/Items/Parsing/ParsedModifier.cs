namespace PoEnhance.Core.Items.Parsing;

public sealed record ParsedModifier(
    IReadOnlyList<string> ValueLines,
    string? RawMetadataLine,
    ParsedModifierKind Kind,
    string? Name,
    int? Tier,
    int? Rank,
    string? CategoryText,
    bool IsCrafted,
    bool IsFractured,
    bool IsVeiled)
{
    public IReadOnlyList<ParsedModifierEffect> Effects { get; init; } = ValueLines
        .Select(line => new ParsedModifierEffect(line, [], HasUnscalableValue: false))
        .ToArray();

    public IReadOnlyList<string> ReminderLines => Effects
        .SelectMany(effect => effect.ReminderLines)
        .ToArray();

    public bool HasUnscalableValue => Effects.Any(effect => effect.HasUnscalableValue);

    public string Text => string.Join(Environment.NewLine, ValueLines);
}
