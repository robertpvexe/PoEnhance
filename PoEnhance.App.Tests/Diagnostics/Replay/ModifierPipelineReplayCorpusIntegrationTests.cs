using System.Text.Json;
using System.Text.Json.Serialization;
using PoEnhance.App.Diagnostics;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

[Collection(nameof(DiagnosticEnvironmentVariableCollection))]
public sealed class ModifierPipelineReplayCorpusIntegrationTests
{
    private static readonly JsonSerializerOptions CaptureJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

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
    public async Task Replay_HistoricalA53ReplayReadyCorpus_IsGameDataMismatchAgainstCurrentPackage()
    {
        var corpus = ResolveA53CorpusDirectory();
        if (corpus is null)
        {
            return; // Manual corpus not present on this machine; focused unit tests cover harness contracts.
        }

        var historicalReadyOnly = MaterializeHistoricalReplayReadySubset(corpus);
        var output = Path.Combine(Path.GetTempPath(), "PoEnhance-A.5.16.1-HistoricalMismatch");
        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }

        Directory.CreateDirectory(output);
        var gameDataPath = FindRepoFile("artifacts", "poenhance-game-data.json");
        try
        {
            AssertHistoricalCapturesRetainPreviousGameDataIdentity(historicalReadyOnly);

            var report = await ModifierPipelineReplayRunner.ReplayDirectoryAsync(
                historicalReadyOnly,
                gameDataPath,
                output,
                tradeStatCatalog: LoadPinnedOfficialTradeCatalog(),
                tradeItemCatalog: new PathOfExileTradeItemCatalog([]),
                filterCatalog: PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog());

            Assert.Equal(9, report.Summary.CaptureFileCount);
            Assert.Equal(9, report.Summary.ReplayReady);
            Assert.Equal(0, report.Summary.AuditOnlyOrSchemaRefused);
            Assert.Equal(9, report.Summary.GameDataMismatch);
            Assert.Equal(0, report.Summary.ReplayError);
            Assert.Equal(0, report.Summary.Replayed);
            Assert.Contains(
                "3.29.1.2.5-export-owner-modtextmap-positive-exact",
                report.Summary.DistinctCapturedGameDataVersions);
            Assert.Contains(
                "05d038a696dc095f656c27e20c411dd8cd0dfeafe333bf3c06491c647099e679",
                report.Summary.DistinctCapturedGameDataSha256Values);
            Assert.DoesNotContain(
                "3.29.1.2.8-timeless-unique-item-domain",
                report.Summary.DistinctCapturedGameDataVersions);
        }
        finally
        {
            DeleteIfTempSubset(historicalReadyOnly, corpus);
        }
    }

    [Fact]
    public async Task Replay_A53ReplayReadyCorpus_WritesReportAndClassifiesTimelessFamily()
    {
        var corpus = ResolveA53CorpusDirectory();
        if (corpus is null)
        {
            return; // Manual corpus not present on this machine; focused unit tests cover harness contracts.
        }

        var gameDataPath = FindRepoFile("artifacts", "poenhance-game-data.json");
        var identity = await ModifierPipelineReplayGameDataGate.LoadIdentityAsync(gameDataPath);
        var currentReadyOnly = MaterializeCurrentGameDataReplayReadySubset(corpus, identity);
        var output = Path.Combine(Path.GetTempPath(), "PoEnhance-A.5.4-Replay");
        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }

        Directory.CreateDirectory(output);
        try
        {
            var report = await ModifierPipelineReplayRunner.ReplayDirectoryAsync(
                currentReadyOnly,
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
            DeleteIfTempSubset(currentReadyOnly, corpus);
        }
    }

    private static string MaterializeHistoricalReplayReadySubset(string corpus)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"PoEnhance-A.5.16.1-HistoricalReplayReady-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        foreach (var path in Directory.GetFiles(corpus, "*.json").OrderBy(path => path, StringComparer.Ordinal))
        {
            var json = File.ReadAllText(path);
            if (!IsHistoricalReplayReadyPayload(json))
            {
                continue;
            }

            File.Copy(path, Path.Combine(directory, Path.GetFileName(path)), overwrite: true);
        }

        return directory;
    }

    private static string MaterializeCurrentGameDataReplayReadySubset(
        string historicalCorpus,
        ModifierPipelineReplayGameDataIdentity identity)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"PoEnhance-A.5.16.1-CurrentReplayReady-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        var index = 0;
        foreach (var path in Directory.GetFiles(historicalCorpus, "*.json")
                     .OrderBy(candidate => candidate, StringComparer.Ordinal))
        {
            var json = File.ReadAllText(path);
            if (!IsHistoricalReplayReadyPayload(json))
            {
                continue;
            }

            var historical = JsonSerializer.Deserialize<ModifierPipelineReplayCaptureDocument>(
                json,
                CaptureJsonOptions);
            var rawClipboard = historical?.ReplayContext?.RawClipboardText;
            if (string.IsNullOrEmpty(rawClipboard))
            {
                continue;
            }

            // Preserve original clipboard evidence; regenerate ReplayReady capture under CURRENT GameData
            // through the canonical diagnostic capture writer (no in-place historical rewrite).
            var rematerialized = WriteCanonicalReplayReadyCapture(
                rawClipboard,
                identity.DataVersion,
                identity.Sha256);
            var displayName = historical?.Item?.DisplayName ?? $"item-{index}";
            var fileName = $"{index:D2}-{SanitizeFileName(displayName)}.json";
            File.WriteAllText(Path.Combine(directory, fileName), rematerialized);
            index++;
        }

        Assert.Equal(9, index);
        return directory;
    }

    private static string WriteCanonicalReplayReadyCapture(
        string rawClipboardText,
        string gameDataVersion,
        string gameDataSha256)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"PoEnhance-A.5.16.1-CaptureWrite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        var previous = Environment.GetEnvironmentVariable(ModifierPipelineDiagnosticRecorder.EnvironmentVariableName);
        Environment.SetEnvironmentVariable(
            ModifierPipelineDiagnosticRecorder.EnvironmentVariableName,
            outputDirectory);
        try
        {
            var parsed = new ItemTextParser().Parse(rawClipboardText);
            var catalog = LoadActiveGameDataCatalog();
            var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, catalog);
            var sourceResolutions = new ParsedItemModifierCandidateResolver().Resolve(
                parsed,
                catalog,
                baseResolution);
            var draft = Assert.IsType<TradeSearchDraft>(
                new TradeSearchDraftMapper()
                    .CreateDraft(parsed, baseResolution, sourceResolutions, catalog)
                    .Draft);
            var replay = ModifierPipelineReplayContextCapture.FromRuntime(
                rawClipboardText,
                gameDataVersion,
                gameDataSha256,
                gameDataPathSource: "TestRematerialization");
            ModifierPipelineDiagnosticRecorder.TryBeginCapture(
                parsed,
                baseResolution,
                sourceResolutions,
                draft,
                replay);
            ModifierPipelineDiagnosticRecorder.TryCompleteCapture(
                draft,
                TradeSearchValidationResult.FromDiagnostics([]));
            return File.ReadAllText(Directory.GetFiles(outputDirectory, "*.json").Single());
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ModifierPipelineDiagnosticRecorder.EnvironmentVariableName,
                previous);
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static void AssertHistoricalCapturesRetainPreviousGameDataIdentity(string historicalReadyOnly)
    {
        foreach (var path in Directory.GetFiles(historicalReadyOnly, "*.json"))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var replay = document.RootElement.GetProperty("replayContext");
            Assert.Equal(
                "3.29.1.2.5-export-owner-modtextmap-positive-exact",
                replay.GetProperty("gameDataVersion").GetString());
            Assert.Equal(
                "05d038a696dc095f656c27e20c411dd8cd0dfeafe333bf3c06491c647099e679",
                replay.GetProperty("gameDataSha256").GetString());
        }
    }

    private static bool IsHistoricalReplayReadyPayload(string json) =>
        (json.Contains("\"captureSchemaVersion\": \"A.5.3-replay-1\"", StringComparison.Ordinal) ||
            json.Contains("\"captureSchemaVersion\":\"A.5.3-replay-1\"", StringComparison.Ordinal)) &&
        json.Contains("\"rawClipboardText\"", StringComparison.Ordinal) &&
        json.Contains("\"gameDataSha256\"", StringComparison.Ordinal);

    private static void DeleteIfTempSubset(string subset, string corpus)
    {
        if (Directory.Exists(subset) &&
            !string.Equals(subset, corpus, StringComparison.OrdinalIgnoreCase))
        {
            Directory.Delete(subset, recursive: true);
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }

        return value;
    }

    private static GameDataCatalog LoadActiveGameDataCatalog()
    {
        var result = GameDataPackageLoader
            .LoadFromFileAsync(FindRepoFile("artifacts", "poenhance-game-data.json"))
            .GetAwaiter()
            .GetResult();
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        return GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(result.Package));
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
