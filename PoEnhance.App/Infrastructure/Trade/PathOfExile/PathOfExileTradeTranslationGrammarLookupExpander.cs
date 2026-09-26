using System.Text.RegularExpressions;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

/// <summary>
/// Translation-grammar lookup expansions for Exact Unique provider discovery.
/// TRADE.4c — article-count + singular/plural. TRADE.4e — helper-word branch + signed→unsigned.
/// Each rule requires packaged StatTranslationVariant evidence for the resolved StatId(s).
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

    /// <summary>
    /// TRADE.4e Track A — same translation-family condition branch that inserts a helper word
    /// (e.g. source without <c>additional</c> + companion state line vs Trade with <c>additional</c>).
    /// </summary>
    public static IReadOnlyList<string> ExpandHelperWordBranchLookups(
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
            foreach (var (sourceBranchLines, tradeBranchLines) in EnumerateHelperWordBranchSiblingPairs(translation))
            {
                var sourceLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                    string.Join('\n', sourceBranchLines.Select(ToProviderFormatLine)));
                var tradeLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                    string.Join('\n', tradeBranchLines.Select(ToProviderFormatLine)));
                if (string.IsNullOrWhiteSpace(sourceLookup) ||
                    string.IsNullOrWhiteSpace(tradeLookup) ||
                    string.Equals(sourceLookup, tradeLookup, StringComparison.Ordinal))
                {
                    continue;
                }

                var sourceArity = PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(sourceLookup);
                var tradeArity = PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(tradeLookup);
                if (sourceArity == 0 ||
                    tradeArity == 0 ||
                    sourceArity != tradeArity)
                {
                    continue;
                }

                // Source must match the no-helper branch (allow trailing companion lines already in sourceLookup).
                if (!lookups.Any(lookup =>
                        LookupsCompatibleIgnoringEmbeddedLiterals(lookup, sourceLookup) ||
                        IsSourceLookupWithOptionalTrailingCompanions(lookup, sourceLookup)))
                {
                    continue;
                }

                expanded.Add(tradeLookup);
            }
        }

        return expanded.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// TRADE.4e Track B — signed display placeholder (<c>-#</c>) vs unsigned translation/Trade
    /// format (<c>{0}</c>/<c>#</c>) within the same mechanical family. Does not apply negate /
    /// more-less polarity bridges.
    /// </summary>
    public static IReadOnlyList<string> ExpandSignedSourceToUnsignedFormatLookups(
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
            if (TranslationHasNegateHandler(translation))
            {
                continue;
            }

            foreach (var variant in translation.Variants)
            {
                if (variant.FormatLines.Count == 0 ||
                    !HasUnsignedHashFormat(variant) ||
                    VariantHasNegateHandler(variant))
                {
                    continue;
                }

                var unsignedLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                    string.Join('\n', variant.FormatLines.Select(ToProviderFormatLine)));
                if (string.IsNullOrWhiteSpace(unsignedLookup) ||
                    PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(unsignedLookup) == 0)
                {
                    continue;
                }

                if (!lookups.Any(lookup => IsSignedPlaceholderFormOfUnsigned(lookup, unsignedLookup)))
                {
                    continue;
                }

                expanded.Add(unsignedLookup);
            }
        }

        return expanded.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Proves the Exact Unique signed-display → unsigned Trade magnitude class for bound projection.
    /// </summary>
    public static bool IsSignedSourceUnsignedTradeMagnitudeProjection(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate providerStat,
        GameDataCatalog? gameData)
    {
        if (!component.HasExactUniqueSourceProvenance ||
            PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(providerStat.Text) != 1 ||
            providerStat.OptionMetadata.Count != 0 ||
            gameData is null)
        {
            return false;
        }

        var providerLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
            providerStat.Text);
        if (providerLookup.Contains("-#", StringComparison.Ordinal))
        {
            return false;
        }

        var sourceLookup = GetBestSourceLookup(component);
        if (sourceLookup is null ||
            !sourceLookup.Contains("-#", StringComparison.Ordinal) ||
            !IsSignedPlaceholderFormOfUnsigned(sourceLookup, providerLookup))
        {
            return false;
        }

        foreach (var translation in EnumerateExactStatTranslations(component, gameData))
        {
            if (TranslationHasNegateHandler(translation))
            {
                continue;
            }

            foreach (var variant in translation.Variants)
            {
                if (!HasUnsignedHashFormat(variant) ||
                    VariantHasNegateHandler(variant))
                {
                    continue;
                }

                var unsignedLookup = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                    string.Join('\n', variant.FormatLines.Select(ToProviderFormatLine)));
                if (string.Equals(unsignedLookup, providerLookup, StringComparison.Ordinal) ||
                    LookupsCompatibleIgnoringEmbeddedLiterals(unsignedLookup, providerLookup))
                {
                    return true;
                }
            }
        }

        return false;
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

        // Prefer exact StatId-vector translations; also walk per-StatId families so multi-stat
        // components (e.g. jewel attribute + radius companion) still see the attribute family.
        var yieldedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var translation in gameData.FindStatTranslationsByStatIdGroup(resolved))
        {
            if (translation.Variants.Count > 0 &&
                yieldedIds.Add(translation.Id ?? $"group:{string.Join('|', translation.StatIds)}"))
            {
                yield return translation;
            }
        }

        foreach (var statId in resolved)
        {
            foreach (var translation in gameData.FindStatTranslationsByStatId(statId))
            {
                if (translation.Variants.Count > 0 &&
                    yieldedIds.Add(translation.Id ?? $"stat:{statId}:{string.Join('|', translation.StatIds)}"))
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

    private static IEnumerable<(IReadOnlyList<string> SourceBranchLines, IReadOnlyList<string> TradeBranchLines)>
        EnumerateHelperWordBranchSiblingPairs(StatTranslationDefinition translation)
    {
        var variants = translation.Variants
            .Where(variant => variant.FormatLines.Count > 0 &&
                variant.ValueFormats.Any(format => format is "#" or "+#"))
            .ToArray();
        for (var i = 0; i < variants.Length; i++)
        {
            for (var j = 0; j < variants.Length; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var sourceVariant = variants[i];
                var tradeVariant = variants[j];
                if (!HaveCompatibleQueryFormats(sourceVariant, tradeVariant))
                {
                    continue;
                }

                var sourceLines = sourceVariant.FormatLines
                    .Select(ToProviderFormatLine)
                    .ToArray();
                var tradeLines = tradeVariant.FormatLines
                    .Select(ToProviderFormatLine)
                    .ToArray();
                if (!TryProveHelperWordBranchPair(sourceLines, tradeLines))
                {
                    continue;
                }

                yield return (sourceVariant.FormatLines, tradeVariant.FormatLines);
            }
        }
    }

    private static bool HaveCompatibleQueryFormats(
        StatTranslationVariant left,
        StatTranslationVariant right)
    {
        static int QueryFormatCount(StatTranslationVariant variant) =>
            variant.ValueFormats.Count(format => format is "#" or "+#");

        return QueryFormatCount(left) > 0 &&
            QueryFormatCount(left) == QueryFormatCount(right);
    }

    private static bool TryProveHelperWordBranchPair(
        IReadOnlyList<string> sourceLines,
        IReadOnlyList<string> tradeLines)
    {
        if (tradeLines.Count == 0 ||
            sourceLines.Count < tradeLines.Count)
        {
            return false;
        }

        var sourceIdx = 0;
        var insertions = 0;
        for (var tradeIdx = 0; tradeIdx < tradeLines.Count; tradeIdx++)
        {
            if (sourceIdx >= sourceLines.Count)
            {
                return false;
            }

            if (string.Equals(sourceLines[sourceIdx], tradeLines[tradeIdx], StringComparison.Ordinal))
            {
                sourceIdx++;
                continue;
            }

            if (IsSingleTokenInsertion(sourceLines[sourceIdx], tradeLines[tradeIdx], out _))
            {
                insertions++;
                sourceIdx++;
                continue;
            }

            return false;
        }

        while (sourceIdx < sourceLines.Count)
        {
            if (PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(sourceLines[sourceIdx]) != 0)
            {
                return false;
            }

            sourceIdx++;
        }

        return insertions == 1;
    }

    private static bool IsSingleTokenInsertion(string shorter, string longer, out string insertedToken)
    {
        insertedToken = string.Empty;
        var shortTokens = shorter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var longTokens = longer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (longTokens.Length != shortTokens.Length + 1)
        {
            return false;
        }

        var shortIdx = 0;
        var longIdx = 0;
        var inserted = false;
        while (shortIdx < shortTokens.Length && longIdx < longTokens.Length)
        {
            if (string.Equals(shortTokens[shortIdx], longTokens[longIdx], StringComparison.Ordinal))
            {
                shortIdx++;
                longIdx++;
                continue;
            }

            if (inserted)
            {
                return false;
            }

            insertedToken = longTokens[longIdx];
            inserted = true;
            longIdx++;
        }

        if (longIdx < longTokens.Length)
        {
            if (inserted)
            {
                return false;
            }

            insertedToken = longTokens[longIdx];
            inserted = true;
            longIdx++;
        }

        return inserted &&
            shortIdx == shortTokens.Length &&
            longIdx == longTokens.Length &&
            !string.IsNullOrWhiteSpace(insertedToken);
    }

    private static bool IsSourceLookupWithOptionalTrailingCompanions(
        string actualLookup,
        string sourceBranchLookup)
    {
        if (LookupsCompatibleIgnoringEmbeddedLiterals(actualLookup, sourceBranchLookup))
        {
            return true;
        }

        // Actual may omit trailing zero-slot companion lines that the source branch includes,
        // or include them — compare query-bearing prefixes.
        var actualLines = actualLookup.Split('\n');
        var sourceLines = sourceBranchLookup.Split('\n');
        var actualCore = actualLines
            .Where(line => PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(line) > 0)
            .ToArray();
        var sourceCore = sourceLines
            .Where(line => PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(line) > 0)
            .ToArray();
        if (actualCore.Length == 0 ||
            actualCore.Length != sourceCore.Length)
        {
            return false;
        }

        for (var i = 0; i < actualCore.Length; i++)
        {
            if (!string.Equals(actualCore[i], sourceCore[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Shared non-query prefix lines (e.g. "Consumes Socketed...") must agree when present.
        var actualPrefix = actualLines
            .TakeWhile(line => PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(line) == 0)
            .ToArray();
        var sourcePrefix = sourceLines
            .TakeWhile(line => PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(line) == 0)
            .ToArray();
        if (actualPrefix.Length != sourcePrefix.Length)
        {
            return false;
        }

        for (var i = 0; i < actualPrefix.Length; i++)
        {
            if (!string.Equals(actualPrefix[i], sourcePrefix[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasUnsignedHashFormat(StatTranslationVariant variant)
    {
        if (!variant.ValueFormats.Any(format => format is "#" or "+#"))
        {
            return false;
        }

        var joined = string.Join('\n', variant.FormatLines);
        // Signed format templates encode the minus in the format string itself.
        if (joined.Contains("-{0}", StringComparison.Ordinal) ||
            joined.Contains("-#", StringComparison.Ordinal))
        {
            return false;
        }

        return joined.Contains("{0}", StringComparison.Ordinal) ||
            joined.Contains("#", StringComparison.Ordinal);
    }

    private static bool IsSignedPlaceholderFormOfUnsigned(string signedLookup, string unsignedLookup)
    {
        if (string.IsNullOrWhiteSpace(signedLookup) ||
            string.IsNullOrWhiteSpace(unsignedLookup) ||
            !signedLookup.Contains("-#", StringComparison.Ordinal))
        {
            return false;
        }

        if (unsignedLookup.Contains("-#", StringComparison.Ordinal))
        {
            return false;
        }

        // Strip signed placeholders only — do not touch literal negatives in free text.
        var stripped = SignedPlaceholderRegex().Replace(signedLookup, "#");
        return LookupsCompatibleIgnoringEmbeddedLiterals(stripped, unsignedLookup);
    }

    private static bool TranslationHasNegateHandler(StatTranslationDefinition translation) =>
        translation.Variants.Any(VariantHasNegateHandler);

    private static bool VariantHasNegateHandler(StatTranslationVariant variant) =>
        variant.IndexHandlers.Any(handler =>
            handler.Handlers.Any(id =>
                string.Equals(id, "negate", StringComparison.OrdinalIgnoreCase)));

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

    /// <summary>Matches signed numeric placeholders (<c>-#</c>), not free-text hyphens.</summary>
    [GeneratedRegex(@"-#", RegexOptions.CultureInvariant)]
    private static partial Regex SignedPlaceholderRegex();
}
