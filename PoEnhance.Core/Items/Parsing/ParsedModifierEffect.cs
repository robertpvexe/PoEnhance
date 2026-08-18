namespace PoEnhance.Core.Items.Parsing;

public sealed record ParsedModifierEffect(
    string Text,
    IReadOnlyList<string> ReminderLines,
    bool HasUnscalableValue)
{
    /// <summary>
    /// Complete copied effect line before terminal Advanced Copy metadata is removed.
    /// </summary>
    public string RawText { get; init; } = Text;

    /// <summary>
    /// Candidate semantic text with a syntactically valid textual option-range separated.
    /// Consumers must still require exact generated-catalog proof before using it.
    /// </summary>
    public string SemanticText { get; init; } = Text;

    public ParsedTextualOptionRange? TextualOptionRange { get; init; }
}
