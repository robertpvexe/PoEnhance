using System.Text.Json;
using System.Text.RegularExpressions;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

internal static class TimelessAblationCorpusBlast
{
    public static TimelessCorpusBlastReport Analyze(
        string bigManualDirectory,
        TimelessMinimalTrigger trigger)
    {
        if (!Directory.Exists(bigManualDirectory))
        {
            return new TimelessCorpusBlastReport
            {
                StructuralSubclasses =
                [
                    new TimelessStructuralSubclass
                    {
                        ClassId = "corpus-missing",
                        Description = $"Big Manual corpus not found at {bigManualDirectory}",
                        ItemCount = 0,
                        ModifierCount = 0,
                        ExampleItems = [],
                    },
                ],
            };
        }

        var versionMismatchRows = new List<(string Item, string Text, string Code, bool HasParentheticalEvidence)>();
        foreach (var path in Directory.GetFiles(bigManualDirectory, "*.json"))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var itemName = document.RootElement.TryGetProperty("item", out var item) &&
                           item.TryGetProperty("displayName", out var display)
                ? display.GetString() ?? Path.GetFileNameWithoutExtension(path)
                : Path.GetFileNameWithoutExtension(path);

            if (document.RootElement.TryGetProperty("uniqueMechanicalResolution", out var mech) &&
                mech.TryGetProperty("modifierBlocks", out var blocks) &&
                blocks.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in blocks.EnumerateArray())
                {
                    var code = block.TryGetProperty("diagnosticCode", out var codeElement)
                        ? codeElement.GetString()
                        : null;
                    if (!IsVersionMismatch(code))
                    {
                        continue;
                    }

                    versionMismatchRows.Add((itemName, "(block)", code!, HasParentheticalProxy(itemName, document)));
                }
            }

            if (!document.RootElement.TryGetProperty("modifiers", out var modifiers) ||
                modifiers.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var modifier in modifiers.EnumerateArray())
            {
                var code = TryGetNestedString(modifier, "sourceResolution", "uniqueResolutionDiagnosticCode") ??
                           TryGetNestedString(modifier, "sourceResolution", "diagnosticCode") ??
                           TryGetNestedString(modifier, "sourceResolution", "aggregateDiagnosticCode");
                if (!IsVersionMismatch(code))
                {
                    continue;
                }

                var text = TryGetNestedString(modifier, "raw", "originalText") ?? string.Empty;
                versionMismatchRows.Add((itemName, text, code!, HasParentheticalInText(text) || HasParentheticalProxy(itemName, document)));
            }
        }

        var sharing = versionMismatchRows
            .Where(row => row.HasParentheticalEvidence || LooksLikeTimelessJewelSeed(row.Item, row.Text) ||
                          HasHistoricUnscalable(row.Text))
            .ToArray();
        var notSharing = versionMismatchRows
            .Where(row => !row.HasParentheticalEvidence &&
                          !LooksLikeTimelessJewelSeed(row.Item, row.Text) &&
                          !HasHistoricUnscalable(row.Text))
            .ToArray();

        var timelessItems = sharing
            .Select(row => row.Item)
            .Where(IsTimelessName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var subclasses = new List<TimelessStructuralSubclass>
        {
            new()
            {
                ClassId = "aid-historic-unscalable-value-on-fixed-unique-block",
                Description = trigger.FutureClassInvariant ??
                              "Advanced-copy Historic — Unscalable Value annotation flips Fixed Unique block compatibility.",
                ItemCount = sharing.Select(row => row.Item).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                ModifierCount = sharing.Length,
                ExampleItems = sharing.Select(row => row.Item)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(10)
                    .ToArray(),
            },
            new()
            {
                ClassId = "intentional-or-other-version-mismatch",
                Description =
                    "UNIQUE_BLOCK_VERSION_MISMATCH rows without Timeless Advanced-copy parenthetical/seed chrome evidence (legitimate fail-closed / other shapes).",
                ItemCount = notSharing.Select(row => row.Item).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                ModifierCount = notSharing.Length,
                ExampleItems = notSharing.Select(row => row.Item)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(10)
                    .ToArray(),
            },
        };

        return new TimelessCorpusBlastReport
        {
            TimelessAffectedItemCount = timelessItems.Length,
            BroaderAffectedItemCount = sharing.Select(row => row.Item).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            BroaderAffectedModifierCount = sharing.Length,
            TopExampleItems = sharing.Select(row => row.Item)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToArray(),
            VersionMismatchRowCount = versionMismatchRows.Count,
            VersionMismatchRowsSharingTrigger = sharing.Length,
            VersionMismatchRowsNotSharingTrigger = notSharing.Length,
            StructuralSubclasses = subclasses,
        };
    }

    private static bool IsVersionMismatch(string? code) =>
        string.Equals(code, "UNIQUE_BLOCK_VERSION_MISMATCH", StringComparison.Ordinal) ||
        string.Equals(code, "UNIQUE_VERSION_NOT_FOUND", StringComparison.Ordinal);

    private static bool HasParentheticalInText(string text) =>
        text.Contains("(Conquered Passive Skills", StringComparison.Ordinal);

    private static bool HasParentheticalProxy(string itemName, JsonDocument document)
    {
        // Older AuditOnly captures lack raw clipboard; Timeless names with VERSION_MISMATCH
        // are treated as the same Advanced-copy chrome class based on A.5.4/A.5.5 evidence.
        if (IsTimelessName(itemName))
        {
            return true;
        }

        if (document.RootElement.TryGetProperty("replayContext", out var replay) &&
            replay.TryGetProperty("rawClipboardText", out var raw))
        {
            return HasParentheticalInText(raw.GetString() ?? string.Empty);
        }

        return false;
    }

    private static bool HasHistoricUnscalable(string text) =>
        text.Contains("Historic", StringComparison.Ordinal) &&
        text.Contains("Unscalable Value", StringComparison.Ordinal);

    private static bool LooksLikeTimelessJewelSeed(string itemName, string text) =>
        IsTimelessName(itemName) ||
        Regex.IsMatch(
            text,
            "Commanded leadership|Denoted service|Bathed in the blood|Carved to glorify|Commissioned .+ coins",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool IsTimelessName(string itemName) =>
        itemName is "Lethal Pride" or "Brutal Restraint" or "Elegant Hubris" or "Glorious Vanity" or "Militant Faith";

    private static string? TryGetNestedString(JsonElement root, string first, string second)
    {
        if (!root.TryGetProperty(first, out var child) ||
            !child.TryGetProperty(second, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }
}
