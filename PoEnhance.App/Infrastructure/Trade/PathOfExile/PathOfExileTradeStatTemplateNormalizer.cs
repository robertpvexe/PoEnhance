using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static partial class PathOfExileTradeStatTemplateNormalizer
{
    private const string LocalProviderAnnotationSuffix = " (Local)";

    public static string NormalizeTemplate(string? text)
    {
        var normalizedText = NormalizeText(text);
        return NumberRegex().Replace(normalizedText, match =>
        {
            var value = match.Value;
            return value[0] is '+' or '-'
                ? string.Concat(value.AsSpan(0, 1), "#")
                : "#";
        });
    }

    public static string NormalizeLookupTemplate(string? text)
    {
        return StripProviderLocalAnnotation(NormalizeTemplate(text));
    }

    public static bool HasProviderLocalAnnotation(string? text)
    {
        return NormalizeText(text).EndsWith(
            LocalProviderAnnotationSuffix,
            StringComparison.OrdinalIgnoreCase);
    }

    public static PathOfExileTradeStatModifierNormalization NormalizeModifierText(
        string? text)
    {
        var normalizedText = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return new PathOfExileTradeStatModifierNormalization
            {
                NormalizedTemplate = string.Empty,
                ExtractedNumericValues = [],
            };
        }

        if (UnsupportedCommaNumberRegex().IsMatch(normalizedText))
        {
            return new PathOfExileTradeStatModifierNormalization
            {
                NormalizedTemplate = normalizedText,
                ExtractedNumericValues = [],
                Diagnostic = new PathOfExileTradeStatMatchDiagnostic(
                    PathOfExileTradeStatMatchDiagnosticCodes.UnsupportedNumericTokenFormat,
                    "The modifier text contains an unsupported numeric token format."),
            };
        }

        if (HasMalformedAttachedRangeAnnotation(normalizedText))
        {
            return new PathOfExileTradeStatModifierNormalization
            {
                NormalizedTemplate = normalizedText,
                ExtractedNumericValues = [],
                Diagnostic = new PathOfExileTradeStatMatchDiagnostic(
                    PathOfExileTradeStatMatchDiagnosticCodes.MalformedAdvancedRangeAnnotation,
                    "The modifier text contains an unsupported attached numeric range annotation."),
            };
        }

        normalizedText = StrictAttachedRangeAnnotationRegex().Replace(
            normalizedText,
            match => match.Groups["roll"].Value);

        var values = new List<decimal>();
        var normalizedTemplate = NumberRegex().Replace(normalizedText, match =>
        {
            var token = match.Value;
            if (!decimal.TryParse(
                token,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var parsedValue))
            {
                return token;
            }

            values.Add(parsedValue);
            return token[0] is '+' or '-'
                ? string.Concat(token.AsSpan(0, 1), "#")
                : "#";
        });

        return new PathOfExileTradeStatModifierNormalization
        {
            NormalizedTemplate = normalizedTemplate,
            ExtractedNumericValues = values,
        };
    }

    private static bool HasMalformedAttachedRangeAnnotation(string text)
    {
        return AttachedParenthesisAfterNumberRegex().Matches(text)
            .Cast<Match>()
            .Any(match => !StrictAttachedRangeAnnotationRegex().IsMatch(match.Value));
    }

    private static string StripProviderLocalAnnotation(string text)
    {
        return text.EndsWith(LocalProviderAnnotationSuffix, StringComparison.OrdinalIgnoreCase)
            ? text[..^LocalProviderAnnotationSuffix.Length]
            : text;
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var previousWasWhitespace = false;
        foreach (var character in text.Trim())
        {
            var normalized = character switch
            {
                '\u00a0' => ' ',
                '\u2010' or '\u2011' or '\u2012' or '\u2013' or '\u2014' or '\u2212' => '-',
                _ => character,
            };

            if (char.IsWhiteSpace(normalized))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }
            }
            else
            {
                builder.Append(normalized);
                previousWasWhitespace = false;
            }
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"(?<![\w#])[\+\-]?\d+(?:\.\d+)?(?![\w#])", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\d+,\d+", RegexOptions.CultureInvariant)]
    private static partial Regex UnsupportedCommaNumberRegex();

    [GeneratedRegex(
        @"(?<![\w#])(?<roll>[\+\-]?\d+(?:\.\d+)?)\((?<minimum>[\+\-]?\d+(?:\.\d+)?)-(?<maximum>[\+\-]?\d+(?:\.\d+)?)\)",
        RegexOptions.CultureInvariant)]
    private static partial Regex StrictAttachedRangeAnnotationRegex();

    [GeneratedRegex(
        @"(?<![\w#])[\+\-]?\d+(?:\.\d+)?\([^)]*\)",
        RegexOptions.CultureInvariant)]
    private static partial Regex AttachedParenthesisAfterNumberRegex();
}
