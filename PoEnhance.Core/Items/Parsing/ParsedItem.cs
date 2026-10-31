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
    IReadOnlyList<ParsedItemProperty> Properties,
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

    public bool IsMirrored => ItemStates.Contains("Mirrored", StringComparer.OrdinalIgnoreCase);

    public bool IsIdentified => !ItemStates.Contains("Unidentified", StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Proven Fractured status from the clipboard marker and/or fractured modifier metadata.
    /// </summary>
    public bool IsFractured =>
        ItemStates.Contains("Fractured Item", StringComparer.OrdinalIgnoreCase) ||
        Modifiers.Any(static modifier => modifier.IsFractured);

    /// <summary>
    /// Proven Foulborn status from the decorated Unique display name and/or Foulborn Unique
    /// modifier metadata. Does not invent status from UI labels alone.
    /// </summary>
    public bool IsFoulborn =>
        (DisplayName?.StartsWith("Foulborn ", StringComparison.Ordinal) ?? false) ||
        Modifiers.Any(static modifier =>
            modifier.UniqueOrigin == ParsedUniqueModifierOrigin.Foulborn);

    public IReadOnlyList<string> PropertyLines { get; } = Properties
        .Select(property => property.OriginalText)
        .ToArray();
}
