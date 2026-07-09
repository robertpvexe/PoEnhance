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
    public string Text => string.Join(Environment.NewLine, ValueLines);
}
