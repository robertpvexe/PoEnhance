using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PoEnhance.GameData;

/// <summary>Deterministic structured identities; rendered prose is never the mechanical identity.</summary>
public static partial class StatTranslationStructuralSemantics
{
    public static string MechanicalSignature(StatTranslationDefinition translation) =>
        Hash(MechanicalCanonicalForm(translation));

    public static string RenderingSignature(StatTranslationDefinition translation) =>
        Hash(RenderingCanonicalForm(translation));

    public static string NumericShapeSignature(StatTranslationDefinition translation) =>
        Hash(NumericShapeCanonicalForm(translation));

    public static string MechanicalCanonicalForm(StatTranslationDefinition translation)
    {
        ArgumentNullException.ThrowIfNull(translation);
        return Join(
            Vector(translation.StatIds),
            translation.Language?.Trim() ?? string.Empty,
            string.Join('\u001c', translation.Variants.Select((variant, index) => Join(
                index.ToString(CultureInfo.InvariantCulture),
                Conditions(variant),
                ValueShape(variant.ValueFormats),
                Handlers(variant),
                PlaceholderTopology(variant.FormatLines)))));
    }

    public static string RenderingCanonicalForm(StatTranslationDefinition translation)
    {
        ArgumentNullException.ThrowIfNull(translation);
        return Join(
            MechanicalCanonicalForm(translation),
            string.Join('\u001c', translation.Variants.Select(variant => Join(
                Vector(variant.ValueFormats),
                Vector(variant.FormatLines)))));
    }

    public static string NumericShapeCanonicalForm(StatTranslationDefinition translation)
    {
        ArgumentNullException.ThrowIfNull(translation);
        return string.Join('\u001c', translation.Variants.Select(variant => Join(
                string.Join(',', variant.ValueFormats.Select((format, index) =>
                    IsNumeric(format) ? index.ToString(CultureInfo.InvariantCulture) : string.Empty)
                    .Where(value => value.Length > 0)),
                variant.FormatLines.Count.ToString(CultureInfo.InvariantCulture),
                string.Join(',', variant.FormatLines.Select(line =>
                    PlaceholderPattern().Matches(line).Count.ToString(CultureInfo.InvariantCulture))))));
    }

    private static string Conditions(StatTranslationVariant variant) => string.Join(';',
        variant.Conditions.Select((condition, position) => Join(
            position.ToString(CultureInfo.InvariantCulture),
            condition.Index.ToString(CultureInfo.InvariantCulture),
            Number(condition.MinValue),
            Number(condition.MaxValue),
            condition.IsNegated ? "1" : "0")));

    private static string Handlers(StatTranslationVariant variant) => string.Join(';',
        variant.IndexHandlers.Select((handler, position) => Join(
            position.ToString(CultureInfo.InvariantCulture),
            handler.Index.ToString(CultureInfo.InvariantCulture),
            Vector(handler.Handlers))));

    private static string PlaceholderTopology(IReadOnlyList<string> lines) => string.Join(';',
        lines.Select((line, lineIndex) => Join(
            lineIndex.ToString(CultureInfo.InvariantCulture),
            string.Join(',', PlaceholderPattern().Matches(line)
                .Select(match => match.Groups[1].Value)))));

    private static string ValueShape(IReadOnlyList<string> formats) => string.Join(',', formats.Select(format =>
        IsNumeric(format) ? "numeric" : format.Trim().Equals("ignore", StringComparison.OrdinalIgnoreCase)
            ? "ignore"
            : $"other:{format.Trim()}"));

    private static bool IsNumeric(string? format) => format?.Trim() is "#" or "+#";

    private static string Number(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Vector(IEnumerable<string> values) =>
        string.Join('\u001e', values.Select(value => value?.Trim() ?? string.Empty));

    private static string Join(params string[] values) => string.Join('\u001f', values);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    [GeneratedRegex(@"\{(\d+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();
}
