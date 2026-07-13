using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed partial class ModifierTextSignatureMatcher
{
    private static readonly ISet<string> NumericOnlyHandlers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "negate",
        "divide_by_one_hundred",
        "per_minute_to_per_second",
        "milliseconds_to_seconds",
        "divide_by_ten_1dp_if_required",
        "locations_to_metres",
        "double",
        "negate_and_double",
        "milliseconds_to_seconds_2dp_if_required",
        "per_minute_to_per_second_2dp_if_required",
        "old_leech_percent",
        "old_leech_permyriad",
        "milliseconds_to_seconds_2dp",
        "divide_by_two_0dp",
        "per_minute_to_per_second_2dp",
        "milliseconds_to_seconds_0dp",
        "divide_by_one_hundred_2dp_if_required",
        "divide_by_one_hundred_2dp",
        "divide_by_ten_0dp",
        "divide_by_ten_1dp",
        "divide_by_two_0dp_if_required",
        "divide_by_five_0dp",
    };

    public ModifierTextSignatureMatchResult Match(
        ModifierDefinition candidate,
        GameDataCatalog catalog,
        IReadOnlyList<string> parsedEffectLines)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(parsedEffectLines);

        var parsedSignatureResult = ModifierTextSignatureNormalizer.CreateParsedSignature(parsedEffectLines);
        var parsedSignature = parsedSignatureResult.Signature;
        var parsedSignatures = ToReadOnly([parsedSignature]);
        if (parsedSignature.Lines.Count == 0)
        {
            return Unknown(
                evaluated: false,
                ModifierTextSignatureMatchReasonCodes.ParsedSignatureEmpty,
                "The parsed modifier has no effect lines to compare.",
                candidateSignatures: [],
                parsedSignatures);
        }

        var candidateBuild = TryCreateCandidateSignature(candidate, catalog);
        if (!candidateBuild.IsSuccess)
        {
            return Unknown(
                evaluated: false,
                candidateBuild.ReasonCode,
                candidateBuild.Reason,
                candidateBuild.Signatures,
                parsedSignatures);
        }

        if (parsedSignatureResult.HasUnsupportedExplanatoryLine)
        {
            return Unknown(
                evaluated: false,
                ModifierTextSignatureMatchReasonCodes.ParsedSignatureUnsupported,
                "The parsed modifier contains an unsupported parenthesized explanatory line.",
                candidateBuild.Signatures,
                parsedSignatures);
        }

        var candidateSignature = candidateBuild.Signatures[0];
        var matches = SignaturesEqual(candidateSignature, parsedSignature);
        return new ModifierTextSignatureMatchResult(
            Evaluated: true,
            matches ? ModifierTextSignatureMatchOutcome.Match : ModifierTextSignatureMatchOutcome.NoMatch,
            matches
                ? ModifierTextSignatureMatchReasonCodes.Match
                : ModifierTextSignatureMatchReasonCodes.NoMatch,
            matches
                ? "The translated modifier text signature matches the parsed effect text signature."
                : "The translated modifier text signature differs from the parsed effect text signature.",
            candidateBuild.Signatures,
            parsedSignatures);
    }

    private static CandidateSignatureBuildResult TryCreateCandidateSignature(
        ModifierDefinition candidate,
        GameDataCatalog catalog)
    {
        var stats = candidate.Stats
            .Where(stat => !string.IsNullOrWhiteSpace(stat.StatId))
            .OrderBy(stat => stat.Index)
            .ToArray();
        if (stats.Length == 0)
        {
            return CandidateSignatureBuildResult.Unknown(
                ModifierTextSignatureMatchReasonCodes.ModifierStatsMissing,
                "The candidate modifier has no stat ids to translate.");
        }

        var lines = new List<string>();
        var position = 0;
        while (position < stats.Length)
        {
            var group = FindNextTranslationGroup(stats, position, catalog);
            if (!group.IsSuccess)
            {
                return CandidateSignatureBuildResult.Unknown(group.ReasonCode, group.Reason, group.Signatures);
            }

            var groupBuild = TryCreateGroupSignature(group.Translation!, group.Stats!);
            if (!groupBuild.IsSuccess)
            {
                return groupBuild;
            }

            lines.AddRange(groupBuild.Signatures[0].Lines);
            position += group.Stats!.Count;
        }

        return CandidateSignatureBuildResult.Success(ModifierTextSignature.Create(lines));
    }

    private static TranslationGroupResult FindNextTranslationGroup(
        IReadOnlyList<ModifierStat> stats,
        int startIndex,
        GameDataCatalog catalog)
    {
        for (var length = stats.Count - startIndex; length >= 1; length--)
        {
            var groupStats = stats.Skip(startIndex).Take(length).ToArray();
            var statIds = groupStats.Select(stat => stat.StatId!.Trim()).ToArray();
            var translations = catalog.FindStatTranslationsByStatIdGroup(statIds);
            if (translations.Count == 0)
            {
                continue;
            }

            if (translations.Count > 1)
            {
                return TranslationGroupResult.Unknown(
                    ModifierTextSignatureMatchReasonCodes.TranslationAmbiguous,
                    "Multiple stat translation records match the same ordered stat-id group.");
            }

            return TranslationGroupResult.Success(translations[0], groupStats);
        }

        return TranslationGroupResult.Unknown(
            ModifierTextSignatureMatchReasonCodes.TranslationMissing,
            "No stat translation record matched the candidate modifier stat ids.");
    }

    private static CandidateSignatureBuildResult TryCreateGroupSignature(
        StatTranslationDefinition translation,
        IReadOnlyList<ModifierStat> stats)
    {
        if (translation.Variants.Count == 0)
        {
            return CandidateSignatureBuildResult.Unknown(
                ModifierTextSignatureMatchReasonCodes.TranslationShapeUnsupported,
                "The stat translation has no variants.");
        }

        if (translation.Variants.Any(variant => variant.Conditions.Any(condition => condition.IsNegated)))
        {
            return CandidateSignatureBuildResult.Unknown(
                ModifierTextSignatureMatchReasonCodes.TranslationConditionUnsupported,
                "Negated stat translation conditions are not supported by text-signature matching yet.");
        }

        var signatures = new List<ModifierTextSignature>();
        foreach (var variant in translation.Variants)
        {
            var match = VariantMatches(variant, stats);
            if (match == VariantConditionMatch.Unresolved)
            {
                return CandidateSignatureBuildResult.Unknown(
                    ModifierTextSignatureMatchReasonCodes.TranslationConditionUnresolved,
                    "The stat translation condition could not be safely resolved for the candidate stat ranges.",
                    signatures);
            }

            if (match == VariantConditionMatch.NoMatch)
            {
                continue;
            }

            var rendered = TryRenderVariantSignature(variant, stats.Count);
            if (!rendered.IsSuccess)
            {
                return rendered;
            }

            signatures.Add(rendered.Signatures[0]);
        }

        var distinctSignatures = signatures
            .Distinct(SignatureComparer.Instance)
            .ToArray();
        return distinctSignatures.Length == 1
            ? CandidateSignatureBuildResult.Success(distinctSignatures[0])
            : CandidateSignatureBuildResult.Unknown(
                ModifierTextSignatureMatchReasonCodes.TranslationConditionUnresolved,
                distinctSignatures.Length == 0
                    ? "No stat translation variant safely matched the candidate stat ranges."
                    : "Multiple stat translation variants matched with different text signatures.",
                distinctSignatures);
    }

    private static VariantConditionMatch VariantMatches(
        StatTranslationVariant variant,
        IReadOnlyList<ModifierStat> stats)
    {
        if (variant.Conditions.Count != stats.Count)
        {
            return VariantConditionMatch.Unresolved;
        }

        if (variant.Conditions
            .Select(condition => condition.Index)
            .Distinct()
            .Count() != variant.Conditions.Count)
        {
            return VariantConditionMatch.Unresolved;
        }

        var conditionsByIndex = variant.Conditions.ToDictionary(condition => condition.Index);
        for (var index = 0; index < stats.Count; index++)
        {
            if (!conditionsByIndex.TryGetValue(index, out var condition))
            {
                return VariantConditionMatch.Unresolved;
            }

            var stat = stats[index];
            if (!stat.MinValue.HasValue || !stat.MaxValue.HasValue)
            {
                if (condition.MinValue.HasValue || condition.MaxValue.HasValue)
                {
                    return VariantConditionMatch.Unresolved;
                }

                continue;
            }

            var min = stat.MinValue.Value;
            var max = stat.MaxValue.Value;
            if (condition.MinValue.HasValue && max < condition.MinValue.Value)
            {
                return VariantConditionMatch.NoMatch;
            }

            if (condition.MaxValue.HasValue && min > condition.MaxValue.Value)
            {
                return VariantConditionMatch.NoMatch;
            }

            if (condition.MinValue.HasValue && min < condition.MinValue.Value ||
                condition.MaxValue.HasValue && max > condition.MaxValue.Value)
            {
                return VariantConditionMatch.Unresolved;
            }
        }

        return VariantConditionMatch.Match;
    }

    private static CandidateSignatureBuildResult TryRenderVariantSignature(
        StatTranslationVariant variant,
        int statCount)
    {
        if (variant.ValueFormats.Count != statCount ||
            variant.IndexHandlers.Count != statCount)
        {
            return CandidateSignatureBuildResult.Unknown(
                ModifierTextSignatureMatchReasonCodes.TranslationShapeUnsupported,
                "The stat translation variant does not align with the stat-id group shape.");
        }

        var replacements = new Dictionary<int, string>();
        for (var index = 0; index < statCount; index++)
        {
            var format = variant.ValueFormats[index].Trim();
            var indexHandlers = variant.IndexHandlers
                .Where(handler => handler.Index == index)
                .ToArray();
            if (indexHandlers.Length != 1 || !HandlersAreSupported(indexHandlers[0].Handlers))
            {
                return CandidateSignatureBuildResult.Unknown(
                    ModifierTextSignatureMatchReasonCodes.TranslationRenderingUnsupported,
                    "The stat translation uses an unsupported index handler for text-signature matching.");
            }

            replacements[index] = format switch
            {
                "#" => "<number>",
                "+#" => "+<number>",
                "ignore" => string.Empty,
                _ => string.Empty,
            };

            if (format is not ("#" or "+#" or "ignore"))
            {
                return CandidateSignatureBuildResult.Unknown(
                    ModifierTextSignatureMatchReasonCodes.TranslationRenderingUnsupported,
                    "The stat translation uses an unsupported value format for text-signature matching.");
            }
        }

        var lines = new List<string>();
        foreach (var formatLine in variant.FormatLines)
        {
            var rendered = formatLine;
            foreach (var replacement in replacements)
            {
                if (replacement.Value.Length == 0 && rendered.Contains($"{{{replacement.Key}}}", StringComparison.Ordinal))
                {
                    return CandidateSignatureBuildResult.Unknown(
                        ModifierTextSignatureMatchReasonCodes.TranslationRenderingUnsupported,
                        "The stat translation hides a value that is required by the rendered format line.");
                }

                rendered = rendered.Replace(
                    $"{{{replacement.Key}}}",
                    replacement.Value,
                    StringComparison.Ordinal);
            }

            if (UnresolvedPlaceholderPattern().IsMatch(rendered))
            {
                return CandidateSignatureBuildResult.Unknown(
                    ModifierTextSignatureMatchReasonCodes.TranslationRenderingUnsupported,
                    "The stat translation contains an unresolved value placeholder.");
            }

            lines.Add(ModifierTextSignatureNormalizer.NormalizeLine(rendered));
        }

        return CandidateSignatureBuildResult.Success(ModifierTextSignature.Create(lines));
    }

    private static bool HandlersAreSupported(IReadOnlyList<string>? handlers)
    {
        if (handlers is null || handlers.Count == 0)
        {
            return true;
        }

        return handlers.All(handler => NumericOnlyHandlers.Contains(handler.Trim()));
    }

    private static bool SignaturesEqual(ModifierTextSignature first, ModifierTextSignature second)
    {
        return first.Lines.SequenceEqual(second.Lines, StringComparer.OrdinalIgnoreCase);
    }

    private static ModifierTextSignatureMatchResult Unknown(
        bool evaluated,
        string reasonCode,
        string reason,
        IReadOnlyList<ModifierTextSignature> candidateSignatures,
        IReadOnlyList<ModifierTextSignature> parsedSignatures)
    {
        return new ModifierTextSignatureMatchResult(
            evaluated,
            ModifierTextSignatureMatchOutcome.Unknown,
            reasonCode,
            reason,
            ToReadOnly(candidateSignatures),
            ToReadOnly(parsedSignatures));
    }

    private static IReadOnlyList<T> ToReadOnly<T>(IEnumerable<T> values)
    {
        return new ReadOnlyCollection<T>(values.ToArray());
    }

    [GeneratedRegex(@"\{\d+\}", RegexOptions.CultureInvariant)]
    private static partial Regex UnresolvedPlaceholderPattern();

    private enum VariantConditionMatch
    {
        Match,
        NoMatch,
        Unresolved,
    }

    private sealed record CandidateSignatureBuildResult(
        bool IsSuccess,
        IReadOnlyList<ModifierTextSignature> Signatures,
        string ReasonCode,
        string Reason)
    {
        public static CandidateSignatureBuildResult Success(ModifierTextSignature signature)
        {
            return new CandidateSignatureBuildResult(
                true,
                ToReadOnly([signature]),
                ModifierTextSignatureMatchReasonCodes.Match,
                "The candidate text signature was rendered.");
        }

        public static CandidateSignatureBuildResult Unknown(
            string reasonCode,
            string reason,
            IReadOnlyList<ModifierTextSignature>? signatures = null)
        {
            return new CandidateSignatureBuildResult(
                false,
                ToReadOnly(signatures ?? []),
                reasonCode,
                reason);
        }
    }

    private sealed record TranslationGroupResult(
        bool IsSuccess,
        StatTranslationDefinition? Translation,
        IReadOnlyList<ModifierStat>? Stats,
        IReadOnlyList<ModifierTextSignature> Signatures,
        string ReasonCode,
        string Reason)
    {
        public static TranslationGroupResult Success(
            StatTranslationDefinition translation,
            IReadOnlyList<ModifierStat> stats)
        {
            return new TranslationGroupResult(
                true,
                translation,
                ToReadOnly(stats),
                [],
                ModifierTextSignatureMatchReasonCodes.Match,
                "A stat translation group was found.");
        }

        public static TranslationGroupResult Unknown(string reasonCode, string reason)
        {
            return new TranslationGroupResult(false, null, null, [], reasonCode, reason);
        }
    }

    private sealed class SignatureComparer : IEqualityComparer<ModifierTextSignature>
    {
        public static readonly SignatureComparer Instance = new();

        public bool Equals(ModifierTextSignature? x, ModifierTextSignature? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return SignaturesEqual(x, y);
        }

        public int GetHashCode(ModifierTextSignature obj)
        {
            var hash = new HashCode();
            foreach (var line in obj.Lines)
            {
                hash.Add(line, StringComparer.OrdinalIgnoreCase);
            }

            return hash.ToHashCode();
        }
    }
}
