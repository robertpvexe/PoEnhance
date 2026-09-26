using System.Text.RegularExpressions;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

/// <summary>
/// TRADE.4c — two independent translation-grammar lookup expansions for Exact Unique provider
/// discovery. Both require packaged StatTranslationVariant evidence for the resolved StatId(s).
/// </summary>
internal static partial class PathOfExileTradeTranslationGrammarLookupExpander
{
    /// <summary>
    /// Track A — numeric-count format branch (<c>#/{0} …</c>) vs Trade article branch
    /// (<c>An/A …</c>, <c>ignore</c> format) within the same translation family.
    /// </summary>
    public static IReadOnlyList<string> ExpandNumericCountToArticleLookups(
        ResolvedSearchComponent component,
        IReadOnlyList<string> lookups,
        GameDataCatalog? gameData)
    {
        if (!component.HasExactUniqueSourceProvenance ||
            lookups.Count == 0)
        {
            return [];
        }

        var expanded = new List<string>();
        foreach (var translation in EnumerateExactStatTranslations(component, gameData))
        {
            if (!TryGetArticleAndNumericCountSiblings(translation, out var articleLines, out _))
            {
                continue;
            }

            var articleLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                string.Join('\n', articleLines));
            if (string.IsNullOrWhiteSpace(articleLookup) ||
                PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(articleLookup) != 0)
            {
                continue;
            }

            // Only expand when at least one source lookup is the numeric-count sibling form.
            if (!lookups.Any(lookup =>
                    IsNumericCountSiblingOfArticle(lookup, articleLookup)))
            {
                continue;
            }

            expanded.Add(articleLookup);
        }

        return expanded.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Track B — same-StatId singular/plural noun inflection where Trade publishes the singular
    /// canonical form with numeric <c>#</c>.
    /// </summary>
    public static IReadOnlyList<string> ExpandPluralToSingularInflectionLookups(
        ResolvedSearchComponent component,
        IReadOnlyList<string> lookups,
        GameDataCatalog? gameData)
    {
        if (!component.HasExactUniqueSourceProvenance ||
            lookups.Count == 0)
        {
            return [];
        }

        var expanded = new List<string>();
        foreach (var translation in EnumerateExactStatTranslations(component, gameData))
        {
            if (!TryGetSingularPluralInflectionSiblings(
                    translation,
                    out var singularLines,
                    out var pluralLines))
            {
                continue;
            }

            var singularLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                string.Join('\n', singularLines.Select(ToProviderFormatLine)));
            var pluralLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                string.Join('\n', pluralLines.Select(ToProviderFormatLine)));
            if (string.IsNullOrWhiteSpace(singularLookup) ||
                string.IsNullOrWhiteSpace(pluralLookup) ||
                string.Equals(singularLookup, pluralLookup, StringComparison.Ordinal))
            {
                continue;
            }

            // Source must match the plural branch; expand to Trade's singular canonical lookup.
            if (!lookups.Any(lookup =>
                    LookupsCompatibleIgnoringEmbeddedLiterals(lookup, pluralLookup)))
            {
                continue;
            }

            expanded.Add(singularLookup);
        }

        return expanded.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static bool IsNumericCountArticlePresenceProjection(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate providerStat,
        GameDataCatalog? gameData)
    {
        if (!component.HasExactUniqueSourceProvenance ||
            PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(providerStat.Text) != 0 ||
            providerStat.OptionMetadata.Count != 0 ||
            gameData is null)
        {
            return false;
        }

        var providerLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
            providerStat.Text);
        foreach (var translation in EnumerateExactStatTranslations(component, gameData))
        {
            if (!TryGetArticleAndNumericCountSiblings(translation, out var articleLines, out _))
            {
                continue;
            }

            var articleLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                string.Join('\n', articleLines));
            if (string.Equals(providerLookup, articleLookup, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryGetTranslationInflectionQueryScalar(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate providerStat,
        GameDataCatalog? gameData,
        out decimal scalar)
    {
        scalar = 0m;
        if (!component.HasExactUniqueSourceProvenance ||
            PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(providerStat.Text) != 1 ||
            providerStat.OptionMetadata.Count != 0 ||
            gameData is null)
        {
            return false;
        }

        var providerLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
            providerStat.Text);
        var embedded = PathOfExileTradeStatTemplateNormalizer
            .NormalizeModifierText(providerStat.Text)
            .ExtractedNumericValues;

        foreach (var translation in EnumerateExactStatTranslations(component, gameData))
        {
            if (!TryGetSingularPluralInflectionSiblings(
                    translation,
                    out var singularLines,
                    out var pluralLines))
            {
                continue;
            }

            var singularLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                string.Join('\n', singularLines.Select(ToProviderFormatLine)));
            if (!string.Equals(providerLookup, singularLookup, StringComparison.Ordinal))
            {
                continue;
            }

            var pluralLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                string.Join('\n', pluralLines.Select(ToProviderFormatLine)));
            var sourceLookup = GetBestSourceLookup(component);
            if (sourceLookup is null ||
                !LookupsCompatibleIgnoringEmbeddedLiterals(sourceLookup, pluralLookup))
            {
                continue;
            }

            // Prefer the observed value that fills the Trade # slot (not embedded literals).
            var observed = component.ObservedNumericValues.Count > 0
                ? component.ObservedNumericValues
                : component.ProviderFallbackNumericValues;
            if (observed.Count == 0)
            {
                continue;
            }

            var queryValues = observed
                .Where(value => !embedded.Contains(value))
                .ToArray();
            if (queryValues.Length == 1)
            {
                scalar = queryValues[0];
                return true;
            }

            // Fallback: first observed when embedded sets are disjoint by position.
            if (observed.Count >= 1 &&
                (embedded.Count == 0 || !embedded.Contains(observed[0])))
            {
                scalar = observed[0];
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<StatTranslationDefinition> EnumerateExactStatTranslations(
        ResolvedSearchComponent component,
        GameDataCatalog? gameData)
    {
        var fromRecognition = component.TranslationRecognition?.CanonicalTranslation ??
            component.TranslationRecognition?.RecognizedTranslation;
        if (fromRecognition is not null &&
            fromRecognition.Variants.Count > 0)
        {
            yield return fromRecognition;
        }

        if (gameData is null)
        {
            yield break;
        }

        var resolved = component.ResolvedStatIds
            .Where(statId => !string.IsNullOrWhiteSpace(statId))
            .Select(statId => statId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (resolved.Length == 0)
        {
            yield break;
        }

        // Prefer exact StatId-vector translations; fall back to per-StatId families.
        foreach (var translation in gameData.FindStatTranslationsByStatIdGroup(resolved))
        {
            if (translation.Variants.Count > 0)
            {
                yield return translation;
            }
        }

        if (resolved.Length == 1)
        {
            foreach (var translation in gameData.FindStatTranslationsByStatId(resolved[0]))
            {
                if (translation.Variants.Count > 0)
                {
                    yield return translation;
                }
            }
        }
    }

    private static bool TryGetArticleAndNumericCountSiblings(
        StatTranslationDefinition translation,
        out IReadOnlyList<string> articleLines,
        out IReadOnlyList<string> numericLines)
    {
        articleLines = [];
        numericLines = [];
        StatTranslationVariant? article = null;
        StatTranslationVariant? numeric = null;
        foreach (var variant in translation.Variants)
        {
            if (variant.FormatLines.Count == 0)
            {
                continue;
            }

            var hasIgnore = variant.ValueFormats.Any(format =>
                string.Equals(format, "ignore", StringComparison.OrdinalIgnoreCase));
            var hasHash = variant.ValueFormats.Any(format => format is "#" or "+#");
            var joined = string.Join('\n', variant.FormatLines);
            if (hasIgnore &&
                ArticlePrefixRegex().IsMatch(joined) &&
                article is null)
            {
                article = variant;
            }

            if (hasHash &&
                (joined.Contains("{0}", StringComparison.Ordinal) ||
                    joined.Contains("{0:", StringComparison.Ordinal)) &&
                numeric is null)
            {
                numeric = variant;
            }
        }

        if (article is null || numeric is null)
        {
            return false;
        }

        var articleJoined = string.Join('\n', article.FormatLines);
        var numericJoined = string.Join('\n', numeric.FormatLines);
        var articleAsCount = ArticlePrefixRegex().Replace(articleJoined, "{0} ");
        var numericNormalized = numericJoined.Replace("{0}", "{0}", StringComparison.Ordinal);
        if (!string.Equals(
                NormalizeComparableGrammar(articleAsCount),
                NormalizeComparableGrammar(numericNormalized),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        articleLines = article.FormatLines;
        numericLines = numeric.FormatLines;
        return true;
    }

    private static bool TryGetSingularPluralInflectionSiblings(
        StatTranslationDefinition translation,
        out IReadOnlyList<string> singularLines,
        out IReadOnlyList<string> pluralLines)
    {
        singularLines = [];
        pluralLines = [];
        var variants = translation.Variants
            .Where(variant => variant.FormatLines.Count > 0 &&
                variant.ValueFormats.Any(format => format is "#" or "+#"))
            .ToArray();
        for (var i = 0; i < variants.Length; i++)
        {
            for (var j = i + 1; j < variants.Length; j++)
            {
                var left = variants[i];
                var right = variants[j];
                if (!left.ValueFormats.SequenceEqual(right.ValueFormats, StringComparer.Ordinal) ||
                    left.FormatLines.Count != right.FormatLines.Count)
                {
                    continue;
                }

                if (!TryGetSingleSingularPluralDiff(
                        left.FormatLines,
                        right.FormatLines,
                        out var leftIsSingular))
                {
                    continue;
                }

                if (leftIsSingular)
                {
                    singularLines = left.FormatLines;
                    pluralLines = right.FormatLines;
                }
                else
                {
                    singularLines = right.FormatLines;
                    pluralLines = left.FormatLines;
                }

                return true;
            }
        }

        return false;
    }

    private static bool TryGetSingleSingularPluralDiff(
        IReadOnlyList<string> leftLines,
        IReadOnlyList<string> rightLines,
        out bool leftIsSingular)
    {
        leftIsSingular = false;
        var diffs = 0;
        string? leftToken = null;
        string? rightToken = null;
        for (var line = 0; line < leftLines.Count; line++)
        {
            if (string.Equals(leftLines[line], rightLines[line], StringComparison.Ordinal))
            {
                continue;
            }

            diffs++;
            var leftTokens = leftLines[line].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var rightTokens = rightLines[line].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (leftTokens.Length != rightTokens.Length)
            {
                return false;
            }

            var tokenDiffs = 0;
            for (var t = 0; t < leftTokens.Length; t++)
            {
                if (string.Equals(leftTokens[t], rightTokens[t], StringComparison.Ordinal))
                {
                    continue;
                }

                tokenDiffs++;
                leftToken = leftTokens[t];
                rightToken = rightTokens[t];
            }

            if (tokenDiffs != 1)
            {
                return false;
            }
        }

        if (diffs != 1 ||
            leftToken is null ||
            rightToken is null ||
            !IsRegularSingularPluralPair(leftToken, rightToken, out leftIsSingular))
        {
            return false;
        }

        return true;
    }

    private static bool IsRegularSingularPluralPair(string left, string right, out bool leftIsSingular)
    {
        leftIsSingular = false;
        left = left.Trim().TrimEnd(',', '.', ';', ':');
        right = right.Trim().TrimEnd(',', '.', ';', ':');
        if (string.Equals(left + "s", right, StringComparison.OrdinalIgnoreCase))
        {
            leftIsSingular = true;
            return true;
        }

        if (string.Equals(right + "s", left, StringComparison.OrdinalIgnoreCase))
        {
            leftIsSingular = false;
            return true;
        }

        return false;
    }

    private static bool IsNumericCountSiblingOfArticle(string numericLookup, string articleLookup)
    {
        if (string.IsNullOrWhiteSpace(numericLookup) ||
            string.IsNullOrWhiteSpace(articleLookup))
        {
            return false;
        }

        // "# Enemy …" vs "An Enemy …" / "A Enemy …"
        if (numericLookup.StartsWith("# ", StringComparison.Ordinal) &&
            (articleLookup.StartsWith("An ", StringComparison.Ordinal) ||
                articleLookup.StartsWith("A ", StringComparison.Ordinal)))
        {
            var numericRest = numericLookup[2..];
            var articleRest = articleLookup.StartsWith("An ", StringComparison.Ordinal)
                ? articleLookup[3..]
                : articleLookup[2..];
            return string.Equals(numericRest, articleRest, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool LookupsCompatibleIgnoringEmbeddedLiterals(string left, string right)
    {
        var leftNorm = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(left);
        var rightNorm = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(right);
        if (string.Equals(leftNorm, rightNorm, StringComparison.Ordinal))
        {
            return true;
        }

        // Source may hash all numbers; Trade singular variant keeps embedded literals as digits
        // until NormalizeLookupTemplate collapses them to #.
        return string.Equals(leftNorm, rightNorm, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetBestSourceLookup(ResolvedSearchComponent component)
    {
        IEnumerable<string?> texts =
        [
            component.ProviderCanonicalSignature,
            component.CanonicalSignature,
            .. component.ProviderSearchSignatures,
            component.OriginalText,
        ];
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var lookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                text
                    .Replace("+<number>", "+#", StringComparison.Ordinal)
                    .Replace("-<number>", "-#", StringComparison.Ordinal)
                    .Replace("<number>", "#", StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(lookup))
            {
                return lookup;
            }
        }

        return null;
    }

    private static string ToProviderFormatLine(string formatLine) =>
        formatLine
            .Replace("{0}", "#", StringComparison.Ordinal)
            .Replace("{1}", "#", StringComparison.Ordinal)
            .Replace("{2}", "#", StringComparison.Ordinal);

    private static string NormalizeComparableGrammar(string text) =>
        Regex.Replace(text.Trim(), @"\s+", " ");

    [GeneratedRegex(@"^(An|A)\s+", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ArticlePrefixRegex();
}
