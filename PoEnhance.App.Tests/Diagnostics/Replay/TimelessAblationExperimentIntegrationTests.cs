using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

public sealed class TimelessAblationExperimentIntegrationTests
{
    [Fact]
    public async Task Run_FiveTimelessAblation_PostFixExactContractAndHistoricalTriggerProvenance()
    {
        var corpus = ResolveCorpus();
        if (corpus is null)
        {
            return;
        }

        var output = Path.Combine(Path.GetTempPath(), "PoEnhance-A.5.6.1-TimelessAblation");
        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }

        var gameData = FindRepoFile("artifacts", "poenhance-game-data.json");
        var report = await TimelessAblationExperiment.RunAsync(corpus, gameData, output);

        Assert.True(File.Exists(Path.Combine(output, "ablation-summary.txt")));
        Assert.True(File.Exists(Path.Combine(output, "timeless-minimal-trigger.json")));
        Assert.True(File.Exists(Path.Combine(output, "timeless-ablation-matrix.csv")));
        Assert.True(File.Exists(Path.Combine(output, "timeless-reverse-addition-matrix.csv")));

        var summary = await File.ReadAllTextAsync(Path.Combine(output, "ablation-summary.txt"));
        Assert.Contains("real Timeless Exact after fix: 5/5", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Timeless real mismatch reproduced:", summary, StringComparison.Ordinal);

        var realRows = report.AblationRows
            .Where(row => row.TransformName == "identity_real")
            .ToArray();
        Assert.Equal(5, realRows.Length);
        Assert.All(realRows, row =>
        {
            Assert.Equal("EXACT", row.Outcome);
            Assert.True(row.HasUnscalableValue);
            Assert.NotEmpty(row.ModifierIds);
            Assert.NotEmpty(row.StatIds);
            Assert.True(row.IsSearchable, $"{row.ItemName} should be searchable with Timeless-capable trade item catalog");
            Assert.Null(row.SeedBlockDiagnostic);
            Assert.DoesNotContain(
                "VERSION_MISMATCH",
                row.UniqueIdentityDiagnostic ?? string.Empty,
                StringComparison.Ordinal);
        });

        var fixtureExact = report.AblationRows.Count(row =>
            row.TransformName == "identity_fixture" && row.Outcome == "EXACT");
        Assert.True(fixtureExact >= 4, $"fixtureExact={fixtureExact}");

        var normalizeHistoric = report.AblationRows
            .Where(row => row.TransformName == "normalize_historic_to_plain")
            .ToArray();
        Assert.Equal(5, normalizeHistoric.Length);
        Assert.All(normalizeHistoric, row => Assert.Equal("EXACT", row.Outcome));

        var restoreHistoric = report.ReverseAdditionRows
            .Where(row => row.TransformName == "add_real_historic_wording")
            .ToArray();
        Assert.Equal(5, restoreHistoric.Length);
        Assert.All(restoreHistoric, row => Assert.Equal("EXACT", row.Outcome));

        // Presentation-only / annotation transforms must not reintroduce version mismatch.
        var timelessExperimentRows = report.AblationRows
            .Concat(report.ReverseAdditionRows)
            .Where(row => row.Outcome != TimelessAblationClipboard.InvalidExperimentInput)
            .ToArray();
        Assert.DoesNotContain(
            timelessExperimentRows,
            row => row.Outcome == "MISMATCH" ||
                   string.Equals(
                       row.SeedBlockDiagnostic,
                       "UNIQUE_BLOCK_VERSION_MISMATCH",
                       StringComparison.Ordinal));

        Assert.Equal("post_fix_family_wide", report.MinimalTrigger.EvidenceStrength);
        Assert.Equal("FAMILY_WIDE", report.MinimalTrigger.FamilyClassification);
        Assert.Equal("bidirectional", report.MinimalTrigger.HistoricalEvidenceStrength);
        Assert.Contains(
            report.MinimalTrigger.Features,
            feature => feature.Contains("historic_unscalable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            "Unscalable Value",
            report.MinimalTrigger.HistoricalTrigger ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        Assert.All(
            report.IntentionalMismatchControls,
            row => Assert.Equal("MISMATCH", row.Outcome));

        // Working non-Timeless ReplayReady control: Thread of Hope Core semantics stay Exact.
        var threadCapture = Directory.GetFiles(corpus, "*Thread of Hope*.json")
            .Select(path =>
            {
                using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
                if (!document.RootElement.TryGetProperty("replayContext", out var replay) ||
                    !replay.TryGetProperty("rawClipboardText", out var raw))
                {
                    return null;
                }

                return raw.GetString();
            })
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
        Assert.False(string.IsNullOrWhiteSpace(threadCapture));
        var threadParsed = new ItemTextParser().Parse(threadCapture!);
        Assert.True(threadParsed.Modifiers.Count >= 2);
        Assert.Contains(
            threadParsed.Modifiers,
            modifier => modifier.ValueLines.Any(line =>
                line.Contains("Passage", StringComparison.Ordinal) ||
                line.Contains("Ring", StringComparison.Ordinal)));
    }

    private static string? ResolveCorpus()
    {
        var fromEnv = Environment.GetEnvironmentVariable("POENHANCE_A53_REPLAY_CORPUS");
        if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
        {
            return fromEnv;
        }

        var fallback = Path.Combine(Path.GetTempPath(), "PoEnhance-A.5.3-ReplayReady");
        return Directory.Exists(fallback) ? fallback : null;
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
