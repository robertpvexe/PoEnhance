using System.Globalization;
using System.Text.RegularExpressions;

namespace PoEnhance.Core.Items.Parsing;

public sealed partial class ItemTextParser
{
    private const string ItemClassPrefix = "Item Class:";
    private const string RarityPrefix = "Rarity:";
    private const string ItemLevelPrefix = "Item Level:";
    private const string ModifierMarker = " Modifier ";
    private const string NotePrefix = "Note:";
    private const string EnchantSuffix = "(enchant)";
    private const string TierPrefix = "(Tier:";
    private const string RankPrefix = "(Rank:";
    private const string VeiledPrefixLine = "Veiled Prefix";
    private const string VeiledSuffixLine = "Veiled Suffix";
    private const char EmDash = '\u2014';
    private static readonly string UnscalableValueSuffix = $" {EmDash} Unscalable Value";

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

        var itemStates = new List<string>();
        var noteLines = new List<string>();
        var listingNote = ReadItemStatesAndNoteLines(sections, classifiedLines, itemStates, noteLines);

        var isUnidentified = itemStates.Contains("Unidentified", StringComparer.OrdinalIgnoreCase);
        var traditionalInfluences = new List<string>();
        var eldritchInfluences = new List<string>();
        ReadInfluenceLines(sections, classifiedLines, traditionalInfluences, eldritchInfluences);

        var (name, baseType) = ReadNameAndBaseType(
            sections,
            raritySectionIndex,
            classifiedLines,
            isUnidentified,
            rarity,
            itemClass);
        var isFlask = IsFlaskItemClass(itemClass);
        var itemTypeDescriptor = ReadItemTypeDescriptor(
            sections,
            raritySectionIndex,
            itemLevelSectionIndex,
            classifiedLines,
            isFlask);
        var hasAdvancedModifierMetadata = ContainsAdvancedModifierMetadata(sections);
        var flavourTextLines = ReadFlavourTextLines(
            sections,
            raritySectionIndex,
            itemLevelSectionIndex,
            classifiedLines,
            rarity,
            hasAdvancedModifierMetadata);
        var properties = new List<ParsedItemProperty>();
        var enchantments = new List<ParsedEnchantment>();
        var modifiers = new List<ParsedModifier>();
        var uniqueModifiers = new List<ParsedModifier>();
        var descriptionLines = new List<string>();
        var unclassifiedLines = new List<string>();

        foreach (var section in sections)
        {
            var isModifierSection = itemLevelSectionIndex.HasValue
                && section.Index > itemLevelSectionIndex.Value
                && !IsFlavourTextSection(section, rarity, hasAdvancedModifierMetadata);
            PendingAdvancedModifier? pendingAdvancedModifier = null;

            for (var lineIndex = 0; lineIndex < section.Lines.Count; lineIndex++)
            {
                if (classifiedLines.Contains(new LineLocation(section.Index, lineIndex)))
                {
                    continue;
                }

                var rawLine = section.Lines[lineIndex];
                var line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (IsModifierMetadataLine(line))
                {
                    FlushPendingAdvancedModifier(
                        pendingAdvancedModifier,
                        modifiers,
                        uniqueModifiers,
                        unclassifiedLines);

                    pendingAdvancedModifier = new PendingAdvancedModifier(ParseModifierMetadata(line));
                }
                else if (isModifierSection && TryReadVeiledModifierKind(line, out var veiledModifierKind))
                {
                    if (pendingAdvancedModifier is not null)
                    {
                        pendingAdvancedModifier.ValueLines.Add(rawLine);
                        AddParsedModifier(
                            CreateParsedModifier(
                                pendingAdvancedModifier.ValueLines.ToArray(),
                                pendingAdvancedModifier.Metadata with
                                {
                                    Kind = veiledModifierKind,
                                    IsVeiled = true,
                            }),
                            modifiers,
                            uniqueModifiers);
                    }
                    else
                    {
                        AddParsedModifier(
                            CreateStandaloneVeiledModifier(line, veiledModifierKind),
                            modifiers,
                            uniqueModifiers);
                    }

                    pendingAdvancedModifier = null;
                }
                else if (TryCreateEnchantment(line, out var enchantment))
                {
                    FlushPendingAdvancedModifier(
                        pendingAdvancedModifier,
                        modifiers,
                        uniqueModifiers,
                        unclassifiedLines);
                    pendingAdvancedModifier = null;

                    enchantments.Add(enchantment);
                }
                else if (pendingAdvancedModifier is null && IsPropertyLine(line, isModifierSection, isFlask))
                {
                    properties.Add(CreateItemProperty(line, properties.Count));
                }
                else if (pendingAdvancedModifier is null && IsDescriptionLine(line))
                {
                    descriptionLines.Add(line);
                }
                else if (isModifierSection)
                {
                    if (pendingAdvancedModifier is not null)
                    {
                        pendingAdvancedModifier.ValueLines.Add(rawLine);
                    }
                    else
                    {
                        AddParsedModifier(
                            CreateParsedModifier([rawLine], metadata: null),
                            modifiers,
                            uniqueModifiers);
                    }
                }
                else
                {
                    FlushPendingAdvancedModifier(
                        pendingAdvancedModifier,
                        modifiers,
                        uniqueModifiers,
                        unclassifiedLines);
                    pendingAdvancedModifier = null;

                    unclassifiedLines.Add(line);
                }
            }

            FlushPendingAdvancedModifier(
                pendingAdvancedModifier,
                modifiers,
                uniqueModifiers,
                unclassifiedLines);
        }

        var implicitModifiers = modifiers
            .Where(modifier => modifier.Kind == ParsedModifierKind.Implicit)
            .ToArray();
        var prefixModifiers = modifiers
            .Where(modifier => modifier.Kind == ParsedModifierKind.Prefix)
            .ToArray();
        var suffixModifiers = modifiers
            .Where(modifier => modifier.Kind == ParsedModifierKind.Suffix)
            .ToArray();
        var explicitModifiersWithUnknownKind = modifiers
            .Where(modifier => modifier.Kind == ParsedModifierKind.Unknown)
            .ToArray();

        return new ParsedItem(
            rawText,
            GetInputFormat(rawText, hasAdvancedModifierMetadata),
            itemClass,
            rarity,
            name,
            baseType,
            itemTypeDescriptor,
            itemStates,
            noteLines,
            listingNote,
            traditionalInfluences,
            eldritchInfluences,
            itemStates.Contains("Corrupted", StringComparer.OrdinalIgnoreCase),
            itemLevel,
            properties.ToArray(),
            modifiers,
            implicitModifiers,
            prefixModifiers,
            suffixModifiers,
            uniqueModifiers,
            explicitModifiersWithUnknownKind,
            modifiers.SelectMany(modifier => modifier.ValueLines).ToArray(),
            flavourTextLines,
            enchantments,
            descriptionLines,
            unclassifiedLines);
    }

    private static (string? Name, string? BaseType) ReadNameAndBaseType(
        IReadOnlyList<ItemTextSection> sections,
        int? raritySectionIndex,
        ISet<LineLocation> classifiedLines,
        bool isUnidentified,
        string? rarity,
        string? itemClass)
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

        if ((isUnidentified || IsNormalRarity(rarity) || IsSingleHeaderBaseTypeItem(rarity, itemClass, headerLines.Count))
            && headerLines.Count >= 1)
        {
            baseType = headerLines[^1].Line;

            foreach (var headerLine in headerLines)
            {
                classifiedLines.Add(new LineLocation(section.Index, headerLine.LineIndex));
            }

            return (name, baseType);
        }

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

    private static string? ReadItemStatesAndNoteLines(
        IReadOnlyList<ItemTextSection> sections,
        ISet<LineLocation> classifiedLines,
        ICollection<string> itemStates,
        ICollection<string> noteLines)
    {
        string? listingNote = null;

        foreach (var section in sections)
        {
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

                if (IsItemStateLine(line))
                {
                    itemStates.Add(line);
                    classifiedLines.Add(new LineLocation(section.Index, lineIndex));
                    continue;
                }

                if (line.StartsWith(NotePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    noteLines.Add(line);
                    listingNote ??= line[NotePrefix.Length..].Trim();
                    classifiedLines.Add(new LineLocation(section.Index, lineIndex));
                }
            }
        }

        return listingNote;
    }

    private static string? ReadItemTypeDescriptor(
        IReadOnlyList<ItemTextSection> sections,
        int? raritySectionIndex,
        int? itemLevelSectionIndex,
        ISet<LineLocation> classifiedLines,
        bool isFlask)
    {
        if (!raritySectionIndex.HasValue)
        {
            return null;
        }

        foreach (var section in sections.Where(section => section.Index > raritySectionIndex.Value))
        {
            if (itemLevelSectionIndex.HasValue && section.Index >= itemLevelSectionIndex.Value)
            {
                break;
            }

            if (LooksLikeFlavorText(section))
            {
                continue;
            }

            var candidateLines = ReadUnclassifiedNonEmptyLines(section, classifiedLines);
            if (candidateLines.Count == 0)
            {
                continue;
            }

            var firstLine = candidateLines[0];
            if (firstLine.Line.Contains(':') || IsModifierMetadataLine(firstLine.Line))
            {
                continue;
            }

            if (isFlask && IsFlaskPropertyLine(firstLine.Line))
            {
                continue;
            }

            if (!HasFollowingPropertyLine(candidateLines)
                && !NextPreItemLevelSectionStartsWithProperty(sections, section.Index, itemLevelSectionIndex, classifiedLines))
            {
                continue;
            }

            classifiedLines.Add(new LineLocation(section.Index, firstLine.LineIndex));
            return firstLine.Line;
        }

        return null;
    }

    private static IReadOnlyList<(int LineIndex, string Line)> ReadUnclassifiedNonEmptyLines(
        ItemTextSection section,
        ISet<LineLocation> classifiedLines)
    {
        var lines = new List<(int LineIndex, string Line)>();

        for (var lineIndex = 0; lineIndex < section.Lines.Count; lineIndex++)
        {
            if (classifiedLines.Contains(new LineLocation(section.Index, lineIndex)))
            {
                continue;
            }

            var line = section.Lines[lineIndex].Trim();
            if (line.Length > 0)
            {
                lines.Add((lineIndex, line));
            }
        }

        return lines;
    }

    private static bool HasFollowingPropertyLine(IReadOnlyList<(int LineIndex, string Line)> candidateLines)
    {
        return candidateLines.Skip(1).Any(candidateLine => candidateLine.Line.Contains(':'));
    }

    private static bool NextPreItemLevelSectionStartsWithProperty(
        IReadOnlyList<ItemTextSection> sections,
        int currentSectionIndex,
        int? itemLevelSectionIndex,
        ISet<LineLocation> classifiedLines)
    {
        var nextSection = sections.FirstOrDefault(section => section.Index > currentSectionIndex);
        if (nextSection is null || itemLevelSectionIndex.HasValue && nextSection.Index >= itemLevelSectionIndex.Value)
        {
            return false;
        }

        var nextSectionLines = ReadUnclassifiedNonEmptyLines(nextSection, classifiedLines);
        return nextSectionLines.Count > 0 && nextSectionLines[0].Line.Contains(':');
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

    private static bool IsItemStateLine(string line)
    {
        return line.Equals("Unidentified", StringComparison.OrdinalIgnoreCase)
            || line.Equals("Corrupted", StringComparison.OrdinalIgnoreCase)
            || line.Equals("Mirrored", StringComparison.OrdinalIgnoreCase)
            || line.Equals("Split", StringComparison.OrdinalIgnoreCase)
            || line.Equals("Synthesised Item", StringComparison.OrdinalIgnoreCase)
            || line.Equals("Fractured Item", StringComparison.OrdinalIgnoreCase)
            || line.Equals("Relic Unique", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNormalRarity(string? rarity)
    {
        return rarity?.Equals("Normal", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsSingleHeaderBaseTypeItem(string? rarity, string? itemClass, int headerLineCount)
    {
        return headerLineCount == 1
            && (rarity?.Equals("Currency", StringComparison.OrdinalIgnoreCase) == true
                || itemClass?.Equals("Stackable Currency", StringComparison.OrdinalIgnoreCase) == true
                || itemClass?.Equals("Incubators", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool IsFlaskItemClass(string? itemClass)
    {
        return itemClass?.Equals("Life Flasks", StringComparison.OrdinalIgnoreCase) == true
            || itemClass?.Equals("Mana Flasks", StringComparison.OrdinalIgnoreCase) == true
            || itemClass?.Equals("Hybrid Flasks", StringComparison.OrdinalIgnoreCase) == true
            || itemClass?.Equals("Utility Flasks", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static void ReadInfluenceLines(
        IReadOnlyList<ItemTextSection> sections,
        ISet<LineLocation> classifiedLines,
        ICollection<string> traditionalInfluences,
        ICollection<string> eldritchInfluences)
    {
        foreach (var section in sections)
        {
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

                if (IsEldritchInfluenceLine(line))
                {
                    eldritchInfluences.Add(line);
                    classifiedLines.Add(new LineLocation(section.Index, lineIndex));
                    continue;
                }

                if (IsTraditionalInfluenceLine(line))
                {
                    traditionalInfluences.Add(line);
                    classifiedLines.Add(new LineLocation(section.Index, lineIndex));
                }
            }
        }
    }

    private static bool IsEldritchInfluenceLine(string line)
    {
        return line.Equals("Searing Exarch Item", StringComparison.OrdinalIgnoreCase)
            || line.Equals("Eater of Worlds Item", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTraditionalInfluenceLine(string line)
    {
        return line.Equals("Shaper Item", StringComparison.OrdinalIgnoreCase)
            || line.Equals("Elder Item", StringComparison.OrdinalIgnoreCase)
            || line.Equals("Crusader Item", StringComparison.OrdinalIgnoreCase)
            || line.Equals("Redeemer Item", StringComparison.OrdinalIgnoreCase)
            || line.Equals("Hunter Item", StringComparison.OrdinalIgnoreCase)
            || line.Equals("Warlord Item", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ReadFlavourTextLines(
        IReadOnlyList<ItemTextSection> sections,
        int? raritySectionIndex,
        int? itemLevelSectionIndex,
        ISet<LineLocation> classifiedLines,
        string? rarity,
        bool hasAdvancedModifierMetadata)
    {
        var firstCandidateSectionIndex = itemLevelSectionIndex ?? raritySectionIndex;
        if (!firstCandidateSectionIndex.HasValue)
        {
            return [];
        }

        var flavourTextLines = new List<string>();
        foreach (var section in sections.Where(section => section.Index > firstCandidateSectionIndex.Value))
        {
            if (!IsFlavourTextSection(section, rarity, hasAdvancedModifierMetadata))
            {
                continue;
            }

            for (var lineIndex = 0; lineIndex < section.Lines.Count; lineIndex++)
            {
                if (classifiedLines.Contains(new LineLocation(section.Index, lineIndex)))
                {
                    continue;
                }

                var rawLine = section.Lines[lineIndex];
                var line = rawLine.Trim();
                if (line.Length == 0 || IsDescriptionLine(line))
                {
                    continue;
                }

                flavourTextLines.Add(rawLine);
                classifiedLines.Add(new LineLocation(section.Index, lineIndex));
            }
        }

        return flavourTextLines;
    }

    private static void FlushPendingAdvancedModifier(
        PendingAdvancedModifier? pendingAdvancedModifier,
        ICollection<ParsedModifier> modifiers,
        ICollection<ParsedModifier> uniqueModifiers,
        ICollection<string> unclassifiedLines)
    {
        if (pendingAdvancedModifier is null)
        {
            return;
        }

        if (pendingAdvancedModifier.ValueLines.Count == 0)
        {
            unclassifiedLines.Add(pendingAdvancedModifier.Metadata.RawLine);
            return;
        }

        AddParsedModifier(
            CreateParsedModifier(
                pendingAdvancedModifier.ValueLines.ToArray(),
                pendingAdvancedModifier.Metadata),
            modifiers,
            uniqueModifiers);
    }

    private static void AddParsedModifier(
        ParsedModifier modifier,
        ICollection<ParsedModifier> modifiers,
        ICollection<ParsedModifier> uniqueModifiers)
    {
        modifiers.Add(modifier);

        if (modifier.Kind == ParsedModifierKind.Unique)
        {
            uniqueModifiers.Add(modifier);
        }
    }

    private static ParsedModifier CreateParsedModifier(IReadOnlyList<string> valueLines, PendingModifierMetadata? metadata)
    {
        var effects = CreateModifierEffects(valueLines);
        var cleanedValueLines = effects
            .Select(effect => effect.Text)
            .Where(line => line.Length > 0)
            .ToArray();

        if (metadata is not null)
        {
            return new ParsedModifier(
                cleanedValueLines,
                metadata.RawLine,
                metadata.Kind,
                metadata.Name,
                metadata.Tier,
                metadata.Rank,
                metadata.CategoryText,
                metadata.IsCrafted,
                metadata.IsFractured,
                metadata.IsVeiled)
            {
                Effects = effects,
            };
        }

        var text = string.Join(Environment.NewLine, cleanedValueLines);
        return new ParsedModifier(
            cleanedValueLines,
            RawMetadataLine: null,
            InferNormalModifierKind(text),
            Name: null,
            Tier: null,
            Rank: null,
            CategoryText: null,
            IsCrafted: false,
            IsFractured: false,
            IsVeiled: false)
        {
            Effects = effects,
        };
    }

    private static IReadOnlyList<ParsedModifierEffect> CreateModifierEffects(IReadOnlyList<string> valueLines)
    {
        var effects = new List<PendingModifierEffect>();
        foreach (var rawLine in valueLines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (IsReminderLine(line) && effects.Count > 0)
            {
                effects[^1].ReminderLines.Add(line);
                continue;
            }

            var cleanedLine = RemoveTerminalUnscalableValue(line, out var hasUnscalableValue);
            effects.Add(new PendingModifierEffect(cleanedLine, hasUnscalableValue));
        }

        return effects
            .Select(effect => new ParsedModifierEffect(
                effect.Text,
                effect.ReminderLines.ToArray(),
                effect.HasUnscalableValue))
            .ToArray();
    }

    private static bool IsReminderLine(string line)
    {
        return line.Length >= 2 &&
            line[0] == '(' &&
            line[^1] == ')';
    }

    private static string RemoveTerminalUnscalableValue(string line, out bool hasUnscalableValue)
    {
        if (line.EndsWith(UnscalableValueSuffix, StringComparison.Ordinal))
        {
            hasUnscalableValue = true;
            return line[..^UnscalableValueSuffix.Length].TrimEnd();
        }

        hasUnscalableValue = false;
        return line;
    }

    private static bool TryReadVeiledModifierKind(string line, out ParsedModifierKind kind)
    {
        if (line.Equals(VeiledPrefixLine, StringComparison.OrdinalIgnoreCase))
        {
            kind = ParsedModifierKind.Prefix;
        }
        else if (line.Equals(VeiledSuffixLine, StringComparison.OrdinalIgnoreCase))
        {
            kind = ParsedModifierKind.Suffix;
        }
        else
        {
            kind = default;
            return false;
        }

        return true;
    }

    private static ParsedModifier CreateStandaloneVeiledModifier(string line, ParsedModifierKind kind)
    {
        return new ParsedModifier(
            [line],
            RawMetadataLine: null,
            kind,
            Name: null,
            Tier: null,
            Rank: null,
            CategoryText: null,
            IsCrafted: false,
            IsFractured: false,
            IsVeiled: true);
    }

    private static bool IsDescriptionLine(string line)
    {
        if (line.Contains(':')
            || IsItemStateLine(line)
            || line.StartsWith(NotePrefix, StringComparison.OrdinalIgnoreCase)
            || IsModifierMetadataLine(line)
            || IsEnchantmentLine(line)
            || IsClearlyNumericModifierLine(line))
        {
            return false;
        }

        return line.Equals("Removes all modifiers from an item", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Right click ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Shift click ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Place into ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Place this item ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Item drops after killing ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Adds an incubated ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Can grow into ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Corrupts a level ", StringComparison.OrdinalIgnoreCase)
            || line.Equals(
                "Place into an allocated Jewel Socket on the Passive Skill Tree. Right click to remove from the Socket.",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPropertyLine(string line, bool isModifierSection, bool isFlask)
    {
        if (line.Contains(':') && !isModifierSection)
        {
            return true;
        }

        if (line.StartsWith("Requires ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return isFlask && IsFlaskPropertyLine(line);
    }

    private static ParsedItemProperty CreateItemProperty(string line, int sourceIndex)
    {
        var colonIndex = line.IndexOf(':');
        var name = colonIndex < 0
            ? line
            : line[..colonIndex].Trim();
        var rawValueText = colonIndex < 0
            ? string.Empty
            : line[(colonIndex + 1)..].Trim();

        return new ParsedItemProperty(
            line,
            name,
            rawValueText,
            NormalizePropertyName(name),
            sourceIndex,
            ParseNumericPropertyGroups(rawValueText));
    }

    private static string NormalizePropertyName(string name)
    {
        return string.Join(
                ' ',
                name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
    }

    private static IReadOnlyList<ParsedItemPropertyNumericGroup> ParseNumericPropertyGroups(
        string rawValueText)
    {
        if (string.IsNullOrWhiteSpace(rawValueText))
        {
            return [];
        }

        var segments = rawValueText.Split(',');
        var groups = new List<ParsedItemPropertyNumericGroup>(segments.Length);
        foreach (var rawSegment in segments)
        {
            var segment = rawSegment.Trim();
            var match = NumericPropertyGroupPattern().Match(segment);
            if (!match.Success)
            {
                return [];
            }

            var isPercentage = match.Groups["percentage"].Success;
            var isAugmented = match.Groups["augmented"].Success;
            if (match.Groups["scalar"].Success)
            {
                if (!TryParsePropertyNumber(match.Groups["scalar"].Value, out var scalar))
                {
                    return [];
                }

                groups.Add(new ParsedItemPropertyNumericGroup(
                    segment,
                    scalar,
                    MinimumValue: null,
                    MaximumValue: null,
                    isPercentage,
                    isAugmented));
                continue;
            }

            if (!TryParsePropertyNumber(match.Groups["minimum"].Value, out var minimum) ||
                !TryParsePropertyNumber(match.Groups["maximum"].Value, out var maximum))
            {
                return [];
            }

            groups.Add(new ParsedItemPropertyNumericGroup(
                segment,
                ScalarValue: null,
                minimum,
                maximum,
                isPercentage,
                isAugmented));
        }

        return groups.ToArray();
    }

    private static bool TryParsePropertyNumber(string value, out decimal number)
    {
        return decimal.TryParse(
            value,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out number);
    }

    private static bool IsFlaskPropertyLine(string line)
    {
        return line.StartsWith("Recovers ", StringComparison.OrdinalIgnoreCase) && line.Contains(" over ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Lasts ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Consumes ", StringComparison.OrdinalIgnoreCase) && line.Contains(" Charges on use", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Currently has ", StringComparison.OrdinalIgnoreCase) && line.EndsWith(" Charges", StringComparison.OrdinalIgnoreCase)
            || line.Equals("+1500 to Armour", StringComparison.OrdinalIgnoreCase)
            || line.Equals("+5% to maximum Fire Resistance", StringComparison.OrdinalIgnoreCase)
            || line.Equals("+40% to Fire Resistance", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCreateEnchantment(string line, out ParsedEnchantment enchantment)
    {
        if (!IsEnchantmentLine(line))
        {
            enchantment = default!;
            return false;
        }

        enchantment = new ParsedEnchantment(
            line,
            IsAnoint: line.StartsWith("Allocates ", StringComparison.OrdinalIgnoreCase));
        return true;
    }

    private static bool IsEnchantmentLine(string line)
    {
        return line.EndsWith(EnchantSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsClearlyNumericModifierLine(string line)
    {
        if (line.Length == 0)
        {
            return false;
        }

        return char.IsDigit(line[0])
            || line[0] == '+'
            || line[0] == '-'
            || line.Contains("(implicit)", StringComparison.OrdinalIgnoreCase);
    }

    private static ParsedModifierKind InferNormalModifierKind(string text)
    {
        return text.Contains("(implicit)", StringComparison.OrdinalIgnoreCase)
            ? ParsedModifierKind.Implicit
            : ParsedModifierKind.Unknown;
    }

    private static ParsedItemInputFormat GetInputFormat(string rawText, bool hasAdvancedModifierMetadata)
    {
        if (hasAdvancedModifierMetadata)
        {
            return ParsedItemInputFormat.Advanced;
        }

        return string.IsNullOrWhiteSpace(rawText)
            ? ParsedItemInputFormat.Unknown
            : ParsedItemInputFormat.Normal;
    }

    private static bool IsModifierMetadataLine(string line)
    {
        var trimmedLine = line.Trim();
        return trimmedLine.StartsWith('{')
            && trimmedLine.EndsWith('}')
            && trimmedLine.Contains(ModifierMarker, StringComparison.OrdinalIgnoreCase);
    }

    private static PendingModifierMetadata ParseModifierMetadata(string line)
    {
        var metadataText = line.Trim()[1..^1].Trim();
        var kind = ReadModifierKind(metadataText);
        var name = ReadQuotedValue(metadataText);
        var tier = ReadTier(metadataText);
        var rank = ReadRank(metadataText);
        var categoryText = ReadCategoryText(metadataText);
        var isCrafted = IsCraftedModifier(metadataText);
        var isFractured = IsFracturedModifier(metadataText);
        var isVeiled = IsVeiledModifier(metadataText);

        return new PendingModifierMetadata(line, kind, name, tier, rank, categoryText, isCrafted, isFractured, isVeiled);
    }

    private static ParsedModifierKind ReadModifierKind(string metadataText)
    {
        if (metadataText.Contains("Unique Modifier", StringComparison.OrdinalIgnoreCase))
        {
            return ParsedModifierKind.Unique;
        }

        if (metadataText.Contains("Prefix Modifier", StringComparison.OrdinalIgnoreCase))
        {
            return ParsedModifierKind.Prefix;
        }

        if (metadataText.Contains("Suffix Modifier", StringComparison.OrdinalIgnoreCase))
        {
            return ParsedModifierKind.Suffix;
        }

        if (metadataText.Contains("Implicit Modifier", StringComparison.OrdinalIgnoreCase))
        {
            return ParsedModifierKind.Implicit;
        }

        return ParsedModifierKind.Unknown;
    }

    private static string? ReadQuotedValue(string metadataText)
    {
        var openingQuoteIndex = metadataText.IndexOf('"');
        if (openingQuoteIndex < 0)
        {
            return null;
        }

        var closingQuoteIndex = metadataText.IndexOf('"', openingQuoteIndex + 1);
        if (closingQuoteIndex <= openingQuoteIndex)
        {
            return null;
        }

        return metadataText[(openingQuoteIndex + 1)..closingQuoteIndex];
    }

    private static int? ReadTier(string metadataText)
    {
        return ReadParenthesizedInteger(metadataText, TierPrefix);
    }

    private static int? ReadRank(string metadataText)
    {
        return ReadParenthesizedInteger(metadataText, RankPrefix);
    }

    private static int? ReadParenthesizedInteger(string metadataText, string prefix)
    {
        var valueIndex = metadataText.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (valueIndex < 0)
        {
            return null;
        }

        var valueStartIndex = valueIndex + prefix.Length;
        var valueEndIndex = metadataText.IndexOf(')', valueStartIndex);
        if (valueEndIndex < 0)
        {
            return null;
        }

        var valueText = metadataText[valueStartIndex..valueEndIndex].Trim();
        return int.TryParse(valueText, out var value) ? value : null;
    }

    private static bool IsCraftedModifier(string metadataText)
    {
        return metadataText.Contains("Crafted", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFracturedModifier(string metadataText)
    {
        return metadataText.Contains("Fractured", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVeiledModifier(string metadataText)
    {
        return metadataText.Contains("Veiled", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadCategoryText(string metadataText)
    {
        var dashIndex = metadataText.IndexOf('—');
        var categoryStartIndex = dashIndex < 0 ? -1 : dashIndex + 1;
        if (dashIndex < 0)
        {
            dashIndex = metadataText.IndexOf(" - ", StringComparison.Ordinal);
            categoryStartIndex = dashIndex < 0 ? -1 : dashIndex + 3;
        }

        if (categoryStartIndex < 0 || categoryStartIndex >= metadataText.Length)
        {
            return null;
        }

        var categoryText = metadataText[categoryStartIndex..].Trim();
        return categoryText.Length == 0 ? null : categoryText;
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

    private static bool ContainsAdvancedModifierMetadata(IReadOnlyList<ItemTextSection> sections)
    {
        return sections.Any(section => section.Lines.Any(line => IsModifierMetadataLine(line.Trim())));
    }

    private static bool LooksLikeFlavorText(ItemTextSection section)
    {
        return section.Lines.Any(line =>
        {
            var trimmedLine = line.Trim();
            return trimmedLine.StartsWith('"') || trimmedLine.EndsWith('"');
        });
    }

    private static bool IsFlavourTextSection(
        ItemTextSection section,
        string? rarity,
        bool hasAdvancedModifierMetadata)
    {
        if (LooksLikeFlavorText(section))
        {
            return true;
        }

        if (hasAdvancedModifierMetadata && LooksLikeAdvancedTerminalFlavourText(section))
        {
            return true;
        }

        return rarity?.Equals("Unique", StringComparison.OrdinalIgnoreCase) == true
            && section.Lines.Any(line => line.Trim().Length > 0)
            && section.Lines
                .Where(line => line.Trim().Length > 0)
                .All(line =>
                {
                    var trimmedLine = line.Trim();
                    return !trimmedLine.Contains(':')
                        && !IsModifierMetadataLine(trimmedLine)
                        && !IsEnchantmentLine(trimmedLine)
                        && !IsClearlyNumericModifierLine(trimmedLine);
                });
    }

    private static bool LooksLikeAdvancedTerminalFlavourText(ItemTextSection section)
    {
        var nonEmptyLines = section.Lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
        return nonEmptyLines.Length > 0 &&
            nonEmptyLines.All(line =>
                !line.Contains(':') &&
                !IsDescriptionLine(line) &&
                !IsItemStateLine(line) &&
                !IsTraditionalInfluenceLine(line) &&
                !IsEldritchInfluenceLine(line) &&
                !IsModifierMetadataLine(line) &&
                !IsEnchantmentLine(line) &&
                !IsClearlyNumericModifierLine(line));
    }

    private sealed record ItemTextSection(int Index, IReadOnlyList<string> Lines);

    private sealed record PendingModifierMetadata(
        string RawLine,
        ParsedModifierKind Kind,
        string? Name,
        int? Tier,
        int? Rank,
        string? CategoryText,
        bool IsCrafted,
        bool IsFractured,
        bool IsVeiled);

    private sealed record PendingAdvancedModifier(PendingModifierMetadata Metadata)
    {
        public List<string> ValueLines { get; } = [];
    }

    private sealed record PendingModifierEffect(string Text, bool HasUnscalableValue)
    {
        public List<string> ReminderLines { get; } = [];
    }

    private readonly record struct LineLocation(int SectionIndex, int LineIndex);

    [GeneratedRegex(
        @"^(?:(?<minimum>[+-]?\d+(?:\.\d+)?)\s*-\s*(?<maximum>[+-]?\d+(?:\.\d+)?)|(?<scalar>[+-]?\d+(?:\.\d+)?))(?<percentage>%)?(?:\s+(?<augmented>\(augmented\)))?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex NumericPropertyGroupPattern();
}
