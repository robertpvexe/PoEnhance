namespace PoEnhance.Core.Items.Parsing;

public sealed record ParsedItemProperty(
    string OriginalText,
    string Name,
    string RawValueText,
    string NormalizedName,
    int SourceIndex,
    IReadOnlyList<ParsedItemPropertyNumericGroup> NumericGroups);
