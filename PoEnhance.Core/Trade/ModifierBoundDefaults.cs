using System.Globalization;
using System.Text.RegularExpressions;
using PoEnhance.GameData;

namespace PoEnhance.Core.Trade;

/// <summary>Derives provider-neutral numeric bound evidence from retained GameData translation provenance.</summary>
internal static partial class ModifierBoundDefaults
{
    private static readonly HashSet<string> OrderPreservingHandlers = new(StringComparer.OrdinalIgnoreCase)
    {
        "30%_of_value",
        "60%_of_value",
        "divide_by_fifteen_0dp",
        "divide_by_five",
        "divide_by_four",
        "divide_by_one_hundred",
        "divide_by_one_hundred_2dp",
        "divide_by_one_hundred_2dp_if_required",
        "divide_by_one_thousand",
        "divide_by_six",
        "divide_by_ten_0dp",
        "divide_by_ten_1dp",
        "divide_by_ten_1dp_if_required",
        "divide_by_three",
        "divide_by_twelve",
        "divide_by_twenty",
        "divide_by_twenty_then_double_0dp",
        "divide_by_two_0dp",
        "double",
        "deciseconds_to_seconds",
        "locations_to_metres",
        "milliseconds_to_seconds",
        "milliseconds_to_seconds_0dp",
        "milliseconds_to_seconds_1dp",
        "milliseconds_to_seconds_2dp",
        "milliseconds_to_seconds_2dp_if_required",
        "multiplicative_damage_modifier",
        "old_leech_percent",
        "old_leech_permyriad",
        "per_minute_to_per_second",
        "per_minute_to_per_second_0dp",
        "per_minute_to_per_second_1dp",
        "per_minute_to_per_second_2dp",
        "per_minute_to_per_second_2dp_if_required",
        "permyriad_per_minute_to_%_per_second",
        "plus_two_hundred",
        "times_one_point_five",
        "times_twenty",
    };

    private static readonly HashSet<string> OrderReversingHandlers = new(StringComparer.OrdinalIgnoreCase)
    {
        "divide_by_one_hundred_and_negate",
        "negate",
        "negate_and_double",
    };

    public static ModifierBoundDefaultResult Create(
        ModifierDefinition? modifier,
        IReadOnlyList<ModifierStat> stats,
        IReadOnlyList<string> sourceLines,
        GameDataCatalog? catalog)
    {
        if (modifier is null)
        {
            return Unsupported("The source modifier did not resolve to one exact GameData modifier.");
        }

        if (catalog is null)
        {
            return Unsupported("GameData translations are unavailable for this modifier.");
        }

        if (stats.Count == 0 || sourceLines.Count != 1 || stats.Any(stat =>
                string.IsNullOrWhiteSpace(stat.StatId)))
        {
            return Unsupported(
                "The modifier does not retain one translated GameData stat group and one displayed value line.");
        }

        var observedValues = ExtractObservedValues(sourceLines[0]);
        var statIds = stats.Select(stat => stat.StatId!.Trim()).ToArray();
        var branches = statIds
            .SelectMany(catalog.FindStatTranslationsByStatId)
            .Concat(catalog.FindStatTranslationsByStatIdGroup(statIds))
            .DistinctBy(translation => translation.Id, StringComparer.Ordinal)
            .Where(translation => statIds.All(statId => translation.StatIds.Contains(
                statId,
                StringComparer.Ordinal)))
            .SelectMany(translation =>
            {
                var translationStats = AlignTranslationStats(translation, modifier.Stats);
                return translation.Variants.Select(variant => CreateBranch(
                    translation,
                    variant,
                    translationStats,
                    stats,
                    sourceLines[0]));
            })
            .Where(branch => branch is not null)
            .Select(branch => branch!)
            .DistinctBy(BranchKey, StringComparer.Ordinal)
            .ToArray();
        if (branches.Length != 1)
        {
            return Unsupported(
                branches.Length == 0
                    ? "No GameData translation branch matches the resolved component, roll ranges, and displayed line."
                    : "Multiple distinct GameData translation branches match the resolved component and displayed line.",
                observedValues);
        }

        var translation = branches[0].Translation;
        var variant = branches[0].Variant;
        var numericIndexes = branches[0].NumericIndexes;
        observedValues = branches[0].ObservedValues;

        var handlers = numericIndexes
            .Select(index => HandlersFor(variant, index))
            .ToArray();
        if (handlers.Any(group => group is null))
        {
            return Unsupported(
                "The selected translation branch does not retain one deterministic handler sequence per numeric value.",
                observedValues);
        }

        var retainedHandlers = handlers.Select(group => (IReadOnlyList<string>)group!).ToArray();
        if (numericIndexes.Count == 0 && observedValues.Count == 0)
        {
            return new ModifierBoundDefaultResult(
                false,
                default,
                ModifierBoundDirection.Minimum,
                "The GameData translation is presence-only and has no displayed numeric value.",
                ModifierBoundShape.PresenceOnly,
                observedValues,
                retainedHandlers);
        }

        if (numericIndexes.Count != observedValues.Count)
        {
            return Unsupported(
                "The observed numeric tuple arity does not match the selected GameData translation branch.",
                observedValues,
                retainedHandlers);
        }

        if (numericIndexes.Count == 1)
        {
            if (!TryGetDirection(retainedHandlers[0], out var direction, out var unsupportedHandler))
            {
                return Unsupported(
                    $"Translation handler '{unsupportedHandler}' has no proven provider-bound ordering semantics.",
                    observedValues,
                    retainedHandlers);
            }

            var observed = observedValues[0];
            var canonical = direction == ModifierBoundDirection.Maximum
                ? -decimal.Abs(observed)
                : observed;
            return new ModifierBoundDefaultResult(
                true,
                canonical,
                direction,
                null,
                ModifierBoundShape.Scalar,
                observedValues,
                retainedHandlers);
        }

        if (numericIndexes.Count == 2 &&
            IsDamageRange(modifier, translation, numericIndexes) &&
            retainedHandlers.All(group => group.Count == 0) &&
            observedValues[0] <= observedValues[1])
        {
            return new ModifierBoundDefaultResult(
                false,
                (observedValues[0] + observedValues[1]) / 2m,
                ModifierBoundDirection.Minimum,
                "The damage range requires confirmation against the resolved two-value Trade stat.",
                ModifierBoundShape.ArithmeticMeanRange,
                observedValues,
                retainedHandlers);
        }

        return Unsupported(
            "The translated numeric tuple has no proven single-value provider projection.",
            observedValues,
            retainedHandlers);
    }

    internal static IReadOnlyList<decimal> ExtractObservedValues(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return [];
        }

        var observedOnly = AttachedRangeRegex().Replace(source, "${roll}");
        return NumberRegex().Matches(observedOnly)
            .Select(match => decimal.TryParse(
                    match.Value,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var value)
                ? (decimal?)value
                : null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
    }

    internal static IReadOnlyList<decimal> ExtractObservedValues(
        string source,
        StatTranslationVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);
        var numericIndexes = variant.ValueFormats
            .Select((format, index) => new { format, index })
            .Where(candidate => candidate.format is "#" or "+#")
            .Select(candidate => candidate.index)
            .ToArray();
        return TryExtractObservedValues(source, variant, numericIndexes, out var values)
            ? values
            : [];
    }

    private static bool TryExtractObservedValues(
        string source,
        StatTranslationVariant variant,
        IReadOnlyList<int> numericIndexes,
        out IReadOnlyList<decimal> values)
    {
        values = [];
        if (variant.FormatLines.Count != 1 || variant.ValueFormats.Count == 0)
        {
            return false;
        }

        var pattern = Regex.Escape(variant.FormatLines[0]);
        for (var index = 0; index < variant.ValueFormats.Count; index++)
        {
            var placeholder = Regex.Escape($"{{{index}}}");
            var replacement = variant.ValueFormats[index] is "#" or "+#"
                ? $@"(?<value{index}>[+-]?\d+(?:\.\d+)?)"
                : string.Empty;
            pattern = pattern.Replace(placeholder, replacement, StringComparison.Ordinal);
        }

        pattern = pattern.Replace(@"\ ", @"\s+", StringComparison.Ordinal);
        var observedOnly = AttachedRangeRegex().Replace(source, "${roll}");
        var match = Regex.Match(
            observedOnly,
            $@"^\s*{pattern}\s*$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        var extracted = new List<decimal>(numericIndexes.Count);
        foreach (var index in numericIndexes)
        {
            var captures = match.Groups[$"value{index}"].Captures;
            if (captures.Count == 0 || captures.Cast<Capture>().Any(capture =>
                    !string.Equals(capture.Value, captures[0].Value, StringComparison.Ordinal)))
            {
                return false;
            }

            if (!decimal.TryParse(
                    captures[0].Value,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                return false;
            }

            extracted.Add(value);
        }

        values = extracted;
        return true;
    }

    private static BoundTranslationBranch? CreateBranch(
        StatTranslationDefinition translation,
        StatTranslationVariant variant,
        IReadOnlyList<ModifierStat?> translationStats,
        IReadOnlyList<ModifierStat> componentStats,
        string sourceLine)
    {
        if (translation.StatIds.Count != variant.ValueFormats.Count ||
            translation.StatIds.Count != variant.Conditions.Count ||
            translation.StatIds.Count != translationStats.Count)
        {
            return null;
        }

        for (var index = 0; index < translationStats.Count; index++)
        {
            var stat = translationStats[index];
            if (!VariantConditionMatches(
                    variant.Conditions[index],
                    stat?.MinValue ?? 0m,
                    stat?.MaxValue ?? 0m))
            {
                return null;
            }
        }

        var allNumericIndexes = variant.ValueFormats
            .Select((format, index) => new { format, index })
            .Where(candidate => candidate.format is "#" or "+#")
            .Select(candidate => candidate.index)
            .ToArray();
        var componentNumericIndexes = allNumericIndexes
            .Where(index => translationStats[index] is not null &&
                ComponentContainsStat(componentStats, translationStats[index]!))
            .ToArray();
        if (componentNumericIndexes.Length != allNumericIndexes.Length ||
            !TryExtractObservedValues(sourceLine, variant, componentNumericIndexes, out var observedValues))
        {
            return null;
        }

        return new BoundTranslationBranch(
            translation,
            variant,
            componentNumericIndexes,
            observedValues);
    }

    private static IReadOnlyList<ModifierStat?> AlignTranslationStats(
        StatTranslationDefinition translation,
        IReadOnlyList<ModifierStat> modifierStats)
    {
        var unused = modifierStats.OrderBy(stat => stat.Index).ToList();
        var aligned = new List<ModifierStat?>(translation.StatIds.Count);
        foreach (var statId in translation.StatIds)
        {
            var index = unused.FindIndex(stat => string.Equals(
                stat.StatId?.Trim(),
                statId?.Trim(),
                StringComparison.Ordinal));
            if (index < 0)
            {
                aligned.Add(null);
                continue;
            }

            aligned.Add(unused[index]);
            unused.RemoveAt(index);
        }

        return aligned;
    }

    private static bool ComponentContainsStat(
        IReadOnlyList<ModifierStat> componentStats,
        ModifierStat candidate)
    {
        return componentStats.Any(stat => ReferenceEquals(stat, candidate) ||
            stat.Index == candidate.Index && string.Equals(
                stat.StatId?.Trim(),
                candidate.StatId?.Trim(),
                StringComparison.Ordinal));
    }

    private static string BranchKey(BoundTranslationBranch branch)
    {
        var numeric = branch.NumericIndexes.Select(index => string.Join(
                "\u001f",
                branch.Translation.StatIds[index],
                branch.Variant.ValueFormats[index],
                string.Join("\u001d", HandlersFor(branch.Variant, index) ?? ["<missing>"])))
            .ToArray();
        return string.Join("\u001e", branch.Variant.FormatLines.Concat(numeric));
    }

    private static IReadOnlyList<string>? HandlersFor(StatTranslationVariant variant, int statIndex)
    {
        var matches = variant.IndexHandlers.Where(handler => handler.Index == statIndex).ToArray();
        return matches.Length == 1
            ? matches[0].Handlers.Select(handler => handler.Trim()).ToArray()
            : null;
    }

    private static bool IsDamageRange(
        ModifierDefinition modifier,
        StatTranslationDefinition translation,
        IReadOnlyList<int> numericIndexes)
    {
        if (!modifier.Tags.Any(IsDamageTag) || numericIndexes.Count != 2)
        {
            return false;
        }

        return TryGetRangeRole(translation.StatIds[numericIndexes[0]], out var firstStem, out var firstRole) &&
            TryGetRangeRole(translation.StatIds[numericIndexes[1]], out var secondStem, out var secondRole) &&
            firstRole == RangeRole.Minimum &&
            secondRole == RangeRole.Maximum &&
            string.Equals(firstStem, secondStem, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDamageTag(string? tag)
    {
        return (tag ?? string.Empty)
            .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("damage", StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryGetRangeRole(string? statId, out string stem, out RangeRole role)
    {
        stem = string.Empty;
        role = default;
        var tokens = (statId ?? string.Empty)
            .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        var roleIndexes = tokens
            .Select((token, index) => new { token, index })
            .Where(candidate => candidate.token is "minimum" or "maximum" or "min" or "max")
            .ToArray();
        if (roleIndexes.Length != 1)
        {
            return false;
        }

        role = roleIndexes[0].token is "minimum" or "min"
            ? RangeRole.Minimum
            : RangeRole.Maximum;
        tokens[roleIndexes[0].index] = "{range}";
        stem = string.Join('_', tokens);
        return true;
    }

    private static bool TryGetDirection(
        IReadOnlyList<string> handlers,
        out ModifierBoundDirection direction,
        out string? unsupportedHandler)
    {
        var reversesOrder = false;
        unsupportedHandler = null;
        foreach (var handler in handlers)
        {
            if (OrderReversingHandlers.Contains(handler))
            {
                reversesOrder = !reversesOrder;
                continue;
            }

            if (!OrderPreservingHandlers.Contains(handler))
            {
                direction = ModifierBoundDirection.Minimum;
                unsupportedHandler = handler;
                return false;
            }
        }

        direction = reversesOrder
            ? ModifierBoundDirection.Maximum
            : ModifierBoundDirection.Minimum;
        return true;
    }

    private static bool VariantConditionMatches(
        StatTranslationCondition candidate,
        decimal? minimum,
        decimal? maximum)
    {
        return !candidate.IsNegated && minimum.HasValue && maximum.HasValue &&
            (!candidate.MinValue.HasValue || minimum.Value >= candidate.MinValue.Value) &&
            (!candidate.MaxValue.HasValue || maximum.Value <= candidate.MaxValue.Value);
    }

    private static ModifierBoundDefaultResult Unsupported(
        string reason,
        IReadOnlyList<decimal>? observedValues = null,
        IReadOnlyList<IReadOnlyList<string>>? handlers = null)
    {
        return new ModifierBoundDefaultResult(
            false,
            default,
            ModifierBoundDirection.Minimum,
            reason,
            ModifierBoundShape.Unsupported,
            observedValues ?? [],
            handlers ?? []);
    }

    private enum RangeRole
    {
        Minimum,
        Maximum,
    }

    private sealed record BoundTranslationBranch(
        StatTranslationDefinition Translation,
        StatTranslationVariant Variant,
        IReadOnlyList<int> NumericIndexes,
        IReadOnlyList<decimal> ObservedValues);

    [GeneratedRegex(@"(?<![\w#])(?<roll>[\+\-]?\d+(?:\.\d+)?)\(\s*[\+\-]?\d+(?:\.\d+)?\s*[-–—]\s*[\+\-]?\d+(?:\.\d+)?\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex AttachedRangeRegex();

    [GeneratedRegex(@"(?<![\w#])[\+\-]?\d+(?:\.\d+)?(?![\w#])", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();
}

internal readonly record struct ModifierBoundDefaultResult(
    bool IsSupported,
    decimal ObservedCanonicalValue,
    ModifierBoundDirection Direction,
    string? UnsupportedReason,
    ModifierBoundShape Shape,
    IReadOnlyList<decimal> ObservedValues,
    IReadOnlyList<IReadOnlyList<string>> TranslationHandlers);
