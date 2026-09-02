using System.Text.RegularExpressions;
using PoEnhance.GameData;

namespace PoEnhance.Core.Trade;

/// <summary>
/// Classifies numeric query semantics from resolved stat IDs and translation branches.
/// </summary>
internal static partial class NumericQueryRoleClassifier
{
    public static NumericQueryRole Classify(
        IReadOnlyList<string> statIds,
        IReadOnlyList<int> numericIndexes,
        StatTranslationVariant? variant,
        ModifierBoundShape shape,
        bool isSupported)
    {
        if (shape == ModifierBoundShape.PresenceOnly)
        {
            return NumericQueryRole.PresenceOnly;
        }

        if (!isSupported || shape == ModifierBoundShape.Unsupported)
        {
            return NumericQueryRole.Unknown;
        }

        if (numericIndexes.Count == 0)
        {
            return NumericQueryRole.Unknown;
        }

        var formatLine = NormalizeFormatLineForSemantics(
            variant?.FormatLines.Count == 1 ? variant.FormatLines[0] : null);
        if (IsCharacterPerLevelScaling(formatLine))
        {
            return NumericQueryRole.OrdinaryScalar;
        }

        if (numericIndexes.Count >= 2)
        {
            if (IsMultiIndexTriggerChanceLine(formatLine))
            {
                return NumericQueryRole.Unknown;
            }

            if (IsCoupledRatioTranslation(variant, numericIndexes))
            {
                return NumericQueryRole.CoupledRatio;
            }

            return NumericQueryRole.Unknown;
        }

        if (IsCoupledRatioSingleIndex(formatLine, variant))
        {
            return NumericQueryRole.CoupledRatio;
        }

        if (IsSkillGemLevelThreshold(statIds, formatLine))
        {
            return NumericQueryRole.SkillGemLevelThreshold;
        }

        return NumericQueryRole.OrdinaryScalar;
    }

    internal static bool IsSkillGemLevelThreshold(
        IReadOnlyList<string> statIds,
        string? formatLine = null)
    {
        if (statIds.Any(IsDeniedSkillGemLevelStatId))
        {
            return false;
        }

        if (statIds.Any(IsSkillGemLevelStatId))
        {
            return true;
        }

        return GemSkillLevelQuerySemantics.IsGemOrSkillLevelQuery(formatLine);
    }

    private static bool IsSkillGemLevelStatId(string? statId)
    {
        if (string.IsNullOrWhiteSpace(statId))
        {
            return false;
        }

        var normalized = statId.Trim().ToLowerInvariant();
        if (IsDeniedSkillGemLevelStatId(normalized))
        {
            return false;
        }

        return normalized.Contains("supported_by_level", StringComparison.Ordinal) ||
            normalized.Contains("grants_level", StringComparison.Ordinal) ||
            normalized.Contains("trigger_level", StringComparison.Ordinal) ||
            normalized.Contains("gem_level", StringComparison.Ordinal) ||
            normalized.EndsWith("skill_level_+", StringComparison.Ordinal) ||
            string.Equals(normalized, "local_gem_level_+", StringComparison.Ordinal) ||
            string.Equals(normalized, "skill_level", StringComparison.Ordinal);
    }

    private static bool IsDeniedSkillGemLevelStatId(string? statId)
    {
        if (string.IsNullOrWhiteSpace(statId))
        {
            return false;
        }

        var normalized = statId.Trim().ToLowerInvariant();
        return normalized.Contains("per_level", StringComparison.Ordinal) ||
            normalized.Contains("per_socketed_gem", StringComparison.Ordinal);
    }

    private static bool IsCharacterPerLevelScaling(string? formatLine)
    {
        var normalized = NormalizeFormatLineForSemantics(formatLine);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return CharacterPerLevelRegex().IsMatch(normalized) &&
            !GemSkillLevelQuerySemantics.IsGemOrSkillLevelQuery(normalized);
    }

    private static bool IsMultiIndexTriggerChanceLine(string? formatLine)
    {
        var normalized = NormalizeFormatLineForSemantics(formatLine);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return MultiIndexTriggerChanceRegex().IsMatch(normalized);
    }

    private static bool IsCoupledRatioTranslation(
        StatTranslationVariant? variant,
        IReadOnlyList<int> numericIndexes)
    {
        if (variant is null || numericIndexes.Count < 2)
        {
            return false;
        }

        var formatLine = NormalizeFormatLineForSemantics(
            variant.FormatLines.Count == 1 ? variant.FormatLines[0] : null);
        return CoupledRatioRegex().IsMatch(formatLine ?? string.Empty);
    }

    private static bool IsCoupledRatioSingleIndex(string? formatLine, StatTranslationVariant? variant)
    {
        var normalizedFormatLine = NormalizeFormatLineForSemantics(formatLine);
        if (string.IsNullOrWhiteSpace(normalizedFormatLine) || variant is null)
        {
            return false;
        }

        if (!CoupledRatioRegex().IsMatch(normalizedFormatLine))
        {
            return false;
        }

        var numericPlaceholderCount = variant.ValueFormats.Count(format => format is "#" or "+#");
        return numericPlaceholderCount == 1;
    }

    private static string? NormalizeFormatLineForSemantics(string? formatLine)
    {
        if (string.IsNullOrWhiteSpace(formatLine))
        {
            return null;
        }

        return PlaceholderIndexRegex().Replace(formatLine.Trim(), "<number>");
    }

    [GeneratedRegex(@"\{(\d+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderIndexRegex();

    [GeneratedRegex(
        @"\bper\s+Level\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CharacterPerLevelRegex();

    [GeneratedRegex(
        @"%\s+chance\s+to\s+Triggers?\s+Level\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex MultiIndexTriggerChanceRegex();

    [GeneratedRegex(
        @"\bper\s+\d+(?:\.\d+)?\s*%",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CoupledRatioRegex();
}
