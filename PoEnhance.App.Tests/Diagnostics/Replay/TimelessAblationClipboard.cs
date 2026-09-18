using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

/// <summary>
/// Test-only clipboard sectioning/transforms for Timeless Advanced-copy ablation.
/// Does not change production parsing; only rewrites clipboard text before replay.
/// </summary>
internal static class TimelessAblationClipboard
{
    public const string InvalidExperimentInput = "INVALID_EXPERIMENT_INPUT";

    public static IReadOnlyList<string> SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

    public static string JoinLines(IEnumerable<string> lines) =>
        string.Join('\n', lines);

    public static string Hash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool LooksLikeValidJewelClipboard(string text)
    {
        var lines = SplitLines(text);
        if (lines.Count < 8)
        {
            return false;
        }

        return lines.Any(line => line.StartsWith("Item Class:", StringComparison.Ordinal)) &&
               lines.Any(line => line.StartsWith("Rarity:", StringComparison.Ordinal)) &&
               lines.Any(line => line.Contains("{ Unique Modifier", StringComparison.Ordinal));
    }

    public static TimelessAblationTransformResult Apply(
        string rawClipboardText,
        string transformName,
        Func<IReadOnlyList<string>, IReadOnlyList<string>?> mutate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawClipboardText);
        ArgumentException.ThrowIfNullOrWhiteSpace(transformName);
        ArgumentNullException.ThrowIfNull(mutate);

        var lines = SplitLines(rawClipboardText).ToList();
        IReadOnlyList<string>? mutated;
        try
        {
            mutated = mutate(lines);
        }
        catch (Exception exception)
        {
            return TimelessAblationTransformResult.Invalid(transformName, exception.Message);
        }

        if (mutated is null)
        {
            return TimelessAblationTransformResult.Invalid(transformName, "Transform returned null.");
        }

        var text = JoinLines(mutated).TrimEnd('\n') + "\n";
        if (!LooksLikeValidJewelClipboard(text))
        {
            return TimelessAblationTransformResult.Invalid(transformName, "Transformed clipboard failed structural validity checks.");
        }

        return new TimelessAblationTransformResult(transformName, text, true, null);
    }

    public static TimelessAblationTransformResult Identity(string raw, string name = "identity_real") =>
        new(name, raw.EndsWith('\n') ? raw : raw + "\n", true, null);

    public static TimelessAblationTransformResult RemoveFlavour(string raw) =>
        Apply(raw, "remove_flavour_section", lines =>
        {
            var uniqueEnd = FindUniqueBlockEnd(lines);
            if (uniqueEnd < 0)
            {
                return null;
            }

            // Keep header..unique block; drop trailing flavour/socket sections after unique block separator.
            var kept = lines.Take(uniqueEnd + 1).ToList();
            // Ensure we don't leave a dangling separator-only tail requirement; identity ends after Historic.
            return TrimTrailingSeparators(kept);
        });

    public static TimelessAblationTransformResult RemoveLimitedTo(string raw) =>
        Apply(raw, "remove_limited_to", lines =>
            lines.Where(line => !line.StartsWith("Limited to:", StringComparison.Ordinal)).ToArray());

    public static TimelessAblationTransformResult RemoveRadius(string raw) =>
        Apply(raw, "remove_radius", lines =>
            lines.Where(line => !line.StartsWith("Radius:", StringComparison.Ordinal)).ToArray());

    public static TimelessAblationTransformResult NormalizeItemLevel(string raw, int itemLevel = 86) =>
        Apply(raw, "normalize_item_level_to_fixture", lines =>
            lines.Select(line =>
                line.StartsWith("Item Level:", StringComparison.Ordinal)
                    ? $"Item Level: {itemLevel}"
                    : line).ToArray());

    public static TimelessAblationTransformResult NormalizeHistoricWording(string raw) =>
        Apply(raw, "normalize_historic_to_plain", lines =>
            lines.Select(line =>
                Regex.IsMatch(line, @"^Historic\s+[\u2014\-]\s+Unscalable Value$")
                    ? "Historic"
                    : line).ToArray());

    public static TimelessAblationTransformResult RemoveConqueredParenthetical(string raw) =>
        Apply(raw, "remove_conquered_parenthetical", lines =>
            lines.Where(line =>
                !line.StartsWith("(Conquered Passive Skills", StringComparison.Ordinal)).ToArray());

    public static TimelessAblationTransformResult RemoveSocketsSection(string raw) =>
        Apply(raw, "remove_sockets_section", lines =>
        {
            // Timeless jewels typically have no sockets section; treat as no-op if absent.
            if (!lines.Any(line => line.StartsWith("Sockets:", StringComparison.Ordinal)))
            {
                return lines.ToArray();
            }

            return RemoveNamedPropertySection(lines, "Sockets:");
        });

    public static TimelessAblationTransformResult RemoveRequirementsSection(string raw) =>
        Apply(raw, "remove_requirements_section", lines =>
        {
            if (!lines.Any(line => line.Equals("Requirements:", StringComparison.Ordinal)))
            {
                return lines.ToArray();
            }

            return RemoveNamedPropertySection(lines, "Requirements:");
        });

    public static TimelessAblationTransformResult ReplaceSeedBlockWithFixtureEquivalent(
        string realRaw,
        string fixtureRaw) =>
        Apply(realRaw, "replace_seed_block_with_fixture_equivalent", lines =>
        {
            var fixtureSeed = ExtractUniqueSeedBlock(SplitLines(fixtureRaw));
            var realSeedRange = FindUniqueSeedBlockRange(lines);
            if (fixtureSeed.Count == 0 || realSeedRange is null)
            {
                return null;
            }

            var result = lines.ToList();
            result.RemoveRange(realSeedRange.Value.Start, realSeedRange.Value.Count);
            result.InsertRange(realSeedRange.Value.Start, fixtureSeed);
            return result;
        });

    public static TimelessAblationTransformResult KeepRealSeedReplaceSurroundingsWithFixture(
        string realRaw,
        string fixtureRaw) =>
        Apply(realRaw, "keep_real_seed_replace_surroundings_with_fixture", _ =>
        {
            var fixtureLines = SplitLines(fixtureRaw).ToList();
            var realSeed = ExtractUniqueSeedBlock(SplitLines(realRaw));
            var fixtureSeedRange = FindUniqueSeedBlockRange(fixtureLines);
            if (realSeed.Count == 0 || fixtureSeedRange is null)
            {
                return null;
            }

            fixtureLines.RemoveRange(fixtureSeedRange.Value.Start, fixtureSeedRange.Value.Count);
            fixtureLines.InsertRange(fixtureSeedRange.Value.Start, realSeed);
            return fixtureLines;
        });

    public static TimelessAblationTransformResult BuildGreenPlusFeature(
        string fixtureRaw,
        string realRaw,
        string featureName)
    {
        return featureName switch
        {
            "add_real_item_level" => Apply(fixtureRaw, "add_real_item_level", lines =>
            {
                var realIl = SplitLines(realRaw).FirstOrDefault(line =>
                    line.StartsWith("Item Level:", StringComparison.Ordinal));
                if (realIl is null)
                {
                    return null;
                }

                return lines.Select(line =>
                    line.StartsWith("Item Level:", StringComparison.Ordinal) ? realIl : line).ToArray();
            }),
            "add_real_limited_to" => Apply(fixtureRaw, "add_real_limited_to", lines =>
                InsertAfterHeaderSeparators(lines, SplitLines(realRaw)
                    .FirstOrDefault(line => line.StartsWith("Limited to:", StringComparison.Ordinal)))),
            "add_real_radius" => Apply(fixtureRaw, "add_real_radius", lines =>
                InsertAfterHeaderSeparators(lines, SplitLines(realRaw)
                    .FirstOrDefault(line => line.StartsWith("Radius:", StringComparison.Ordinal)))),
            "add_real_historic_wording" => Apply(fixtureRaw, "add_real_historic_wording", lines =>
            {
                var realHistoric = SplitLines(realRaw).FirstOrDefault(line =>
                    line.StartsWith("Historic", StringComparison.Ordinal));
                if (realHistoric is null)
                {
                    return null;
                }

                return lines.Select(line => line == "Historic" ? realHistoric : line).ToArray();
            }),
            "add_real_conquered_parenthetical" => Apply(fixtureRaw, "add_real_conquered_parenthetical", lines =>
            {
                var parenthetical = SplitLines(realRaw).FirstOrDefault(line =>
                    line.StartsWith("(Conquered Passive Skills", StringComparison.Ordinal));
                if (parenthetical is null)
                {
                    return null;
                }

                var result = lines.ToList();
                var historicIndex = result.FindIndex(line => line.StartsWith("Historic", StringComparison.Ordinal));
                if (historicIndex < 0)
                {
                    return null;
                }

                result.Insert(historicIndex, parenthetical);
                return result;
            }),
            "add_real_flavour_and_socket" => Apply(fixtureRaw, "add_real_flavour_and_socket", lines =>
            {
                var realLines = SplitLines(realRaw);
                var uniqueEnd = FindUniqueBlockEnd(realLines);
                if (uniqueEnd < 0 || uniqueEnd >= realLines.Count - 1)
                {
                    return lines.ToArray();
                }

                var tail = realLines.Skip(uniqueEnd + 1).ToArray();
                return lines.Concat(tail).ToArray();
            }),
            _ => TimelessAblationTransformResult.Invalid(featureName, "Unknown reverse-addition feature."),
        };
    }

    public static IReadOnlyList<string> ExtractUniqueSeedBlock(IReadOnlyList<string> lines)
    {
        var range = FindUniqueSeedBlockRange(lines);
        if (range is null)
        {
            return [];
        }

        return lines.Skip(range.Value.Start).Take(range.Value.Count).ToArray();
    }

    public static (int Start, int Count)? FindUniqueSeedBlockRange(IReadOnlyList<string> lines)
    {
        var start = -1;
        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].Contains("{ Unique Modifier", StringComparison.Ordinal))
            {
                start = index;
                break;
            }
        }

        if (start < 0)
        {
            return null;
        }

        var end = start + 1;
        while (end < lines.Count)
        {
            var line = lines[end];
            if (line == "--------" ||
                (line.StartsWith("{ ", StringComparison.Ordinal) &&
                 !line.Contains("{ Unique Modifier", StringComparison.Ordinal)))
            {
                break;
            }

            // For Militant Faith, seed block ends before subsequent Unique Modifier markers.
            if (end > start &&
                line.Contains("{ Unique Modifier", StringComparison.Ordinal))
            {
                break;
            }

            end++;
        }

        return (start, end - start);
    }

    public static int FindUniqueBlockEnd(IReadOnlyList<string> lines)
    {
        var range = FindUniqueSeedBlockRange(lines);
        if (range is null)
        {
            return -1;
        }

        // Include contiguous following Unique Modifier sections (Militant Faith devotion lines)
        // until a separator that starts flavour.
        var index = range.Value.Start + range.Value.Count;
        while (index < lines.Count)
        {
            if (lines[index] == "--------")
            {
                return index - 1;
            }

            index++;
        }

        return lines.Count - 1;
    }

    private static IReadOnlyList<string> TrimTrailingSeparators(IReadOnlyList<string> lines)
    {
        var result = lines.ToList();
        while (result.Count > 0 &&
               (result[^1] == "--------" || string.IsNullOrWhiteSpace(result[^1])))
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    private static IReadOnlyList<string>? InsertAfterHeaderSeparators(
        IReadOnlyList<string> fixtureLines,
        string? insertion)
    {
        if (string.IsNullOrWhiteSpace(insertion))
        {
            return null;
        }

        var result = fixtureLines.ToList();
        // Insert after first -------- following base type.
        var firstSep = result.FindIndex(line => line == "--------");
        if (firstSep < 0)
        {
            return null;
        }

        result.Insert(firstSep + 1, insertion);
        return result;
    }

    private static IReadOnlyList<string> RemoveNamedPropertySection(
        IReadOnlyList<string> lines,
        string headerPrefix)
    {
        var result = new List<string>();
        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].StartsWith(headerPrefix, StringComparison.Ordinal) ||
                lines[index].Equals(headerPrefix.TrimEnd(':') + ":", StringComparison.Ordinal))
            {
                // Skip until separator or blank boundary.
                index++;
                while (index < lines.Count &&
                       lines[index] != "--------" &&
                       !string.IsNullOrWhiteSpace(lines[index]) &&
                       !lines[index].StartsWith("{ ", StringComparison.Ordinal))
                {
                    index++;
                }

                index--;
                continue;
            }

            result.Add(lines[index]);
        }

        return result;
    }
}

internal sealed record TimelessAblationTransformResult(
    string TransformName,
    string? Text,
    bool IsValid,
    string? InvalidReason)
{
    public static TimelessAblationTransformResult Invalid(string name, string reason) =>
        new(name, null, false, reason);
}
