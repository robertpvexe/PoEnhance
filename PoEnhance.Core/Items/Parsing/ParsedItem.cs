namespace PoEnhance.Core.Items.Parsing;

public sealed record ParsedItem(
    string RawText,
    string? ItemClass,
    string? Rarity,
    string? Name,
    string? BaseType,
    int? ItemLevel,
    IReadOnlyList<string> PropertyLines,
    IReadOnlyList<string> ModifierLines,
    IReadOnlyList<string> UnclassifiedLines);
