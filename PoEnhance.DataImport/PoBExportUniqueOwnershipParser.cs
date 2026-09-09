using System.Text.RegularExpressions;

namespace PoEnhance.DataImport;

/// <summary>
/// Deterministic parser for pinned PoB <c>src/Export/Uniques/*.lua</c> ownership form.
/// Does not interpret general Lua — only the Export Unique entry grammar.
/// </summary>
public static partial class PoBExportUniqueOwnershipParser
{
    public const string RelativeDirectory = "src/Export/Uniques";

    public static PoBExportUniqueOwnershipIndex LoadFromPoBSourceRoot(string pobSourceRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pobSourceRootPath);
        var directory = Path.Combine(pobSourceRootPath, "src", "Export", "Uniques");
        return LoadFromDirectory(directory);
    }

    public static PoBExportUniqueOwnershipIndex LoadFromDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"Pinned Path of Building Export Uniques directory was not found: {directory}");
        }

        var entries = new List<PoBExportUniqueOwnershipEntry>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.lua")
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(path);
            if (fileName.Equals("ModTextMap.lua", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entries.AddRange(ParseFile(path, fileName));
        }

        return new PoBExportUniqueOwnershipIndex(entries);
    }

    public static IReadOnlyList<PoBExportUniqueOwnershipEntry> ParseFile(string path, string relativePath)
    {
        var text = File.ReadAllText(path);
        return ParseText(text, relativePath);
    }

    public static IReadOnlyList<PoBExportUniqueOwnershipEntry> ParseText(string text, string relativePath)
    {
        var entries = new List<PoBExportUniqueOwnershipEntry>();
        foreach (Match match in EntryPattern().Matches(text))
        {
            var body = match.Groups["body"].Value.Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = body.Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToArray();
            if (lines.Length == 0)
            {
                continue;
            }

            var name = lines[0];
            if (name.StartsWith("return", StringComparison.Ordinal) ||
                name.StartsWith("--", StringComparison.Ordinal) ||
                name.StartsWith("local ", StringComparison.Ordinal))
            {
                continue;
            }

            var variantLabels = new List<string>();
            var effectLines = new List<PoBExportUniqueEffectLine>();
            var pastHeader = false;
            for (var index = 1; index < lines.Length; index++)
            {
                var line = lines[index];
                if (line.StartsWith("Variant:", StringComparison.Ordinal))
                {
                    variantLabels.Add(line["Variant:".Length..].Trim());
                    continue;
                }

                if (IsMetadataLine(line))
                {
                    continue;
                }

                // First non-metadata line after the name is the base type.
                if (!pastHeader)
                {
                    pastHeader = true;
                    continue;
                }

                if (TryParseEffectLine(line, out var effect))
                {
                    effectLines.Add(effect);
                }
            }

            entries.Add(new PoBExportUniqueOwnershipEntry
            {
                CanonicalName = name,
                RelativePath = relativePath.Replace('\\', '/'),
                VariantLabels = variantLabels,
                EffectLines = effectLines,
            });
        }

        return entries;
    }

    public static bool TryResolveVariantScope(
        PoBExportUniqueOwnershipEntry entry,
        string versionLabel,
        int? sourceVariantIndex,
        out IReadOnlySet<int> variantIndices,
        out string decisionReason)
    {
        if (entry.VariantLabels.Count == 0)
        {
            variantIndices = new HashSet<int>();
            decisionReason = "export-entry-unscoped";
            return true;
        }

        var labelMatches = new List<int>();
        for (var index = 0; index < entry.VariantLabels.Count; index++)
        {
            if (string.Equals(entry.VariantLabels[index], versionLabel, StringComparison.Ordinal))
            {
                labelMatches.Add(index + 1);
            }
        }

        if (labelMatches.Count == 1)
        {
            variantIndices = new HashSet<int> { labelMatches[0] };
            decisionReason = "export-variant-label-exact";
            return true;
        }

        if (labelMatches.Count > 1)
        {
            variantIndices = new HashSet<int>();
            decisionReason = "export-variant-label-ambiguous";
            return false;
        }

        // Contextual option labels sometimes omit the trailing role suffix in Export.
        var contextualMatches = new List<int>();
        for (var index = 0; index < entry.VariantLabels.Count; index++)
        {
            var exportLabel = entry.VariantLabels[index];
            if (versionLabel.StartsWith(exportLabel + " (", StringComparison.Ordinal) ||
                exportLabel.StartsWith(versionLabel + " (", StringComparison.Ordinal))
            {
                contextualMatches.Add(index + 1);
            }
        }

        if (contextualMatches.Count == 1)
        {
            variantIndices = new HashSet<int> { contextualMatches[0] };
            decisionReason = "export-variant-label-contextual";
            return true;
        }

        if (sourceVariantIndex is int sourceIndex &&
            sourceIndex >= 1 &&
            sourceIndex <= entry.VariantLabels.Count &&
            string.Equals(entry.VariantLabels[sourceIndex - 1], versionLabel, StringComparison.Ordinal))
        {
            variantIndices = new HashSet<int> { sourceIndex };
            decisionReason = "export-variant-index-label-confirmed";
            return true;
        }

        if (string.Equals(versionLabel, "Observed", StringComparison.Ordinal) ||
            versionLabel.StartsWith("Observed:", StringComparison.Ordinal))
        {
            var currentIndices = new List<int>();
            for (var index = 0; index < entry.VariantLabels.Count; index++)
            {
                if (IsCurrentVariantLabel(entry.VariantLabels[index]))
                {
                    currentIndices.Add(index + 1);
                }
            }

            if (currentIndices.Count == 1)
            {
                variantIndices = new HashSet<int> { currentIndices[0] };
                decisionReason = "export-variant-observed-current";
                return true;
            }

            if (currentIndices.Count == 0 && entry.VariantLabels.Count == 1)
            {
                variantIndices = new HashSet<int> { 1 };
                decisionReason = "export-variant-observed-single";
                return true;
            }
        }

        variantIndices = new HashSet<int>();
        decisionReason = "export-variant-unresolved";
        return false;
    }

    public static IReadOnlySet<string> GetTypedOwnerModifierIds(
        PoBExportUniqueOwnershipEntry entry,
        IReadOnlySet<int> variantIndices)
    {
        var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unscoped = variantIndices.Count == 0;
        foreach (var line in entry.EffectLines)
        {
            if (!line.IsTypedModifier || line.ModifierId is null)
            {
                continue;
            }

            if (unscoped)
            {
                if (line.VariantIndices.Count == 0 || entry.VariantLabels.Count == 0)
                {
                    owners.Add(line.ModifierId);
                }
                else
                {
                    // Unscoped observation on a multi-variant entry: only lines that apply to all variants.
                    if (line.VariantIndices.Count == 0)
                    {
                        owners.Add(line.ModifierId);
                    }
                }

                continue;
            }

            if (variantIndices.Any(line.AppliesToVariant))
            {
                owners.Add(line.ModifierId);
            }
        }

        return owners;
    }

    public static IReadOnlyList<string> GetResidualTextsForScope(
        PoBExportUniqueOwnershipEntry entry,
        IReadOnlySet<int> variantIndices)
    {
        var texts = new List<string>();
        var unscoped = variantIndices.Count == 0;
        foreach (var line in entry.EffectLines)
        {
            if (line.IsTypedModifier || string.IsNullOrWhiteSpace(line.ResidualText))
            {
                continue;
            }

            if (unscoped)
            {
                if (line.VariantIndices.Count == 0 || entry.VariantLabels.Count == 0)
                {
                    texts.Add(line.ResidualText);
                }

                continue;
            }

            if (variantIndices.Any(line.AppliesToVariant))
            {
                texts.Add(line.ResidualText);
            }
        }

        return texts;
    }

    private static bool TryParseEffectLine(string line, out PoBExportUniqueEffectLine effect)
    {
        effect = null!;
        var isCrafted = CraftedTagPattern().IsMatch(line);
        var variantIndices = ParseVariantIndices(line);
        var clean = StripDirectives(line).Trim();
        if (clean.Length == 0)
        {
            return false;
        }

        // Strip trailing legacy numeric range markers: ModId[1,2][3,4]
        var withoutRanges = RangeSuffixPattern().Replace(clean, string.Empty);
        if (TypedModifierIdPattern().IsMatch(withoutRanges) && !withoutRanges.Contains(' '))
        {
            effect = new PoBExportUniqueEffectLine
            {
                IsTypedModifier = true,
                ModifierId = withoutRanges,
                ResidualText = null,
                VariantIndices = variantIndices,
                IsCrafted = isCrafted,
            };
            return true;
        }

        effect = new PoBExportUniqueEffectLine
        {
            IsTypedModifier = false,
            ModifierId = null,
            ResidualText = clean,
            VariantIndices = variantIndices,
            IsCrafted = isCrafted,
        };
        return true;
    }

    private static HashSet<int> ParseVariantIndices(string line)
    {
        var indices = new HashSet<int>();
        foreach (Match match in VariantTagPattern().Matches(line))
        {
            foreach (var part in match.Groups["indices"].Value.Split(
                         ',',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var index) && index > 0)
                {
                    indices.Add(index);
                }
            }
        }

        return indices;
    }

    private static string StripDirectives(string line) =>
        DirectivePattern().Replace(line, string.Empty);

    private static bool IsMetadataLine(string line)
    {
        if (MetadataPrefixPattern().IsMatch(line))
        {
            return true;
        }

        return line.StartsWith("Requires Level", StringComparison.Ordinal) ||
               line.StartsWith("Requires ", StringComparison.Ordinal);
    }

    private static bool IsCurrentVariantLabel(string label) =>
        string.Equals(label, "Current", StringComparison.Ordinal) ||
        label.StartsWith("Current ", StringComparison.Ordinal) ||
        label.StartsWith("Current -", StringComparison.Ordinal) ||
        label.EndsWith(" (Current)", StringComparison.Ordinal) ||
        label.EndsWith(" Current", StringComparison.Ordinal);

    [GeneratedRegex(@"\[\[(?<body>.*?)\]\]", RegexOptions.Singleline)]
    private static partial Regex EntryPattern();

    [GeneratedRegex(@"\{variant:(?<indices>[0-9,\s]+)\}", RegexOptions.IgnoreCase)]
    private static partial Regex VariantTagPattern();

    [GeneratedRegex(@"\{crafted\}", RegexOptions.IgnoreCase)]
    private static partial Regex CraftedTagPattern();

    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex DirectivePattern();

    [GeneratedRegex(@"(\[[^\]]*\])+$")]
    private static partial Regex RangeSuffixPattern();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex TypedModifierIdPattern();

    [GeneratedRegex(
        @"^(Variant|League|Source|Implicits|Has Alt Variant|Has Alt Variant Two|Selected Variant|Limited to|Radius|Upgrade|LevelReq|Sockets|Quality):\s*",
        RegexOptions.IgnoreCase)]
    private static partial Regex MetadataPrefixPattern();
}
