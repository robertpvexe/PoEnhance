using System.Text.RegularExpressions;

namespace PoEnhance.Core.Items.GameData;

internal static partial class ModifierTextSignatureNormalizer
{
    public static ModifierTextSignature CreateSignature(IReadOnlyList<string> lines)
    {
        return ModifierTextSignature.Create(lines
            .Select(NormalizeLine)
            .Where(line => line.Length > 0));
    }

    public static ModifierTextSignatureNormalizationResult CreateParsedSignature(IReadOnlyList<string> lines)
    {
        var normalizedLines = new List<string>();
        var hasUnsupportedExplanatoryLine = false;
        foreach (var line in lines)
        {
            if (IsVerifiedReminderLine(line))
            {
                continue;
            }

            if (IsFullParenthesizedLine(line))
            {
                hasUnsupportedExplanatoryLine = true;
                continue;
            }

            var normalized = NormalizeLine(line);
            if (normalized.Length > 0)
            {
                normalizedLines.Add(normalized);
            }
        }

        return new ModifierTextSignatureNormalizationResult(
            ModifierTextSignature.Create(normalizedLines),
            hasUnsupportedExplanatoryLine);
    }

    public static string NormalizeLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        var normalized = line.Trim();
        normalized = RollRangeAnnotationPattern().Replace(normalized, match => match.Groups["roll"].Value);
        normalized = NumberPattern().Replace(normalized, match =>
        {
            var sign = match.Groups["sign"].Value;
            return sign switch
            {
                "+" => "+<number>",
                "-" => "-<number>",
                _ => "<number>",
            };
        });

        normalized = WhitespacePattern().Replace(normalized, " ");
        return normalized.Trim();
    }

    private static bool IsVerifiedReminderLine(string? line)
    {
        var trimmed = line?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        return trimmed.StartsWith("(Only Damage from Hits can be Recouped", StringComparison.Ordinal)
            || trimmed.Equals(
                "(The Damage Types are Physical, Fire, Cold, Lightning, and Chaos)",
                StringComparison.Ordinal);
    }

    private static bool IsFullParenthesizedLine(string? line)
    {
        var trimmed = line?.Trim();
        return trimmed?.Length >= 2 &&
            trimmed[0] == '(' &&
            trimmed[^1] == ')';
    }

    [GeneratedRegex(@"(?<![A-Za-z<])(?<roll>[+-]?\d+(?:[\.,]\d+)?)\(\s*[+-]?\d+(?:[\.,]\d+)?\s*-\s*[+-]?\d+(?:[\.,]\d+)?\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex RollRangeAnnotationPattern();

    [GeneratedRegex(@"(?<![A-Za-z<])(?<sign>[+-]?)(?<number>\d+(?:[\.,]\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}

internal sealed record ModifierTextSignatureNormalizationResult(
    ModifierTextSignature Signature,
    bool HasUnsupportedExplanatoryLine);
