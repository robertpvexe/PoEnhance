using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

public sealed class TimelessAblationExperimentIntegrationTests
{
    [Fact]
    public async Task Run_FiveTimelessAblation_ProducesReportsAndBidirectionalParentheticalTrigger()
    {
        var corpus = ResolveCorpus();
        if (corpus is null)
        {
            return;
        }

        var output = Path.Combine(Path.GetTempPath(), "PoEnhance-A.5.5-TimelessAblation");
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

        var realMismatch = report.AblationRows.Count(row =>
            row.TransformName == "identity_real" && row.Outcome == "MISMATCH");
        Assert.Equal(5, realMismatch);

        var fixtureExact = report.AblationRows.Count(row =>
            row.TransformName == "identity_fixture" && row.Outcome == "EXACT");
        Assert.True(fixtureExact >= 4, $"fixtureExact={fixtureExact}");

        var removeHistoric = report.AblationRows
            .Where(row => row.TransformName == "normalize_historic_to_plain")
            .ToArray();
        Assert.Equal(5, removeHistoric.Length);
        Assert.All(removeHistoric, row => Assert.Equal("EXACT", row.Outcome));

        var addHistoric = report.ReverseAdditionRows
            .Where(row => row.TransformName == "add_real_historic_wording")
            .ToArray();
        Assert.True(addHistoric.Count(row => row.Outcome == "MISMATCH") >= 4);

        Assert.Equal("bidirectional", report.MinimalTrigger.EvidenceStrength);
        Assert.Equal("FAMILY_WIDE", report.MinimalTrigger.FamilyClassification);
        Assert.Contains(
            report.MinimalTrigger.Features,
            feature => feature.Contains("historic_unscalable", StringComparison.OrdinalIgnoreCase));

        Assert.All(
            report.IntentionalMismatchControls,
            row => Assert.Equal("MISMATCH", row.Outcome));

        // Working non-Timeless control: parser still parses Thread-like Passage fixture unchanged by ablation code.
        var passageProbe = new ItemTextParser().Parse("""
            Item Class: Jewels
            Rarity: Unique
            Thread of Hope
            Crimson Jewel
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Only affects Passives in Massive Ring
            { Unique Modifier }
            Passive Skills in Radius can be Allocated without being connected to your tree
            Passage
            """);
        Assert.True(passageProbe.Modifiers.Count >= 2);
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
