using System.Text.RegularExpressions;

namespace PoEnhance.DataImport;

/// <summary>
/// Deterministic parser for pinned PoB <c>src/Export/Uniques/ModTextMap.lua</c>.
/// Does not interpret general Lua — only the generated map grammar.
/// </summary>
public static partial class PoBModTextMapParser
{
    public const string RelativePath = "src/Export/Uniques/ModTextMap.lua";

    public static PoBModTextMapIndex LoadFromPoBSourceRoot(string pobSourceRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pobSourceRootPath);
        var path = Path.Combine(pobSourceRootPath, "src", "Export", "Uniques", "ModTextMap.lua");
        return LoadFromFile(path);
    }

    public static PoBModTextMapIndex LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Pinned Path of Building ModTextMap was not found: {path}",
                path);
        }

        return ParseText(File.ReadAllText(path));
    }

    public static PoBModTextMapIndex ParseText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var entries = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (Match match in EntryPattern().Matches(text))
        {
            var key = NormalizeKey(UnescapeLuaString(match.Groups["key"].Value));
            if (key.Length == 0)
            {
                continue;
            }

            var modifierIds = new List<string>();
            foreach (Match idMatch in ModifierIdPattern().Matches(match.Groups["ids"].Value))
            {
                var modifierId = idMatch.Groups["id"].Value.Trim();
                if (modifierId.Length > 0)
                {
                    modifierIds.Add(modifierId);
                }
            }

            if (modifierIds.Count == 0)
            {
                continue;
            }

            // First declaration wins; generated file should not collide after normalization.
            entries.TryAdd(key, modifierIds);
        }

        return new PoBModTextMapIndex(entries);
    }

    public static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return WhitespacePattern().Replace(value.Trim(), " ").ToLowerInvariant();
    }

    private static string UnescapeLuaString(string value) =>
        value.Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);

    [GeneratedRegex(
        @"\[\s*""(?<key>(?:\\.|[^""])*)""\s*\]\s*=\s*\{\s*(?<ids>.*?)\s*\},",
        RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex EntryPattern();

    [GeneratedRegex(@"""(?<id>[^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex ModifierIdPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}
