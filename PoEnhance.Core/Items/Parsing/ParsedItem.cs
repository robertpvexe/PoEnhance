namespace PoEnhance.Core.Items.Parsing;

public sealed record ParsedItem(
    string RawText,
    ParsedItemInputFormat InputFormat,
    string? ItemClass,
    string? Rarity,
    string? Name,
    string? BaseType,
    string? ItemTypeDescriptor,
    IReadOnlyList<string> ItemStates,
    IReadOnlyList<string> NoteLines,
    string? ListingNote,
    IReadOnlyList<string> TraditionalInfluences,
    IReadOnlyList<string> EldritchInfluences,
    bool IsCorrupted,
    int? ItemLevel,
    IReadOnlyList<string> PropertyLines,
    IReadOnlyList<ParsedModifier> Modifiers,
    IReadOnlyList<ParsedModifier> ImplicitModifiers,
    IReadOnlyList<ParsedModifier> PrefixModifiers,
    IReadOnlyList<ParsedModifier> SuffixModifiers,
    IReadOnlyList<ParsedModifier> UniqueModifiers,
    IReadOnlyList<ParsedModifier> ExplicitModifiersWithUnknownKind,
    IReadOnlyList<string> ModifierLines,
    IReadOnlyList<string> FlavourTextLines,
    IReadOnlyList<ParsedEnchantment> Enchantments,
    IReadOnlyList<string> DescriptionLines,
    IReadOnlyList<string> UnclassifiedLines)
{
    public string? DisplayName => string.IsNullOrWhiteSpace(Name) ? BaseType : Name;
}
