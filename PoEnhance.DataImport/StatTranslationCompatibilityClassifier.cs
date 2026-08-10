using System.Security.Cryptography;
using System.Text;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

internal static class StatTranslationCompatibilityClassifier
{
    public static StatTranslationCompatibilityComparison Compare(
        StatTranslationDefinition current,
        StatTranslationDefinition historical,
        int usageCount,
        bool specialOnly,
        IReadOnlyList<bool>? currentLocalities = null,
        IReadOnlyList<bool>? historicalLocalities = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(historical);

        var currentMechanical = WithLocality(
            StatTranslationStructuralSemantics.MechanicalCanonicalForm(current),
            currentLocalities);
        var historicalMechanical = WithLocality(
            StatTranslationStructuralSemantics.MechanicalCanonicalForm(historical),
            historicalLocalities);
        var currentMechanicalSignature = Hash(currentMechanical);
        var historicalMechanicalSignature = Hash(historicalMechanical);
        var currentRenderingSignature = StatTranslationStructuralSemantics.RenderingSignature(current);
        var historicalRenderingSignature = StatTranslationStructuralSemantics.RenderingSignature(historical);
        var currentNumericShape = StatTranslationStructuralSemantics.NumericShapeSignature(current);
        var historicalNumericShape = StatTranslationStructuralSemantics.NumericShapeSignature(historical);

        var literalMechanicsChanged = LiteralMechanicsChanged(current, historical);
        var classification = usageCount == 0
            ? StatTranslationCompatibilityClassification.NoRuntimeImpact
            : specialOnly
                ? StatTranslationCompatibilityClassification.SpecialOnlyUnsupported
                : !string.Equals(currentNumericShape, historicalNumericShape, StringComparison.Ordinal)
                    ? StatTranslationCompatibilityClassification.NumericShapeChanged
                    : !string.Equals(currentMechanicalSignature, historicalMechanicalSignature, StringComparison.Ordinal)
                        ? StatTranslationCompatibilityClassification.MechanicsChanged
                        : literalMechanicsChanged
                            ? StatTranslationCompatibilityClassification.MechanicsChanged
                        : ValueFormatsEqual(current, historical)
                            ? StatTranslationCompatibilityClassification.MechanicallyEquivalentRendering
                            : StatTranslationCompatibilityClassification.EquivalentWithCanonicalizationChange;

        return new StatTranslationCompatibilityComparison(
            classification,
            currentMechanicalSignature,
            historicalMechanicalSignature,
            currentRenderingSignature,
            historicalRenderingSignature,
            currentNumericShape,
            historicalNumericShape,
            literalMechanicsChanged);
    }

    private static bool ValueFormatsEqual(
        StatTranslationDefinition current,
        StatTranslationDefinition historical) =>
        current.Variants.Select(variant => string.Join('\u001f', variant.ValueFormats))
            .SequenceEqual(
                historical.Variants.Select(variant => string.Join('\u001f', variant.ValueFormats)),
                StringComparer.Ordinal);

    private static bool LiteralMechanicsChanged(
        StatTranslationDefinition current,
        StatTranslationDefinition historical)
    {
        var hasAnyNumericValue = current.Variants.Concat(historical.Variants)
            .SelectMany(variant => variant.ValueFormats)
            .Any(format => format.Trim() is "#" or "+#");
        if (!hasAnyNumericValue)
        {
            return true;
        }

        var mechanicalWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "increased", "reduced", "more", "less", "double", "triple",
            "every", "third", "fourth", "fifth", "sixth",
        };
        return MechanicalWords(current, mechanicalWords)
            .SetEquals(MechanicalWords(historical, mechanicalWords)) == false;
    }

    private static HashSet<string> MechanicalWords(
        StatTranslationDefinition translation,
        ISet<string> vocabulary) => translation.Variants
        .SelectMany(variant => variant.FormatLines)
        .SelectMany(line => line.Split(
            [' ', '\t', ',', '.', ':', ';', '(', ')'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Where(vocabulary.Contains)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string WithLocality(string mechanical, IReadOnlyList<bool>? localities) =>
        localities is null
            ? mechanical
            : $"{mechanical}\u001f{string.Join(',', localities.Select(value => value ? 'L' : 'G'))}";

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

internal sealed record StatTranslationCompatibilityComparison(
    StatTranslationCompatibilityClassification Classification,
    string CurrentMechanicalSignature,
    string HistoricalMechanicalSignature,
    string CurrentRenderingSignature,
    string HistoricalRenderingSignature,
    string CurrentNumericShapeSignature,
    string HistoricalNumericShapeSignature,
    bool LiteralMechanicsChanged);
