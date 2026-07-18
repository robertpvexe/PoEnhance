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
    public ParsedVeiledModifierState VeiledState { get; init; } = IsVeiled
        ? string.IsNullOrWhiteSpace(Name)
            ? ParsedVeiledModifierState.UnrevealedPlaceholder
            : ParsedVeiledModifierState.NamedUnveiled
        : ParsedVeiledModifierState.None;

    public bool IsUnrevealedVeiledPlaceholder =>
        VeiledState == ParsedVeiledModifierState.UnrevealedPlaceholder;

    public bool IsNamedUnveiled => VeiledState == ParsedVeiledModifierState.NamedUnveiled;

    public ParsedImplicitModifierOrigin ImplicitOrigin { get; init; }

    public ParsedUniqueModifierOrigin UniqueOrigin { get; init; }

    public ParsedEldritchImplicitTier? EldritchTier { get; init; }

    public IReadOnlyList<ParsedModifierEffect> Effects { get; init; } = ValueLines
        .Select(line => new ParsedModifierEffect(line, [], HasUnscalableValue: false))
        .ToArray();

    public IReadOnlyList<string> ReminderLines => Effects
        .SelectMany(effect => effect.ReminderLines)
        .ToArray();

    public bool HasUnscalableValue => Effects.Any(effect => effect.HasUnscalableValue);

    public string Text => string.Join(Environment.NewLine, ValueLines);
}
