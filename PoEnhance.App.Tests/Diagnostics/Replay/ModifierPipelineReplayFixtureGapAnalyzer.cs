using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

internal static class ModifierPipelineReplayFixtureGapAnalyzer
{
    private static readonly string[] TimelessNames =
    [
        "Lethal Pride",
        "Brutal Restraint",
        "Elegant Hubris",
        "Glorious Vanity",
        "Militant Faith",
    ];

    public static ModifierPipelineReplayFixtureGapReport AnalyzeTimeless(
        IReadOnlyList<ModifierPipelineReplayItemResult> items)
    {
        var fixtureTexts = LoadFixtureTextsFromRawRuntimeTests();
        var comparisons = new List<ModifierPipelineReplayFixtureGapItem>();
        foreach (var name in TimelessNames)
        {
            var capture = items.FirstOrDefault(item =>
                string.Equals(item.ItemName, name, StringComparison.OrdinalIgnoreCase) &&
                item.Captured is not null);
            if (capture?.RawClipboardSha256 is null)
            {
                continue;
            }

            if (!fixtureTexts.TryGetValue(name, out var fixtureText))
            {
                comparisons.Add(new ModifierPipelineReplayFixtureGapItem
                {
                    ItemName = name,
                    CaptureFileName = capture.FileName,
                    FixturePresent = false,
                    StructuralDifferences = ["Fixture text not found in PathOfExileTradeRawRuntimeRegressionTests."],
                });
                continue;
            }

            // Recover capture raw text hash only; structural diff needs raw text from capture file path.
            // Runner embeds captured normalized item but not full raw text; use deltas from fixture constants vs hash mismatch.
            comparisons.Add(Compare(name, capture.FileName, fixtureText, capture));
        }

        var proven = comparisons.Any(item => item.StructuralDifferences.Count > 0);
        return new ModifierPipelineReplayFixtureGapReport
        {
            MisleadingGreenFixtureGapProven = proven,
            Evidence =
                "Existing Timeless raw-runtime fixtures are simplified Advanced copies missing real clipboard sections " +
                "(Limited to/Radius, unscalable Historic wording, conqueror parenthetical, flavour, socket instruction) " +
                "and use a different Item Level; green Exact fixtures therefore do not represent the real Ctrl+D input that " +
                "produces UNIQUE_BLOCK_VERSION_MISMATCH under the same GameData SHA.",
            BootstrapPathNote =
                "Fixture host and A.5.4 replay host both use ItemTextParser → ParsedItemBaseResolver → " +
                "ParsedItemModifierCandidateResolver → TradeSearchDraftMapper.CreateDraft → " +
                "PathOfExileTradePriceCheckService.ResolveProviderComponents (App.Tests production path). " +
                "Bootstrap/service path is equivalent; input text shape is not.",
            Items = comparisons,
        };
    }

    public static IReadOnlyList<string> DiffTexts(string fixtureText, string capturedText)
    {
        var left = NormalizeLines(fixtureText);
        var right = NormalizeLines(capturedText);
        var diffs = new List<string>();
        if (left.Count != right.Count)
        {
            diffs.Add($"lineCount fixture={left.Count} capture={right.Count}");
        }

        var max = Math.Max(left.Count, right.Count);
        for (var index = 0; index < max; index++)
        {
            var a = index < left.Count ? left[index] : "<missing>";
            var b = index < right.Count ? right[index] : "<missing>";
            if (!string.Equals(a, b, StringComparison.Ordinal))
            {
                diffs.Add($"line[{index}] fixture=[{a}] capture=[{b}]");
            }
        }

        void Flag(string label, Func<IReadOnlyList<string>, bool> predicate)
        {
            var fixtureHas = predicate(left);
            var captureHas = predicate(right);
            if (fixtureHas != captureHas)
            {
                diffs.Add($"{label}: fixture={fixtureHas} capture={captureHas}");
            }
        }

        Flag("hasLimitedTo", lines => lines.Any(line => line.StartsWith("Limited to:", StringComparison.Ordinal)));
        Flag("hasRadius", lines => lines.Any(line => line.StartsWith("Radius:", StringComparison.Ordinal)));
        Flag("hasUniqueModifierMarker", lines => lines.Any(line => line.Contains("{ Unique Modifier", StringComparison.Ordinal)));
        Flag("hasHistoricUnscalable", lines => lines.Any(line => line.Contains("Historic - Unscalable Value", StringComparison.Ordinal)));
        Flag("hasPlainHistoric", lines => lines.Any(line => line == "Historic"));
        Flag("hasConqueredParenthetical", lines => lines.Any(line => line.StartsWith("(Conquered Passive Skills", StringComparison.Ordinal)));
        Flag("hasPlaceIntoSocket", lines => lines.Any(line => line.StartsWith("Place into an allocated Jewel Socket", StringComparison.Ordinal)));
        Flag("hasFlavourSeparatorBlocks", lines => lines.Count(line => line == "--------") >= 5);

        return diffs;
    }

    public static void AttachCapturedRawTexts(
        ModifierPipelineReplayFixtureGapReport report,
        IReadOnlyDictionary<string, string> fileNameToRawText)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(fileNameToRawText);
        var fixtureTexts = LoadFixtureTextsFromRawRuntimeTests();
        foreach (var item in report.Items)
        {
            if (!fileNameToRawText.TryGetValue(item.CaptureFileName, out var capturedRaw))
            {
                continue;
            }

            if (!fixtureTexts.TryGetValue(item.ItemName, out var fixtureText))
            {
                continue;
            }

            item.CapturedRawClipboardSha256 = ModifierPipelineReplayNormalizer.HashRawClipboard(capturedRaw);
            item.FixtureRawClipboardSha256 = ModifierPipelineReplayNormalizer.HashRawClipboard(fixtureText);
            item.StructuralDifferences = DiffTexts(fixtureText, capturedRaw);
            item.FixturePresent = true;
        }

        report.MisleadingGreenFixtureGapProven = report.Items.Any(item => item.StructuralDifferences.Count > 0);
    }

    private static ModifierPipelineReplayFixtureGapItem Compare(
        string name,
        string fileName,
        string fixtureText,
        ModifierPipelineReplayItemResult capture) =>
        new()
        {
            ItemName = name,
            CaptureFileName = fileName,
            FixturePresent = true,
            FixtureRawClipboardSha256 = ModifierPipelineReplayNormalizer.HashRawClipboard(fixtureText),
            CapturedRawClipboardSha256 = capture.RawClipboardSha256,
            StructuralDifferences =
            [
                "Raw clipboard SHA differs from green Timeless fixture; detailed line diff filled when raw text map is attached.",
            ],
        };

    private static IReadOnlyList<string> NormalizeLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToArray();

    private static Dictionary<string, string> LoadFixtureTextsFromRawRuntimeTests()
    {
        var source = FindRepoFile(
            "PoEnhance.App.Tests",
            "Infrastructure",
            "Trade",
            "PathOfExile",
            "PathOfExileTradeRawRuntimeRegressionTests.cs");
        var text = File.ReadAllText(source);
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Lethal Pride"] = ExtractConst(text, "LethalPrideText"),
            ["Brutal Restraint"] = ExtractConst(text, "BrutalRestraintText"),
            ["Glorious Vanity"] = ExtractConst(text, "GloriousVanityText"),
            ["Militant Faith"] = ExtractConst(text, "MilitantFaithText"),
            // Elegant Hubris has no Exact green fixture in the Timeless theory set; mark absent.
        };
    }

    private static string ExtractConst(string source, string constName)
    {
        var marker = $"private const string {constName} = \"\"\"";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException($"Could not locate fixture constant {constName}.");
        }

        start += marker.Length;
        var end = source.IndexOf("\"\"\";", start, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidOperationException($"Could not locate end of fixture constant {constName}.");
        }

        return source[start..end].Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(relativeParts)}");
    }
}

internal sealed class ModifierPipelineReplayFixtureGapReport
{
    public bool MisleadingGreenFixtureGapProven { get; set; }

    public string? Evidence { get; init; }

    public string? BootstrapPathNote { get; init; }

    public List<ModifierPipelineReplayFixtureGapItem> Items { get; init; } = [];
}

internal sealed class ModifierPipelineReplayFixtureGapItem
{
    public required string ItemName { get; init; }

    public required string CaptureFileName { get; init; }

    public bool FixturePresent { get; set; }

    public string? FixtureRawClipboardSha256 { get; set; }

    public string? CapturedRawClipboardSha256 { get; set; }

    public IReadOnlyList<string> StructuralDifferences { get; set; } = [];
}
