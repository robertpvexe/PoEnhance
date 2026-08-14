using System.Globalization;
using System.Text;

namespace PoEnhance.DataImport;

internal static class UniqueSourceIdentityNormalizer
{
    public const string ExactRule = "exact-trimmed-source-text-v1";
    public const string CanonicalRule = "unicode-form-d-casefold-diacritic-punctuation-v1";

    public static string NormalizeKey(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingWhitespace = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                pendingWhitespace = builder.Length > 0;
                continue;
            }

            if (pendingWhitespace)
            {
                builder.Append(' ');
                pendingWhitespace = false;
            }

            builder.Append(char.ToLowerInvariant(character) switch
            {
                '\u2018' or '\u2019' or '\u02bc' or '\uff07' => '\'',
                '\u2010' or '\u2011' or '\u2012' or '\u2013' or '\u2014' or '\u2212' => '-',
                var normalized => normalized,
            });
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string RuleFor(string sourceText, string canonicalText) =>
        string.Equals(sourceText.Trim(), canonicalText.Trim(), StringComparison.Ordinal)
            ? ExactRule
            : CanonicalRule;
}
