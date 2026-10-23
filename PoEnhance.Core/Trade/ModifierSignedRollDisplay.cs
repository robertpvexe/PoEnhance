using System.Globalization;
using System.Text.RegularExpressions;

namespace PoEnhance.Core.Trade;

/// <summary>
/// Formats sourced roll annotations for UI so zero-straddling and negative endpoints stay
/// unambiguous. Does not alter matching/catalog Exact lines — display only.
/// </summary>
internal static partial class ModifierSignedRollDisplay
{
    /// <summary>
    /// Rewrites parenthetical numeric ranges that include a negative endpoint (or a
    /// double-hyphen signed form) into ascending <c>(low to high)</c> with explicit signs.
    /// Positive-only ranges such as <c>(10-16)</c> are left unchanged.
    /// </summary>
    public static string FormatLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return line;
        }

        return SignedRangePattern().Replace(line, match =>
        {
            if (!decimal.TryParse(
                    match.Groups["first"].Value,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var first) ||
                !decimal.TryParse(
                    match.Groups["second"].Value,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var second))
            {
                return match.Value;
            }

            if (first >= 0m && second >= 0m)
            {
                return match.Value;
            }

            var minimum = Math.Min(first, second);
            var maximum = Math.Max(first, second);
            return $"({FormatSigned(minimum)} to {FormatSigned(maximum)})";
        });
    }

    public static IReadOnlyList<string> FormatLines(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return lines.Select(FormatLine).ToArray();
    }

    private static string FormatSigned(decimal value)
    {
        var magnitude = decimal.Abs(value).ToString("G29", CultureInfo.InvariantCulture);
        if (value < 0m)
        {
            return $"-{magnitude}";
        }

        if (value > 0m)
        {
            return $"+{magnitude}";
        }

        return magnitude;
    }

    // Captures (a-b), (a--b), (-a-b), (-a--b) with ASCII/Unicode dashes. Positive-only left intact.
    [GeneratedRegex(
        @"\((?<first>[+-]?\d+(?:\.\d+)?)\s*[-–—]\s*(?<second>[+-]?\d+(?:\.\d+)?)\)",
        RegexOptions.CultureInvariant)]
    private static partial Regex SignedRangePattern();
}
