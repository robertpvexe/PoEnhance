using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

public sealed class ModifierPipelineReplayCorpusIntegrationTests
{
    public static string? ResolveA53CorpusDirectory()
    {
        var fromEnv = Environment.GetEnvironmentVariable("POENHANCE_A53_REPLAY_CORPUS");
        if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
        {
            return fromEnv;
        }

        var fallback = Path.Combine(Path.GetTempPath(), "PoEnhance-A.5.3-ReplayReady");
        return Directory.Exists(fallback) ? fallback : null;
    }

    [Fact]
    public async Task Replay_A53ReplayReadyCorpus_WritesReportAndClassifiesTimelessFamily()
    {
        var corpus = ResolveA53CorpusDirectory();
        if (corpus is null)
        {
            return; // Manual corpus not present on this machine; focused unit tests cover harness contracts.
        }

        var readyOnly = MaterializeReplayReadySubset(corpus);
        var output = Path.Combine(Path.GetTempPath(), "PoEnhance-A.5.4-Replay");
        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }

        Directory.CreateDirectory(output);
        var gameDataPath = FindRepoFile("artifacts", "poenhance-game-data.json");
        try
        {
            var report = await ModifierPipelineReplayRunner.ReplayDirectoryAsync(
                readyOnly,
                gameDataPath,
                output,
                tradeStatCatalog: LoadPinnedOfficialTradeCatalog(),
                tradeItemCatalog: new PathOfExileTradeItemCatalog([]),
                filterCatalog: PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog());

            Assert.Equal(9, report.Summary.CaptureFileCount);
            Assert.Equal(9, report.Summary.ReplayReady);
            Assert.Equal(0, report.Summary.AuditOnlyOrSchemaRefused);
            Assert.Equal(0, report.Summary.GameDataMismatch);
            Assert.Equal(0, report.Summary.ReplayError);
            Assert.Equal(9, report.Summary.Replayed);
            Assert.True(File.Exists(Path.Combine(output, "replay-summary.json")));
            Assert.True(File.Exists(Path.Combine(output, "replay-items.json")));
            Assert.True(File.Exists(Path.Combine(output, "replay-diff.json")));
            Assert.True(File.Exists(Path.Combine(output, "replay-diff.csv")));

            Assert.NotNull(report.TimelessAnalysis);
            Assert.Contains(report.TimelessAnalysis!.FamilyOutcome, (string[])["T1", "T2", "T3"]);
            Assert.Contains(report.Items, item => item.ItemName == "The Battle Within");
            Assert.Contains(report.Items, item => item.ItemName == "Grasping Nightshade");
            Assert.Contains(report.Items, item => item.ItemName == "Thread of Hope");
            Assert.True(report.Items.Count(item => item.ItemName == "Thread of Hope") >= 2);
            Assert.True(report.FixtureGap?.MisleadingGreenFixtureGapProven == true);
        }
        finally
        {
            if (Directory.Exists(readyOnly) &&
                !string.Equals(readyOnly, corpus, StringComparison.OrdinalIgnoreCase))
            {
                Directory.Delete(readyOnly, recursive: true);
            }
        }
    }

    private static string MaterializeReplayReadySubset(string corpus)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"PoEnhance-A.5.4-ReplayReadyOnly-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        foreach (var path in Directory.GetFiles(corpus, "*.json"))
        {
            var json = File.ReadAllText(path);
            if (!json.Contains("\"captureSchemaVersion\": \"A.5.3-replay-1\"", StringComparison.Ordinal) &&
                !json.Contains("\"captureSchemaVersion\":\"A.5.3-replay-1\"", StringComparison.Ordinal))
            {
                continue;
            }

            if (!json.Contains("\"rawClipboardText\"", StringComparison.Ordinal) ||
                !json.Contains("\"gameDataSha256\"", StringComparison.Ordinal))
            {
                continue;
            }

            File.Copy(path, Path.Combine(directory, Path.GetFileName(path)), overwrite: true);
        }

        return directory;
    }

    private static PathOfExileTradeStatCatalog LoadPinnedOfficialTradeCatalog()
    {
        var path = FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Trade",
            "official-stats-2026-08-19.json");
        var result = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(File.ReadAllText(path));
        Assert.True(result.IsSuccess);
        return Assert.IsType<PathOfExileTradeStatCatalog>(result.Catalog);
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
