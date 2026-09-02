using System.Text.RegularExpressions;

namespace PoEnhance.Core.Trade;

/// <summary>
/// Conservative fallback for gem/skill-level query shapes when structured
/// <see cref="NumericQueryRole"/> evidence is unavailable. Primary classification
/// lives in <see cref="NumericQueryRoleClassifier"/>.
/// </summary>
internal static partial class GemSkillLevelQuerySemantics
{
    public static bool IsGemOrSkillLevelQuery(IReadOnlyList<string?> signatures)
    {
        for (var index = 0; index < signatures.Count; index++)
        {
            if (MatchesSignature(signatures[index]))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsGemOrSkillLevelQuery(string? signature) => MatchesSignature(signature);

    private static bool MatchesSignature(string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        foreach (var line in signature.Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (MatchesNormalizedLine(NormalizeTemplateMarkers(line)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesNormalizedLine(string line)
    {
        if (!line.Contains("<number>", StringComparison.Ordinal))
        {
            return false;
        }

        return SupportedByLevelRegex().IsMatch(line) ||
            GrantsLevelRegex().IsMatch(line) ||
            LevelOfGemsRegex().IsMatch(line);
    }

    private static string NormalizeTemplateMarkers(string line)
    {
        var normalized = WhitespaceRegex().Replace(line.Trim(), " ");
        normalized = normalized
            .Replace("+#", "+<number>", StringComparison.Ordinal)
            .Replace("-#", "-<number>", StringComparison.Ordinal);
        normalized = BareHashRegex().Replace(normalized, "<number>");
        return normalized;
    }

    [GeneratedRegex(
        @"\bSupported by Level <number>(?:\b|(?=\s)|$)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SupportedByLevelRegex();

    [GeneratedRegex(
        @"\bGrants Level <number>(?:\b|(?=\s)|$)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex GrantsLevelRegex();

    [GeneratedRegex(
        @"^[+]?<number> to Level of\b.*\bGems?\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LevelOfGemsRegex();

    [GeneratedRegex(@"#", RegexOptions.CultureInvariant)]
    private static partial Regex BareHashRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
