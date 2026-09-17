using System.Text.Json.Nodes;
using PoEnhance.DataTool.UniqueCorpusGate;

namespace PoEnhance.DataTool.Tests.UniqueCorpusGate;

public sealed class UniqueCorpusGateReplayReadinessTests
{
    [Fact]
    public void Classify_NullReplayContext_IsAuditOnly()
    {
        Assert.Equal(
            UniqueCorpusGateReplayReadiness.AuditOnly,
            UniqueCorpusGateReplayReadiness.Classify(null));
    }

    [Fact]
    public void Classify_MissingRawClipboard_IsAuditOnly()
    {
        Assert.Equal(
            UniqueCorpusGateReplayReadiness.AuditOnly,
            UniqueCorpusGateReplayReadiness.Classify(new UniqueCorpusGateCaptureReplayContext
            {
                CaptureSchemaVersion = UniqueCorpusGateReplayReadiness.KnownReplaySchemaVersion,
                RawClipboardText = null,
                GameDataVersion = "v1",
                GameDataSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            }));
    }

    [Fact]
    public void Classify_MissingGameDataIdentity_IsAuditOnly()
    {
        Assert.Equal(
            UniqueCorpusGateReplayReadiness.AuditOnly,
            UniqueCorpusGateReplayReadiness.Classify(new UniqueCorpusGateCaptureReplayContext
            {
                CaptureSchemaVersion = UniqueCorpusGateReplayReadiness.KnownReplaySchemaVersion,
                RawClipboardText = "raw",
                GameDataVersion = "v1",
                GameDataSha256 = null,
            }));
    }

    [Fact]
    public void Classify_UnknownNewerSchema_IsAuditOnly()
    {
        Assert.Equal(
            UniqueCorpusGateReplayReadiness.AuditOnly,
            UniqueCorpusGateReplayReadiness.Classify(new UniqueCorpusGateCaptureReplayContext
            {
                CaptureSchemaVersion = "A.9.9-replay-future",
                RawClipboardText = "raw",
                GameDataVersion = "v1",
                GameDataSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            }));
    }

    [Fact]
    public void Classify_CompleteKnownSchema_IsReplayReady()
    {
        Assert.Equal(
            UniqueCorpusGateReplayReadiness.ReplayReady,
            UniqueCorpusGateReplayReadiness.Classify(new UniqueCorpusGateCaptureReplayContext
            {
                CaptureSchemaVersion = UniqueCorpusGateReplayReadiness.KnownReplaySchemaVersion,
                RawClipboardText = "Item Class: Rings\nRarity: Unique\nFixture",
                GameDataVersion = "3.29.1-test",
                GameDataSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            }));
    }

    [Fact]
    public void AnalyzeDirectory_OldCaptureWithoutReplayContext_RemainsAuditOnlyAndParses()
    {
        var directory = CreateCorpus(("legacy.json", LegacyCaptureWithoutReplayContext()));
        try
        {
            var report = UniqueCorpusGateAnalyzer.AnalyzeDirectory(
                directory,
                new UniqueCorpusGateOptions { DeduplicateLatestCapturePerItem = false });
            Assert.Equal(1, report.Identity.AnalyzedCaptureCount);
            Assert.Equal(0, report.Identity.ReplayReadyCaptureCount);
            Assert.Equal(1, report.Identity.AuditOnlyCaptureCount);
            Assert.Equal(1, report.Identity.MissingRawClipboardCount);
            Assert.Equal(1, report.Identity.MissingGameDataIdentityCount);
            Assert.NotEmpty(report.ObservationalRows);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void AnalyzeDirectory_NewReplayReadyCapture_CountsReplayReady()
    {
        var directory = CreateCorpus(("ready.json", ReplayReadyCapture()));
        try
        {
            var report = UniqueCorpusGateAnalyzer.AnalyzeDirectory(
                directory,
                new UniqueCorpusGateOptions { DeduplicateLatestCapturePerItem = false });
            Assert.Equal(1, report.Identity.ReplayReadyCaptureCount);
            Assert.Equal(0, report.Identity.AuditOnlyCaptureCount);
            Assert.Equal(["3.29.1-test"], report.Identity.DistinctGameDataVersions);
            Assert.Equal(
                ["0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"],
                report.Identity.DistinctGameDataSha256Values);
            Assert.Equal(
                [UniqueCorpusGateReplayReadiness.KnownReplaySchemaVersion],
                report.Identity.DistinctReplaySchemaVersions);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ReplayContext_DoesNotChangeStructuralFingerprintOrRowKey()
    {
        var without = CreateCorpus(("a.json", LegacyCaptureWithoutReplayContext()));
        var with = CreateCorpus(("a.json", ReplayReadyCapture()));
        try
        {
            var reportWithout = UniqueCorpusGateAnalyzer.AnalyzeDirectory(
                without,
                new UniqueCorpusGateOptions { DeduplicateLatestCapturePerItem = false });
            var reportWith = UniqueCorpusGateAnalyzer.AnalyzeDirectory(
                with,
                new UniqueCorpusGateOptions { DeduplicateLatestCapturePerItem = false });
            var left = Assert.Single(reportWithout.ObservationalRows);
            var right = Assert.Single(reportWith.ObservationalRows);
            Assert.Equal(left.Fingerprint, right.Fingerprint);
            // Row keys share identity/kind/text/indexes; occurrence ordinal may differ only by corpus composition.
            Assert.Equal(left.Facts.NormalizedSourceText, right.Facts.NormalizedSourceText);
            Assert.DoesNotContain("replay", left.Fingerprint, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("clipboard", left.RowKey, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(without, true);
            Directory.Delete(with, true);
        }
    }

    private static string CreateCorpus(params (string FileName, string Json)[] files)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"unique-replay-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        foreach (var (fileName, json) in files)
        {
            File.WriteAllText(Path.Combine(directory, fileName), json);
        }

        return directory;
    }

    private static string LegacyCaptureWithoutReplayContext() =>
        """
        {
          "diagnosticVersion": "E6b-generic-live-1",
          "item": {
            "itemClass": "Wands",
            "rarity": "Unique",
            "displayName": "Fixture Rod",
            "parsedBaseType": "Spiraled Wand"
          },
          "uniqueIdentity": {
            "canonicalName": "Fixture Rod",
            "canonicalType": "Spiraled Wand"
          },
          "modifiers": [
            {
              "componentId": "modifier:0:0",
              "sourceModifierIndex": 0,
              "sourceLineIndex": 0,
              "raw": {
                "parsedKind": "Unique",
                "uniqueOrigin": "Ordinary",
                "originalText": "+2 to Level of Socketed Gems",
                "valueLines": ["+2 to Level of Socketed Gems"]
              },
              "sourceResolution": {
                "status": "Exact",
                "resolvedModifierId": "UniqueSocketedGemLevel",
                "resolvedStatIds": ["socketed_gem_level_+"]
              },
              "resolvedSemantics": {
                "parsedKind": "Unique",
                "resolvedSourceKind": "Unique",
                "hasExactUniqueSourceProvenance": true
              },
              "providerResolution": {
                "providerResolutionStatus": "Exact",
                "providerStatId": "explicit.stat_gem_level"
              },
              "consumer": {
                "isSearchable": true,
                "availabilityStatus": "Supported"
              }
            }
          ]
        }
        """;

    private static string ReplayReadyCapture()
    {
        var root = JsonNode.Parse(LegacyCaptureWithoutReplayContext())!;
        root["replayContext"] = new JsonObject
        {
            ["captureSchemaVersion"] = UniqueCorpusGateReplayReadiness.KnownReplaySchemaVersion,
            ["rawClipboardText"] = "Item Class: Wands\nRarity: Unique\nFixture Rod\nSpiraled Wand\n--------\n+2 to Level of Socketed Gems",
            ["inputKind"] = "PathOfExileClipboard",
            ["gameDataVersion"] = "3.29.1-test",
            ["gameDataSha256"] = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            ["gameDataPathSource"] = "CommandLine",
        };
        return root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
}
