using System.Globalization;
using System.Text.RegularExpressions;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

/// <summary>
/// Aligns multi-line Advanced Item Description value evidence to multi-stat source modifiers
/// when each authoritative component has its own translation (no combined group translation).
/// Matching is signature- and value-driven — not line-order-driven.
/// </summary>
internal static partial class SpecialImplicitMultiLineValueAligner
{
    private static readonly ModifierTextSignatureMatcher TextMatcher = new();

    public static bool TryMatch(
        ParsedModifier modifier,
        ModifierDefinition candidate,
        GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(catalog);

        return Classify(modifier, candidate, catalog) ==
            SpecialImplicitMultiLineAlignmentOutcome.UniqueComplete;
    }

    public static SpecialImplicitMultiLineAlignmentOutcome Classify(
        ParsedModifier modifier,
        ModifierDefinition candidate,
        GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(catalog);

        if (!TryBuildCompatibility(modifier, candidate, catalog, out var stats, out _, out var compatible))
        {
            return SpecialImplicitMultiLineAlignmentOutcome.Incomplete;
        }

        var count = CountPerfectMatchings(compatible, stats.Length);
        return count switch
        {
            0 => SpecialImplicitMultiLineAlignmentOutcome.Incomplete,
            1 => SpecialImplicitMultiLineAlignmentOutcome.UniqueComplete,
            _ => SpecialImplicitMultiLineAlignmentOutcome.Ambiguous,
        };
    }

    public static bool TryAlignStatSubsets(
        ParsedModifier modifier,
        ModifierDefinition candidate,
        GameDataCatalog catalog,
        out IReadOnlyList<IReadOnlyList<ModifierStat>> lineStatSubsets)
    {
        lineStatSubsets = [];
        if (!TryBuildCompatibility(modifier, candidate, catalog, out var stats, out var lines, out var compatible) ||
            !TryFindUniqueAssignment(compatible, stats.Length, out var assignment))
        {
            return false;
        }

        var subsets = new IReadOnlyList<ModifierStat>[lines.Length];
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            subsets[lineIndex] = [stats[assignment[lineIndex]]];
        }

        lineStatSubsets = subsets;
        return true;
    }

    private static bool TryBuildCompatibility(
        ParsedModifier modifier,
        ModifierDefinition candidate,
        GameDataCatalog catalog,
        out ModifierStat[] stats,
        out string[] lines,
        out bool[,] compatible)
    {
        lines = modifier.ValueLines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToArray();
        stats = candidate.Stats
            .Where(stat => !string.IsNullOrWhiteSpace(stat.StatId))
            .OrderBy(stat => stat.Index)
            .ToArray();
        compatible = new bool[0, 0];
        if (lines.Length < 2 || stats.Length != lines.Length)
        {
            return false;
        }

        compatible = new bool[stats.Length, lines.Length];
        for (var statIndex = 0; statIndex < stats.Length; statIndex++)
        {
            var componentCandidate = candidate with { Stats = [stats[statIndex]] };
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                compatible[statIndex, lineIndex] =
                    ComponentTextMatches(componentCandidate, catalog, lines[lineIndex]) &&
                    ComponentValuesMatch(componentCandidate, catalog, lines[lineIndex]);
            }
        }

        return true;
    }

    private static bool ComponentTextMatches(
        ModifierDefinition componentCandidate,
        GameDataCatalog catalog,
        string line) =>
        TextMatcher.Match(componentCandidate, catalog, [line]).Outcome ==
            ModifierTextSignatureMatchOutcome.Match;

    private static bool ComponentValuesMatch(
        ModifierDefinition componentCandidate,
        GameDataCatalog catalog,
        string line)
    {
        IReadOnlyList<string> valueLines = [line];
        var advancedRanges = ExtractAdvancedStatRanges(valueLines);
        var stats = componentCandidate.Stats
            .Where(stat => !string.IsNullOrWhiteSpace(stat.StatId))
            .OrderBy(stat => stat.Index)
            .ToArray();
        if (advancedRanges.Count > 0)
        {
            var observedValues = ExtractAdvancedObservedValues(valueLines);
            if (stats.Length != advancedRanges.Count || observedValues.Count != advancedRanges.Count)
            {
                return false;
            }

            var variants = catalog.FindStatTranslationsByStatIdGroup(
                    stats.Select(stat => stat.StatId!.Trim()).ToArray())
                .SelectMany(translation => translation.Variants)
                .ToArray();
            var projectable = variants.Where(variant => VariantProjects(stats, variant)).ToArray();
            if (projectable.Length > 0)
            {
                return projectable.Any(variant =>
                    ProjectionMatches(stats, variant, advancedRanges, observedValues));
            }

            return RawRangesMatch(stats, advancedRanges);
        }

        var displayed = ExtractDisplayedStatValues(valueLines);
        if (displayed.Count == 0)
        {
            return true;
        }

        if (stats.Length != displayed.Count)
        {
            return false;
        }

        var displayVariants = catalog.FindStatTranslationsByStatIdGroup(
                stats.Select(stat => stat.StatId!.Trim()).ToArray())
            .SelectMany(translation => translation.Variants)
            .ToArray();
        if (displayVariants.Any(variant => VariantProjects(stats, variant)))
        {
            return displayVariants.Any(variant =>
                DisplayedProjectionMatches(stats, variant, displayed));
        }

        for (var index = 0; index < stats.Length; index++)
        {
            var minimum = stats[index].MinValue;
            var maximum = stats[index].MaxValue;
            if (!minimum.HasValue ||
                !maximum.HasValue ||
                displayed[index] < minimum.Value ||
                displayed[index] > maximum.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool VariantProjects(IReadOnlyList<ModifierStat> stats, StatTranslationVariant variant)
    {
        if (variant.ValueFormats.Count != stats.Count || variant.Conditions.Count != stats.Count)
        {
            return false;
        }

        for (var index = 0; index < stats.Count; index++)
        {
            var stat = stats[index];
            if (!stat.MinValue.HasValue ||
                !stat.MaxValue.HasValue ||
                variant.ValueFormats[index] is not ("#" or "+#"))
            {
                return false;
            }

            var handlers = variant.IndexHandlers.Where(handler => handler.Index == index).ToArray();
            if (handlers.Length != 1 ||
                !TryProjectDiscrete(stat.MinValue.Value, stat.MaxValue.Value, handlers[0].Handlers, out _))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ProjectionMatches(
        IReadOnlyList<ModifierStat> stats,
        StatTranslationVariant variant,
        IReadOnlyList<(decimal Minimum, decimal Maximum)> ranges,
        IReadOnlyList<decimal> observedValues)
    {
        for (var index = 0; index < stats.Count; index++)
        {
            var stat = stats[index];
            if (!stat.MinValue.HasValue || !stat.MaxValue.HasValue)
            {
                return false;
            }

            var handlers = variant.IndexHandlers.Where(handler => handler.Index == index).ToArray();
            if (handlers.Length != 1 ||
                !TryProjectDiscrete(stat.MinValue.Value, stat.MaxValue.Value, handlers[0].Handlers, out var projected))
            {
                return false;
            }

            var range = ranges[index];
            if (projected.Min() != range.Minimum ||
                projected.Max() != range.Maximum ||
                !projected.Contains(observedValues[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool DisplayedProjectionMatches(
        IReadOnlyList<ModifierStat> stats,
        StatTranslationVariant variant,
        IReadOnlyList<decimal> observedValues)
    {
        for (var index = 0; index < stats.Count; index++)
        {
            var stat = stats[index];
            if (!stat.MinValue.HasValue || !stat.MaxValue.HasValue)
            {
                return false;
            }

            var handlers = variant.IndexHandlers.Where(handler => handler.Index == index).ToArray();
            if (handlers.Length != 1 ||
                !TryProjectDiscrete(stat.MinValue.Value, stat.MaxValue.Value, handlers[0].Handlers, out var projected) ||
                !projected.Contains(observedValues[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RawRangesMatch(
        IReadOnlyList<ModifierStat> stats,
        IReadOnlyList<(decimal Minimum, decimal Maximum)> ranges)
    {
        for (var index = 0; index < stats.Count; index++)
        {
            var minimum = stats[index].MinValue;
            var maximum = stats[index].MaxValue;
            if (!minimum.HasValue || !maximum.HasValue)
            {
                return false;
            }

            var range = ranges[index];
            var exact = minimum.Value == range.Minimum && maximum.Value == range.Maximum;
            var inverted = minimum.Value == -range.Minimum && maximum.Value == -range.Maximum;
            if (!exact && !inverted)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryProjectDiscrete(
        decimal minimum,
        decimal maximum,
        IReadOnlyList<string> handlers,
        out IReadOnlyList<decimal> projectedValues)
    {
        projectedValues = [];
        if (minimum != decimal.Truncate(minimum) ||
            maximum != decimal.Truncate(maximum) ||
            maximum < minimum ||
            maximum - minimum > 10_000m)
        {
            return false;
        }

        var values = new List<decimal>();
        for (var value = minimum; value <= maximum; value++)
        {
            if (!StatTranslationNumericProjector.TryProjectValue(handlers, value, out var projected))
            {
                return false;
            }

            values.Add(projected);
        }

        projectedValues = values;
        return values.Count > 0;
    }

    private static int CountPerfectMatchings(bool[,] compatible, int size)
    {
        var usedStats = new bool[size];
        var count = 0;
        Search(0);
        return count;

        void Search(int lineIndex)
        {
            if (count > 1)
            {
                return;
            }

            if (lineIndex == size)
            {
                count++;
                return;
            }

            for (var statIndex = 0; statIndex < size; statIndex++)
            {
                if (usedStats[statIndex] || !compatible[statIndex, lineIndex])
                {
                    continue;
                }

                usedStats[statIndex] = true;
                Search(lineIndex + 1);
                usedStats[statIndex] = false;
                if (count > 1)
                {
                    return;
                }
            }
        }
    }

    private static bool TryFindUniqueAssignment(bool[,] compatible, int size, out int[] assignment)
    {
        var working = new int[size];
        Array.Fill(working, -1);
        var usedStats = new bool[size];
        var found = 0;
        int[]? unique = null;
        Search(0);
        if (found != 1 || unique is null)
        {
            assignment = [];
            return false;
        }

        assignment = unique;
        return true;

        void Search(int lineIndex)
        {
            if (found > 1)
            {
                return;
            }

            if (lineIndex == size)
            {
                found++;
                unique = (int[])working.Clone();
                return;
            }

            for (var statIndex = 0; statIndex < size; statIndex++)
            {
                if (usedStats[statIndex] || !compatible[statIndex, lineIndex])
                {
                    continue;
                }

                usedStats[statIndex] = true;
                working[lineIndex] = statIndex;
                Search(lineIndex + 1);
                usedStats[statIndex] = false;
                working[lineIndex] = -1;
                if (found > 1)
                {
                    return;
                }
            }
        }
    }

    private static IReadOnlyList<(decimal Minimum, decimal Maximum)> ExtractAdvancedStatRanges(
        IReadOnlyList<string> valueLines)
    {
        var ranges = new List<(decimal, decimal)>();
        foreach (var line in valueLines)
        {
            foreach (Match match in AdvancedRangePattern().Matches(line))
            {
                if (!decimal.TryParse(
                        match.Groups["minimum"].Value,
                        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out var minimum) ||
                    !decimal.TryParse(
                        match.Groups["maximum"].Value,
                        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out var maximum))
                {
                    return [];
                }

                ranges.Add((minimum, maximum));
            }
        }

        return ranges;
    }

    private static IReadOnlyList<decimal> ExtractAdvancedObservedValues(IReadOnlyList<string> valueLines)
    {
        var values = new List<decimal>();
        foreach (var line in valueLines)
        {
            foreach (Match match in AdvancedRangePattern().Matches(line))
            {
                if (!decimal.TryParse(
                        match.Groups["value"].Value,
                        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out var value))
                {
                    return [];
                }

                values.Add(value);
            }
        }

        return values;
    }

    private static IReadOnlyList<decimal> ExtractDisplayedStatValues(IReadOnlyList<string> valueLines)
    {
        var values = new List<decimal>();
        foreach (var line in valueLines)
        {
            foreach (Match match in DisplayedStatValuePattern().Matches(line))
            {
                if (!decimal.TryParse(
                        match.Groups["value"].Value,
                        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out var value))
                {
                    return [];
                }

                values.Add(value);
            }
        }

        return values;
    }

    [GeneratedRegex(
        @"(?<value>[+-]?\d+(?:\.\d+)?)\((?<minimum>[+-]?\d+(?:\.\d+)?)-(?<maximum>[+-]?\d+(?:\.\d+)?)\)",
        RegexOptions.CultureInvariant)]
    private static partial Regex AdvancedRangePattern();

    [GeneratedRegex(@"(?<value>[+-]?\d+(?:\.\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex DisplayedStatValuePattern();
}
