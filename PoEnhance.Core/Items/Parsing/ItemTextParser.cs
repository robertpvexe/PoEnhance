namespace PoEnhance.Core.Items.Parsing;

public sealed class ItemTextParser
{
    private const string ItemClassPrefix = "Item Class:";
    private const string RarityPrefix = "Rarity:";
    private const string ItemLevelPrefix = "Item Level:";

    public ParsedItem Parse(string? rawText)
    {
        rawText ??= string.Empty;

        var sections = SplitIntoSections(SplitLines(rawText));
        var classifiedLines = new HashSet<LineLocation>();

        string? itemClass = null;
        string? rarity = null;
        int? itemLevel = null;
        int? raritySectionIndex = null;
        int? itemLevelSectionIndex = null;

        foreach (var section in sections)
        {
            for (var lineIndex = 0; lineIndex < section.Lines.Count; lineIndex++)
            {
                var line = section.Lines[lineIndex].Trim();

                if (TryReadPrefixedValue(line, ItemClassPrefix, out var currentItemClass))
                {
                    itemClass ??= currentItemClass;
                    classifiedLines.Add(new LineLocation(section.Index, lineIndex));
                    continue;
                }

                if (TryReadPrefixedValue(line, RarityPrefix, out var currentRarity))
                {
                    rarity ??= currentRarity;
                    raritySectionIndex ??= section.Index;
                    classifiedLines.Add(new LineLocation(section.Index, lineIndex));
                    continue;
                }

                if (TryReadPrefixedValue(line, ItemLevelPrefix, out var currentItemLevelText))
                {
                    if (int.TryParse(currentItemLevelText, out var currentItemLevel))
                    {
                        itemLevel ??= currentItemLevel;
                    }

                    itemLevelSectionIndex ??= section.Index;
                    classifiedLines.Add(new LineLocation(section.Index, lineIndex));
                }
            }
        }

        var (name, baseType) = ReadNameAndBaseType(sections, raritySectionIndex, classifiedLines);
        var propertyLines = new List<string>();
        var modifierLines = new List<string>();
        var unclassifiedLines = new List<string>();

        foreach (var section in sections)
        {
            var isModifierSection = itemLevelSectionIndex.HasValue
                && section.Index > itemLevelSectionIndex.Value
                && !LooksLikeFlavorText(section);

            for (var lineIndex = 0; lineIndex < section.Lines.Count; lineIndex++)
            {
                if (classifiedLines.Contains(new LineLocation(section.Index, lineIndex)))
                {
                    continue;
                }

                var line = section.Lines[lineIndex].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.Contains(':'))
                {
                    propertyLines.Add(line);
                }
                else if (isModifierSection)
                {
                    modifierLines.Add(line);
                }
                else
                {
                    unclassifiedLines.Add(line);
                }
            }
        }

        return new ParsedItem(
            rawText,
            itemClass,
            rarity,
            name,
            baseType,
            itemLevel,
            propertyLines,
            modifierLines,
            unclassifiedLines);
    }

    private static (string? Name, string? BaseType) ReadNameAndBaseType(
        IReadOnlyList<ItemTextSection> sections,
        int? raritySectionIndex,
        ISet<LineLocation> classifiedLines)
    {
        if (!raritySectionIndex.HasValue)
        {
            return (null, null);
        }

        var section = sections.FirstOrDefault(section => section.Index == raritySectionIndex.Value);
        if (section is null)
        {
            return (null, null);
        }

        var headerLines = new List<(int LineIndex, string Line)>();
        for (var lineIndex = 0; lineIndex < section.Lines.Count; lineIndex++)
        {
            if (classifiedLines.Contains(new LineLocation(section.Index, lineIndex)))
            {
                continue;
            }

            var line = section.Lines[lineIndex].Trim();
            if (line.Length > 0)
            {
                headerLines.Add((lineIndex, line));
            }
        }

        string? name = null;
        string? baseType = null;

        if (headerLines.Count >= 1)
        {
            name = headerLines[0].Line;
            classifiedLines.Add(new LineLocation(section.Index, headerLines[0].LineIndex));
        }

        if (headerLines.Count >= 2)
        {
            baseType = headerLines[1].Line;
            classifiedLines.Add(new LineLocation(section.Index, headerLines[1].LineIndex));
        }

        return (name, baseType);
    }

    private static bool TryReadPrefixedValue(string line, string prefix, out string value)
    {
        if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = line[prefix.Length..].Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static IReadOnlyList<string> SplitLines(string rawText)
    {
        return rawText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static IReadOnlyList<ItemTextSection> SplitIntoSections(IReadOnlyList<string> lines)
    {
        var sections = new List<ItemTextSection>();
        var currentLines = new List<string>();
        var sectionIndex = 0;

        foreach (var line in lines)
        {
            if (IsSectionSeparator(line))
            {
                sections.Add(new ItemTextSection(sectionIndex, currentLines));
                currentLines = [];
                sectionIndex++;
                continue;
            }

            currentLines.Add(line);
        }

        sections.Add(new ItemTextSection(sectionIndex, currentLines));
        return sections;
    }

    private static bool IsSectionSeparator(string line)
    {
        var trimmedLine = line.Trim();
        return trimmedLine.Length > 0 && trimmedLine.All(character => character == '-');
    }

    private static bool LooksLikeFlavorText(ItemTextSection section)
    {
        return section.Lines.Any(line =>
        {
            var trimmedLine = line.Trim();
            return trimmedLine.StartsWith('"') || trimmedLine.EndsWith('"');
        });
    }

    private sealed record ItemTextSection(int Index, IReadOnlyList<string> Lines);

    private readonly record struct LineLocation(int SectionIndex, int LineIndex);
}
