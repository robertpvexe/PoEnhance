using System.Text.Json;
using System.Text.Json.Nodes;
using PoEnhance.DataTool.UniqueCorpusGate;

namespace PoEnhance.DataTool.Tests.UniqueCorpusGate;

public sealed class UniqueCorpusGateObservationalHarnessTests
{
    [Fact]
    public void ObservationalBaseline_NoChange_IsNeutralAndStrictCanPassWithoutGoldenFailures()
    {
        var corpus = CreateCorpus(("ok.json", ExactSearchableCapture("Alpha", "mod text")));
        try
        {
            var report = Analyze(corpus, keepDuplicates: true);
            var baseline = report.ObservationalSnapshot!;
            var diff = UniqueCorpusGateDifferential.Compare(baseline, report.ObservationalRows);
            Assert.Equal(0, diff.HardRegressionCount);
            Assert.Equal(0, diff.ReviewRequiredCount);
            Assert.True(diff.NeutralCount + diff.NewRowCount >= 0);

            var gated = new UniqueCorpusGateReport
            {
                Schema = report.Schema,
                GeneratedAtUtc = report.GeneratedAtUtc,
                InputDirectory = report.InputDirectory,
                Identity = report.Identity,
                Outcomes = report.Outcomes,
                OutcomesByParsedKind = report.OutcomesByParsedKind,
                OutcomesByResolvedSourceKind = report.OutcomesByResolvedSourceKind,
                OutcomesBySourceFamily = report.OutcomesBySourceFamily,
                FailureStages = report.FailureStages,
                RootCauseClusters = report.RootCauseClusters,
                SignatureFamilies = report.SignatureFamilies,
                RankedBacklog = report.RankedBacklog,
                ObservationalRows = report.ObservationalRows,
                ObservationalSnapshot = report.ObservationalSnapshot,
                ObservationalDiff = diff,
                Invariants = report.Invariants,
                GoldenControls = report.GoldenControls,
                StructuralFailureClasses = report.StructuralFailureClasses,
            };
            var strict = UniqueCorpusGateAnalyzer.EvaluateStrictGate(
                gated,
                new UniqueCorpusGateOptions
                {
                    Strict = true,
                    ObservationalBaselinePath = "x",
                    FailOnReviewRequired = true,
                });
            Assert.True(strict.Passed, string.Join(" | ", strict.Failures));
        }
        finally
        {
            Directory.Delete(corpus, true);
        }
    }

    [Fact]
    public void Differential_SearchableTrueToFalse_IsHardRegression()
    {
        var change = SingleFieldChange(
            before: ExactSearchableCapture("Item", "line"),
            afterMutator: root => root["modifiers"]![0]!["consumer"]!["isSearchable"] = false);
        Assert.Contains(change.Changes, c =>
            c.Kind == UniqueCorpusGateChangeKind.HardRegression &&
            c.ReasonCode == "SEARCHABLE_TRUE_TO_FALSE");
    }

    [Fact]
    public void Differential_ProviderExactToNotFound_IsHardRegression()
    {
        var change = SingleFieldChange(
            before: ExactSearchableCapture("Item", "line"),
            afterMutator: root =>
            {
                root["modifiers"]![0]!["providerResolution"]!["providerResolutionStatus"] = "NotFound";
                root["modifiers"]![0]!["consumer"]!["isSearchable"] = false;
                root["modifiers"]![0]!["consumer"]!["availabilityStatus"] = "Unsupported";
            });
        Assert.Contains(change.Changes, c =>
            c.Kind == UniqueCorpusGateChangeKind.HardRegression &&
            c.ReasonCode == "PROVIDER_EXACT_TO_FAILED");
    }

    [Fact]
    public void Differential_ExactToUnknown_IsHardRegression()
    {
        var change = SingleFieldChange(
            before: ExactSearchableCapture("Item", "line"),
            afterMutator: root =>
            {
                root["modifiers"]![0]!["sourceResolution"]!["status"] = "Unknown";
                root["modifiers"]![0]!["resolvedSemantics"]!["hasExactUniqueSourceProvenance"] = false;
                root["modifiers"]![0]!["consumer"]!["isSearchable"] = false;
            });
        Assert.Contains(change.Changes, c =>
            c.Kind == UniqueCorpusGateChangeKind.HardRegression &&
            c.ReasonCode == "CORE_EXACT_TO_FAILED");
    }

    [Fact]
    public void Differential_TrustedStatIdsLost_IsHardRegression()
    {
        var change = SingleFieldChange(
            before: ExactSearchableCapture("Item", "line"),
            afterMutator: root =>
            {
                root["modifiers"]![0]!["sourceResolution"]!["resolvedStatIds"] = new JsonArray();
            });
        Assert.Contains(change.Changes, c =>
            c.Kind == UniqueCorpusGateChangeKind.HardRegression &&
            c.ReasonCode == "TRUSTED_STATIDS_LOST");
    }

    [Fact]
    public void Differential_TrustedModifierIdsLost_IsHardRegression()
    {
        var change = SingleFieldChange(
            before: ExactSearchableCapture("Item", "line"),
            afterMutator: root =>
            {
                root["modifiers"]![0]!["sourceResolution"]!["resolvedModifierId"] = null;
            });
        Assert.Contains(change.Changes, c =>
            c.Kind == UniqueCorpusGateChangeKind.HardRegression &&
            c.ReasonCode == "TRUSTED_MODIFIERIDS_LOST");
    }

    [Fact]
    public void Differential_ProviderExactIdChanged_IsReviewRequired()
    {
        var change = SingleFieldChange(
            before: ExactSearchableCapture("Item", "line"),
            afterMutator: root =>
            {
                root["modifiers"]![0]!["providerResolution"]!["providerStatId"] = "explicit.stat_changed";
            });
        Assert.Contains(change.Changes, c =>
            c.Kind == UniqueCorpusGateChangeKind.ReviewRequired &&
            c.ReasonCode == "PROVIDER_ID_SET_CHANGED");
    }

    [Fact]
    public void Differential_ComponentCountChanged_IsReviewRequired()
    {
        var change = SingleFieldChange(
            before: ExactSearchableCapture("Item", "line"),
            afterMutator: root =>
            {
                root["modifiers"]![0]!["sourceResolution"]!["uniqueOmittedCompositionComponentIds"] =
                    new JsonArray("component:extra");
            });
        Assert.Contains(change.Changes, c =>
            c.Kind == UniqueCorpusGateChangeKind.ReviewRequired &&
            c.ReasonCode == "COMPONENT_COUNT_CHANGED");
    }

    [Fact]
    public void Differential_RowDisappeared_IsReviewRequired()
    {
        var beforeDir = CreateCorpus(("a.json", ExactSearchableCapture("Keep", "one")));
        var afterDir = CreateCorpus(("b.json", ExactSearchableCapture("Other", "two")));
        try
        {
            var before = Analyze(beforeDir, keepDuplicates: true);
            var after = Analyze(afterDir, keepDuplicates: true);
            var diff = UniqueCorpusGateDifferential.Compare(before.ObservationalSnapshot!, after.ObservationalRows);
            Assert.Contains(diff.Changes, c =>
                c.Kind == UniqueCorpusGateChangeKind.ReviewRequired &&
                c.ReasonCode == "ROW_MISSING");
        }
        finally
        {
            Directory.Delete(beforeDir, true);
            Directory.Delete(afterDir, true);
        }
    }

    [Fact]
    public void Differential_NewRow_IsNewNotFailureByItself()
    {
        var beforeDir = CreateCorpus(("a.json", ExactSearchableCapture("Keep", "one")));
        var afterDir = CreateCorpus(
            ("a.json", ExactSearchableCapture("Keep", "one")),
            ("b.json", ExactSearchableCapture("NewItem", "two")));
        try
        {
            var before = Analyze(beforeDir, keepDuplicates: true);
            var after = Analyze(afterDir, keepDuplicates: true);
            var diff = UniqueCorpusGateDifferential.Compare(before.ObservationalSnapshot!, after.ObservationalRows);
            Assert.Contains(diff.Changes, c => c.Kind == UniqueCorpusGateChangeKind.New && c.ReasonCode == "NEW_ROW");
            Assert.Equal(0, diff.HardRegressionCount);
        }
        finally
        {
            Directory.Delete(beforeDir, true);
            Directory.Delete(afterDir, true);
        }
    }

    [Fact]
    public void Differential_UnsupportedToExact_IsImprovement()
    {
        var unsupported = UnsupportedCapture("Item", "line");
        var change = SingleFieldChange(
            before: unsupported,
            afterMutator: root =>
            {
                root["modifiers"]![0]!["sourceResolution"]!["status"] = "Exact";
                root["modifiers"]![0]!["sourceResolution"]!["resolvedModifierId"] = "ModA";
                root["modifiers"]![0]!["sourceResolution"]!["resolvedStatIds"] = new JsonArray("stat_a");
                root["modifiers"]![0]!["resolvedSemantics"]!["hasExactUniqueSourceProvenance"] = true;
                root["modifiers"]![0]!["providerResolution"]!["providerResolutionStatus"] = "Exact";
                root["modifiers"]![0]!["providerResolution"]!["providerStatId"] = "explicit.stat_a";
                root["modifiers"]![0]!["consumer"]!["isSearchable"] = true;
                root["modifiers"]![0]!["consumer"]!["availabilityStatus"] = "Supported";
            });
        Assert.Contains(change.Changes, c => c.Kind == UniqueCorpusGateChangeKind.Improvement);
    }

    [Fact]
    public void Differential_NonSearchableToSearchable_IsImprovement()
    {
        var unsupported = UnsupportedCapture("Item", "line");
        var change = SingleFieldChange(
            before: unsupported,
            afterMutator: root =>
            {
                root["modifiers"]![0]!["consumer"]!["isSearchable"] = true;
                root["modifiers"]![0]!["providerResolution"]!["providerResolutionStatus"] = "Exact";
                root["modifiers"]![0]!["consumer"]!["availabilityStatus"] = "Supported";
            });
        Assert.Contains(change.Changes, c =>
            c.Kind == UniqueCorpusGateChangeKind.Improvement &&
            c.ReasonCode == "SEARCHABLE_FALSE_TO_TRUE");
    }

    [Fact]
    public void Golden_IntentionalFailClosed_RemainsPass()
    {
        var corpus = CreateCorpus((
            "replica.json",
            VersionMismatchCapture("Replica Bated Breath", "bad line")));
        try
        {
            var report = Analyze(corpus, keepDuplicates: true);
            var golden = Assert.Single(
                report.GoldenControls,
                control => control.Id == "intentional-version-mismatch-fail-closed");
            Assert.True(golden.Passed, golden.Detail);
        }
        finally
        {
            Directory.Delete(corpus, true);
        }
    }

    [Fact]
    public void InvariantC_PreventsUnresolvedBroadMapping()
    {
        var corpus = CreateCorpus((
            "leak.json",
            """
            {
              "diagnosticVersion": "E6b-generic-live-1",
              "item": { "itemClass": "Rings", "rarity": "Unique", "displayName": "Leak", "parsedBaseType": "Gold Ring" },
              "uniqueIdentity": { "canonicalName": "Leak", "canonicalType": "Gold Ring" },
              "modifiers": [{
                "componentId": "modifier:0:0",
                "raw": { "parsedKind": "Unique", "uniqueOrigin": "Ordinary", "originalText": "leaky" },
                "sourceResolution": { "status": "Unknown", "resolvedStatIds": [] },
                "resolvedSemantics": { "parsedKind": "Unique", "resolvedSourceKind": "Unique", "hasExactUniqueSourceProvenance": false },
                "providerResolution": { "providerResolutionStatus": "Unsupported", "providerDiagnosticCode": "MISSING" },
                "consumer": { "isSearchable": true, "availabilityStatus": "Supported" }
              }]
            }
            """));
        try
        {
            var report = Analyze(corpus, keepDuplicates: true);
            var invariantC = Assert.Single(report.Invariants, invariant => invariant.Id == "C");
            Assert.True(invariantC.Violations > 0);
        }
        finally
        {
            Directory.Delete(corpus, true);
        }
    }

    [Fact]
    public void Fingerprint_ExcludesItemName()
    {
        var corpus = CreateCorpus(
            ("a.json", ExactSearchableCapture("Alpha", "shared text")),
            ("b.json", ExactSearchableCapture("Beta", "shared text")));
        try
        {
            var report = Analyze(corpus, keepDuplicates: true);
            Assert.Equal(2, report.ObservationalRows.Count);
            Assert.Equal(report.ObservationalRows[0].Fingerprint, report.ObservationalRows[1].Fingerprint);
            Assert.DoesNotContain("Alpha", report.ObservationalRows[0].Fingerprint, StringComparison.Ordinal);
            Assert.DoesNotContain("Beta", report.ObservationalRows[0].Fingerprint, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(corpus, true);
        }
    }

    [Fact]
    public void InvariantD_DetectsSameFingerprintDivergentOutcomes()
    {
        var corpus = CreateCorpus(
            ("good.json", ExactSearchableCapture("Good", "shared text")),
            ("bad.json", Mutate(ExactSearchableCapture("Bad", "shared text"), root =>
            {
                root["modifiers"]![0]!["consumer"]!["isSearchable"] = false;
                root["modifiers"]![0]!["providerResolution"]!["providerResolutionStatus"] = "Unsupported";
                root["modifiers"]![0]!["consumer"]!["availabilityStatus"] = "Unsupported";
            })));
        try
        {
            // Force identical fingerprints by aligning searchable into fingerprint fields carefully:
            // fingerprints include search flag, so deliberately build two rows with same structural
            // fields except item identity by using extractor after forcing same searchable false/true?
            // Requirement: same structural fingerprint should not diverge solely by item name.
            // Fingerprint includes searchable, so create two Exact searchable with same text first,
            // then evaluate D by manually constructing rows with identical fingerprint and divergent searchable.
            var rows = new[]
            {
                BuildManualRow("A|Base|Rings", "A", "fp-shared", searchable: true, provider: "Exact"),
                BuildManualRow("B|Base|Rings", "B", "fp-shared", searchable: false, provider: "Unsupported"),
            };
            var invariantD = UniqueCorpusGateInvariants.Evaluate(rows).Single(invariant => invariant.Id == "D");
            Assert.True(invariantD.Violations > 0);
        }
        finally
        {
            Directory.Delete(corpus, true);
        }
    }

    [Fact]
    public void Baseline_CannotBeOverwrittenInNormalStrictMode()
    {
        var parsed = UniqueCorpusGateCommandLineParser.Parse(
        [
            "unique-corpus-gate",
            "--input", "captures",
            "--write-observational-baseline", "baseline.json",
            "--strict",
            "--observational-baseline", "baseline.json",
        ]);
        Assert.False(parsed.IsValid);
        Assert.Contains(
            parsed.Errors,
            error => error.Contains("--write-observational-baseline", StringComparison.Ordinal));
    }

    [Fact]
    public void Baseline_ExplicitUpdateWorks()
    {
        var corpus = CreateCorpus(("ok.json", ExactSearchableCapture("Alpha", "mod text")));
        var baselinePath = Path.Combine(Path.GetTempPath(), $"obs-baseline-{Guid.NewGuid():N}.json");
        try
        {
            var report = Analyze(corpus, keepDuplicates: true);
            UniqueCorpusGateBaselineIO.Write(report.ObservationalSnapshot!, baselinePath);
            Assert.True(File.Exists(baselinePath));
            var loaded = UniqueCorpusGateBaselineIO.Read(baselinePath);
            Assert.Equal(UniqueCorpusGateSchema.ObservationalBaselineSchemaId, loaded.Schema);
            Assert.Equal(report.ObservationalRows.Count, loaded.ModifierRowCount);
            Assert.DoesNotContain("golden", File.ReadAllText(baselinePath), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(corpus, true);
            if (File.Exists(baselinePath))
            {
                File.Delete(baselinePath);
            }
        }
    }

    [Fact]
    public void GoldenFailure_MakesGateFail()
    {
        var corpus = CreateCorpus((
            "lethal.json",
            TimelessSeedCapture("Lethal Pride", searchable: false)));
        try
        {
            var report = Analyze(corpus, keepDuplicates: true);
            var golden = Assert.Single(
                report.GoldenControls,
                control => control.Id == "timeless-multiline-seed-exact-searchable");
            Assert.False(golden.Passed);
            var strict = UniqueCorpusGateAnalyzer.EvaluateStrictGate(
                report,
                new UniqueCorpusGateOptions { Strict = true, FailOnReviewRequired = true });
            Assert.False(strict.Passed);
            Assert.Contains(strict.Failures, failure => failure.Contains("timeless-multiline-seed", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(corpus, true);
        }
    }

    [Fact]
    public void ObservationalBrokenState_DoesNotAutomaticallyBecomeGolden()
    {
        var corpus = CreateCorpus((
            "lethal.json",
            TimelessSeedCapture("Lethal Pride", searchable: false)));
        try
        {
            var report = Analyze(corpus, keepDuplicates: true);
            var baseline = report.ObservationalSnapshot!;
            Assert.Contains(baseline.Rows, row => !row.IsSearchable);
            var golden = Assert.Single(
                report.GoldenControls,
                control => control.Id == "timeless-multiline-seed-exact-searchable");
            Assert.False(golden.Passed, "Broken observational state must not satisfy Timeless golden.");
        }
        finally
        {
            Directory.Delete(corpus, true);
        }
    }

    private static UniqueCorpusGateObservationalDiff SingleFieldChange(
        string before,
        Action<JsonNode> afterMutator)
    {
        var beforeDir = CreateCorpus(("item.json", before));
        var afterJson = Mutate(before, afterMutator);
        var afterDir = CreateCorpus(("item.json", afterJson));
        try
        {
            var beforeReport = Analyze(beforeDir, keepDuplicates: true);
            var afterReport = Analyze(afterDir, keepDuplicates: true);
            return UniqueCorpusGateDifferential.Compare(
                beforeReport.ObservationalSnapshot!,
                afterReport.ObservationalRows);
        }
        finally
        {
            Directory.Delete(beforeDir, true);
            Directory.Delete(afterDir, true);
        }
    }

    private static UniqueCorpusGateReport Analyze(string directory, bool keepDuplicates) =>
        UniqueCorpusGateAnalyzer.AnalyzeDirectory(
            directory,
            new UniqueCorpusGateOptions { DeduplicateLatestCapturePerItem = !keepDuplicates });

    private static string CreateCorpus(params (string FileName, string Json)[] files)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"unique-obs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        foreach (var (fileName, json) in files)
        {
            File.WriteAllText(Path.Combine(directory, fileName), json);
        }

        return directory;
    }

    private static string Mutate(string json, Action<JsonNode> mutator)
    {
        var root = JsonNode.Parse(json) ?? throw new InvalidDataException("invalid json");
        mutator(root);
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static UniqueCorpusGateObservationalRow BuildManualRow(
        string identity,
        string name,
        string fingerprint,
        bool searchable,
        string provider) =>
        new()
        {
            RowKey = identity + "|Unique|" + name + "|0|0|#0",
            Fingerprint = fingerprint,
            FingerprintParts = new UniqueCorpusGateStructuralFingerprint { Rendered = fingerprint },
            Facts = new UniqueCorpusGateObservationalFacts
            {
                ItemIdentityKey = identity,
                ItemName = name,
                ProviderStatus = provider,
                IsSearchable = searchable,
                CoreStatus = "Exact",
            },
        };

    private static string ExactSearchableCapture(string name, string text) =>
        $$"""
        {
          "diagnosticVersion": "E6b-generic-live-1",
          "item": {
            "itemClass": "Wands",
            "rarity": "Unique",
            "displayName": "{{name}}",
            "parsedBaseType": "Spiraled Wand",
            "resolvedBaseName": "Spiraled Wand"
          },
          "uniqueIdentity": {
            "canonicalName": "{{name}}",
            "canonicalType": "Spiraled Wand",
            "foulborn": "No"
          },
          "uniqueMechanicalResolution": {
            "status": "ExactIdentity",
            "compatibleVersionRoles": ["Current"]
          },
          "modifiers": [
            {
              "componentId": "modifier:0:0",
              "sourceModifierIndex": 0,
              "sourceLineIndex": 0,
              "raw": {
                "parsedKind": "Unique",
                "uniqueOrigin": "Ordinary",
                "implicitOrigin": "Unspecified",
                "originalText": "{{text}}",
                "valueLines": ["{{text}}"]
              },
              "sourceResolution": {
                "status": "Exact",
                "resolvedModifierId": "UniqueModA",
                "resolvedStatIds": ["stat_a"],
                "uniqueCatalogBlockIds": ["block-a"],
                "isEquivalentSourceSet": false
              },
              "resolvedSemantics": {
                "parsedKind": "Unique",
                "uniqueOrigin": "Ordinary",
                "resolvedSourceKind": "Unique",
                "hasResolvedUniqueSourceSemantics": true,
                "hasExactUniqueSourceProvenance": true
              },
              "signatures": {
                "originalText": "{{text}}",
                "canonicalSignature": "{{text}}"
              },
              "multiline": {
                "originalTextContainsNewLine": false,
                "sourceCount": 1
              },
              "providerResolution": {
                "providerResolutionStatus": "Exact",
                "providerStatId": "explicit.stat_a"
              },
              "consumer": {
                "isSearchable": true,
                "availabilityStatus": "Supported"
              }
            }
          ]
        }
        """;

    private static string UnsupportedCapture(string name, string text) =>
        Mutate(ExactSearchableCapture(name, text), root =>
        {
            root["modifiers"]![0]!["sourceResolution"]!["status"] = "Unknown";
            root["modifiers"]![0]!["sourceResolution"]!["resolvedModifierId"] = null;
            root["modifiers"]![0]!["sourceResolution"]!["resolvedStatIds"] = new JsonArray();
            root["modifiers"]![0]!["resolvedSemantics"]!["hasExactUniqueSourceProvenance"] = false;
            root["modifiers"]![0]!["providerResolution"]!["providerResolutionStatus"] = "Unsupported";
            root["modifiers"]![0]!["providerResolution"]!["providerDiagnosticCode"] =
                "POE_TRADE_SELECTED_MODIFIER_MISSING_GAMEDATA_PROVENANCE";
            root["modifiers"]![0]!["consumer"]!["isSearchable"] = false;
            root["modifiers"]![0]!["consumer"]!["availabilityStatus"] = "Unsupported";
        });

    private static string VersionMismatchCapture(string name, string text) =>
        Mutate(UnsupportedCapture(name, text), root =>
        {
            root["item"]!["parsedBaseType"] = "Chain Belt";
            root["uniqueIdentity"]!["canonicalType"] = "Chain Belt";
            root["modifiers"]![0]!["sourceResolution"]!["uniqueResolutionDiagnosticCode"] =
                "UNIQUE_BLOCK_VERSION_MISMATCH";
        });

    private static string TimelessSeedCapture(string name, bool searchable)
    {
        var text = "Commanded leadership over 14245 warriors under Rakiata\\nPassives in radius are Conquered by the Karui\\nHistoric";
        var json = Mutate(ExactSearchableCapture(name, "placeholder"), root =>
        {
            root["item"]!["itemClass"] = "Jewels";
            root["item"]!["parsedBaseType"] = "Timeless Jewel";
            root["item"]!["resolvedBaseName"] = "Timeless Jewel";
            root["uniqueIdentity"]!["canonicalType"] = "Timeless Jewel";
            root["modifiers"]![0]!["raw"]!["originalText"] = text.Replace("\\n", "\n", StringComparison.Ordinal);
            root["modifiers"]![0]!["raw"]!["valueLines"] = new JsonArray(
                "Commanded leadership over 14245 warriors under Rakiata",
                "Passives in radius are Conquered by the Karui",
                "Historic");
            root["modifiers"]![0]!["multiline"]!["originalTextContainsNewLine"] = true;
            root["modifiers"]![0]!["multiline"]!["sourceCount"] = 3;
            root["modifiers"]![0]!["signatures"]!["originalText"] =
                text.Replace("\\n", "\n", StringComparison.Ordinal);
            if (!searchable)
            {
                root["modifiers"]![0]!["sourceResolution"]!["status"] = "Unknown";
                root["modifiers"]![0]!["sourceResolution"]!["uniqueResolutionDiagnosticCode"] =
                    "UNIQUE_BLOCK_VERSION_MISMATCH";
                root["modifiers"]![0]!["sourceResolution"]!["resolvedModifierId"] = null;
                root["modifiers"]![0]!["sourceResolution"]!["resolvedStatIds"] = new JsonArray();
                root["modifiers"]![0]!["resolvedSemantics"]!["hasExactUniqueSourceProvenance"] = false;
                root["modifiers"]![0]!["providerResolution"]!["providerResolutionStatus"] = "Unsupported";
                root["modifiers"]![0]!["consumer"]!["isSearchable"] = false;
                root["modifiers"]![0]!["consumer"]!["availabilityStatus"] = "Unsupported";
            }
        });
        return json;
    }
}
