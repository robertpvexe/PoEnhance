using System.Text.Json;
using PoEnhance.App.Diagnostics;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Diagnostics;

[Collection(nameof(DiagnosticEnvironmentVariableCollection))]
public sealed class ModifierPipelineReplayContextCaptureTests
{
    [Fact]
    public void Capture_PreservesExactRawClipboardThroughSerialization()
    {
        var raw = "Item Class: Jewels\r\nRarity: Unique\r\nLethal Pride\r\nTimeless Jewel\r\n--------\r\nHistoric\n";
        var artifact = CaptureWithReplay(raw, "3.29.1-test", "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899");
        using var document = JsonDocument.Parse(artifact);
        var replay = document.RootElement.GetProperty("replayContext");
        Assert.Equal(raw, replay.GetProperty("rawClipboardText").GetString());
        Assert.Equal(ModifierPipelineReplayContextCapture.CurrentSchemaVersion,
            replay.GetProperty("captureSchemaVersion").GetString());
        Assert.Equal(ModifierPipelineReplayContextCapture.PathOfExileClipboardInputKind,
            replay.GetProperty("inputKind").GetString());
    }

    [Fact]
    public void Capture_PreservesMultilineClipboardAllLines()
    {
        var raw = string.Join('\n',
            "Item Class: Jewels",
            "Rarity: Unique",
            "Militant Faith",
            "Timeless Jewel",
            "--------",
            "line-one",
            "line-two",
            "Historic");
        var artifact = CaptureWithReplay(raw, "v", "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
        using var document = JsonDocument.Parse(artifact);
        var restored = document.RootElement.GetProperty("replayContext").GetProperty("rawClipboardText").GetString();
        Assert.Equal(raw, restored);
        Assert.Equal(8, restored!.Split('\n').Length);
    }

    [Fact]
    public void Capture_PreservesUnusualNumericTextExactly()
    {
        var unusual = "Commanded leadership over 14245(10000-18000) warriors under Rakiata(Akoya-Rakiata)";
        var raw = $"""
Item Class: Jewels
Rarity: Unique
Lethal Pride
Timeless Jewel
--------
{unusual}
""";
        var artifact = CaptureWithReplay(raw, "v", "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
        using var document = JsonDocument.Parse(artifact);
        Assert.Equal(
            raw,
            document.RootElement.GetProperty("replayContext").GetProperty("rawClipboardText").GetString());
        Assert.Contains(unusual, document.RootElement.GetProperty("replayContext").GetProperty("rawClipboardText").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public void Capture_RawInputEqualsParserInputAndIsNotReconstructed()
    {
        var raw = WindscreamText;
        var parsed = new ItemTextParser().Parse(raw);
        Assert.NotEqual(raw, parsed.DisplayName);
        var artifact = CaptureWithReplay(raw, "v", "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
        using var document = JsonDocument.Parse(artifact);
        Assert.Equal(
            raw,
            document.RootElement.GetProperty("replayContext").GetProperty("rawClipboardText").GetString());
        Assert.DoesNotContain("\"rawClipboardText\": null", artifact, StringComparison.Ordinal);
    }

    [Fact]
    public void Capture_RecordsGameDataVersionAndShaFromRuntimeMetadata()
    {
        var artifact = CaptureWithReplay(
            WindscreamText,
            gameDataVersion: "3.29.1.2.5-export-owner-modtextmap-positive-exact",
            gameDataSha256: "05d038a696dc095f656c27e20c411dd8cd0dfeafe333bf3c06491c647099e679",
            pathSource: "DevelopmentFallback");
        using var document = JsonDocument.Parse(artifact);
        var replay = document.RootElement.GetProperty("replayContext");
        Assert.Equal(
            "3.29.1.2.5-export-owner-modtextmap-positive-exact",
            replay.GetProperty("gameDataVersion").GetString());
        Assert.Equal(
            "05d038a696dc095f656c27e20c411dd8cd0dfeafe333bf3c06491c647099e679",
            replay.GetProperty("gameDataSha256").GetString());
        Assert.Equal("DevelopmentFallback", replay.GetProperty("gameDataPathSource").GetString());
    }

    [Fact]
    public void Capture_DoesNotEmbedUserOrMachineIdentity()
    {
        var artifact = CaptureWithReplay(
            WindscreamText,
            "v",
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
        Assert.DoesNotContain(Environment.UserName, artifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, artifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\Users\\", artifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"user", artifact, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Capture_WithoutReplayContext_StillWritesLegacyCompatibleArtifact()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"PoEnhanceReplayDiag-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        var previous = Environment.GetEnvironmentVariable(ModifierPipelineDiagnosticRecorder.EnvironmentVariableName);
        Environment.SetEnvironmentVariable(ModifierPipelineDiagnosticRecorder.EnvironmentVariableName, outputDirectory);
        try
        {
            var parsed = new ItemTextParser().Parse(WindscreamText);
            var catalog = LoadGameData();
            var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, catalog);
            var sourceResolutions = new ParsedItemModifierCandidateResolver().Resolve(parsed, catalog, baseResolution);
            var draft = Assert.IsType<TradeSearchDraft>(
                new TradeSearchDraftMapper().CreateDraft(parsed, baseResolution, sourceResolutions, catalog).Draft);
            ModifierPipelineDiagnosticRecorder.TryBeginCapture(parsed, baseResolution, sourceResolutions, draft);
            ModifierPipelineDiagnosticRecorder.TryCompleteCapture(
                draft,
                TradeSearchValidationResult.FromDiagnostics([]));
            var json = File.ReadAllText(Directory.GetFiles(outputDirectory, "*.json").Single());
            Assert.Contains("\"diagnosticVersion\": \"E6b-generic-live-1\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"replayContext\"", json, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModifierPipelineDiagnosticRecorder.EnvironmentVariableName, previous);
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static string CaptureWithReplay(
        string rawClipboardText,
        string gameDataVersion,
        string gameDataSha256,
        string? pathSource = "CommandLine")
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"PoEnhanceReplayDiag-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        var previous = Environment.GetEnvironmentVariable(ModifierPipelineDiagnosticRecorder.EnvironmentVariableName);
        Environment.SetEnvironmentVariable(ModifierPipelineDiagnosticRecorder.EnvironmentVariableName, outputDirectory);
        try
        {
            var parsed = new ItemTextParser().Parse(rawClipboardText);
            var catalog = LoadGameData();
            var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, catalog);
            var sourceResolutions = new ParsedItemModifierCandidateResolver().Resolve(parsed, catalog, baseResolution);
            var draft = Assert.IsType<TradeSearchDraft>(
                new TradeSearchDraftMapper().CreateDraft(parsed, baseResolution, sourceResolutions, catalog).Draft);
            var replay = ModifierPipelineReplayContextCapture.FromRuntime(
                rawClipboardText,
                gameDataVersion,
                gameDataSha256,
                pathSource);
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
            Environment.SetEnvironmentVariable(ModifierPipelineDiagnosticRecorder.EnvironmentVariableName, previous);
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static GameDataCatalog LoadGameData()
    {
        var result = GameDataPackageLoader
            .LoadFromFileAsync(FindRepoFile("artifacts", "poenhance-game-data.json"))
            .GetAwaiter()
            .GetResult();
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(result.Package));
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

    private const string WindscreamText = """
Item Class: Boots
Rarity: Unique
Windscream
Reinforced Greaves
--------
Quality: +20% (augmented)
Armour: 109 (augmented)
--------
Requirements:
Level: 33
Str: 60
--------
Sockets: R-R 
--------
Item Level: 55
--------
{ Unique Modifier }
You can apply an additional Curse
""";
}
