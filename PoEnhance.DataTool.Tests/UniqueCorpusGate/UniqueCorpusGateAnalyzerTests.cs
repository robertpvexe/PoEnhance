using System.Text.Json;
using PoEnhance.DataTool.UniqueCorpusGate;

namespace PoEnhance.DataTool.Tests.UniqueCorpusGate;

public sealed class UniqueCorpusGateAnalyzerTests
{
    [Fact]
    public void AnalyzeDirectory_IngestsValidCapturesAndSkipsUnrelatedOrInvalid()
    {
        var report = AnalyzeFixtureCorpus();

        Assert.Equal(UniqueCorpusGateSchema.ReportSchemaId, report.Schema);
        Assert.Equal(7, report.Identity.CaptureFileCount);
        Assert.Equal(5, report.Identity.ParsedCaptureCount);
        Assert.Equal(2, report.Identity.SkippedFileCount);
        Assert.Equal(1, report.Identity.DeduplicatedCaptureCount);
        Assert.Equal(4, report.Identity.AnalyzedCaptureCount);
        Assert.Equal(4, report.Identity.DistinctItemNameCount);
        Assert.Equal(5, report.Identity.ModifierComponentCount);
        Assert.Contains(
            report.Identity.SkippedFiles,
            skipped => skipped.FileName == "unrelated-trade-search.json" &&
                skipped.Reason.Contains("Trade Search", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            report.Identity.SkippedFiles,
            skipped => skipped.FileName == "invalid.json" &&
                skipped.Reason.Contains("Invalid JSON", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnalyzeDirectory_CountsSupportedAmbiguousUnsupportedAndSourceFamilies()
    {
        var report = AnalyzeFixtureCorpus();

        Assert.Equal(1, report.Outcomes.Supported);
        Assert.Equal(3, report.Outcomes.Ambiguous);
        Assert.Equal(1, report.Outcomes.Unsupported);
        Assert.Equal(0, report.Outcomes.Other);
        Assert.Equal(5, report.Outcomes.Total);

        var unique = Assert.Single(
            report.OutcomesBySourceFamily,
            family => family.Key == UniqueCorpusGateSourceFamilies.Unique);
        Assert.Equal(4, unique.Total);
        Assert.Equal(1, unique.Supported);
        Assert.Equal(3, unique.Ambiguous);

        var enchantment = Assert.Single(
            report.OutcomesBySourceFamily,
            family => family.Key == UniqueCorpusGateSourceFamilies.Enchantment);
        Assert.Equal(1, enchantment.Unsupported);
    }

    [Fact]
    public void AnalyzeDirectory_GroupsRootCausesAndRanksByDistinctItemsThenComponents()
    {
        var report = AnalyzeFixtureCorpus();

        Assert.Contains(
            report.RootCauseClusters,
            cluster => cluster.RootCauseKey == "UNIQUE_MECHANICS_EXACT_CONFLICT" &&
                cluster.Stage == UniqueCorpusGateStages.UniqueSourceMechanics);
        Assert.Contains(
            report.RootCauseClusters,
            cluster => cluster.RootCauseKey == "UNIQUE_BLOCK_VERSION_MISMATCH" &&
                cluster.Stage == UniqueCorpusGateStages.VersionBlockMatching);
        Assert.Contains(
            report.RootCauseClusters,
            cluster => cluster.RootCauseKey == "POE_TRADE_STAT_MATCH_AMBIGUOUS_CANDIDATES" &&
                cluster.Stage == UniqueCorpusGateStages.ProviderAmbiguity);
        Assert.Contains(
            report.RootCauseClusters,
            cluster => cluster.RootCauseKey == "POE_TRADE_SELECTED_MODIFIER_MISSING_GAMEDATA_PROVENANCE" &&
                cluster.Stage == UniqueCorpusGateStages.ProvenanceGate);

        Assert.Equal(report.RootCauseClusters.Count, report.RankedBacklog.Count);
        for (var index = 1; index < report.RankedBacklog.Count; index++)
        {
            var previous = report.RankedBacklog[index - 1];
            var current = report.RankedBacklog[index];
            Assert.True(
                previous.DistinctItemCount > current.DistinctItemCount ||
                previous.DistinctItemCount == current.DistinctItemCount &&
                previous.ComponentCount >= current.ComponentCount);
        }
    }

    [Fact]
    public void AnalyzeDirectory_GroupsNormalizedSignaturesFromCaptureCanonicalFields()
    {
        var report = AnalyzeFixtureCorpus();

        var attackSpeed = Assert.Single(
            report.SignatureFamilies,
            family => family.NormalizedSignature == "<number>% increased Attack Speed");
        Assert.Equal(UniqueCorpusGateSourceFamilies.Unique, attackSpeed.SourceFamily);
        Assert.Equal(2, attackSpeed.DistinctItemCount);
        Assert.Equal(2, attackSpeed.ComponentCount);
        Assert.Equal(0, attackSpeed.Outcomes.Supported);
        Assert.Equal(2, attackSpeed.Outcomes.Ambiguous);
        Assert.Contains("UNIQUE_MECHANICS_EXACT_CONFLICT", attackSpeed.RootCauseKeys);
        Assert.Contains("POE_TRADE_STAT_MATCH_AMBIGUOUS_CANDIDATES", attackSpeed.RootCauseKeys);
    }

    [Fact]
    public void AnalyzeDirectory_ReportsFailureStageDistributionPreferringEarlierExplicitCodes()
    {
        var report = AnalyzeFixtureCorpus();

        Assert.Contains(
            report.FailureStages,
            stage => stage.Key == UniqueCorpusGateStages.UniqueSourceMechanics && stage.Total == 1);
        Assert.Contains(
            report.FailureStages,
            stage => stage.Key == UniqueCorpusGateStages.VersionBlockMatching && stage.Total == 1);
        Assert.Contains(
            report.FailureStages,
            stage => stage.Key == UniqueCorpusGateStages.ProviderAmbiguity && stage.Total == 1);
        Assert.Contains(
            report.FailureStages,
            stage => stage.Key == UniqueCorpusGateStages.ProvenanceGate && stage.Total == 1);
    }

    [Fact]
    public void Compare_ExactRootCauseKeysAndSignatureRegressions()
    {
        var current = AnalyzeFixtureCorpus();
        var baselineDirectory = CreateTemporaryCorpus(
            "supported-unique.json",
            "unique-source-failures.json");
        try
        {
            var baseline = UniqueCorpusGateAnalyzer.AnalyzeDirectory(baselineDirectory);
            var comparison = UniqueCorpusGateAnalyzer.Compare(current, baseline);

            Assert.Equal(UniqueCorpusGateSchema.ComparisonSchemaId, comparison.Schema);
            Assert.True(comparison.Outcomes.AmbiguousDelta >= 0);
            Assert.Contains(
                "POE_TRADE_STAT_MATCH_AMBIGUOUS_CANDIDATES",
                comparison.IntroducedClusterKeys);
            Assert.Contains(
                comparison.ClusterDeltas,
                delta => delta.RootCauseKey == "UNIQUE_MECHANICS_EXACT_CONFLICT" &&
                    delta.ComponentDelta == 0);
        }
        finally
        {
            Directory.Delete(baselineDirectory, recursive: true);
        }
    }

    [Fact]
    public void Compare_DetectsSignatureFamilyRegressionWhenSupportedBecomesFailed()
    {
        var baselineDirectory = CreateTemporaryCorpus("supported-unique.json");
        var currentDirectory = CreateTemporaryCorpus("provider-ambiguous.json");
        try
        {
            // Force same signature/family identity for regression matching by rewriting current
            // capture canonical signature to the supported fixture's signature.
            var currentPath = Directory.GetFiles(currentDirectory, "*.json").Single();
            var json = File.ReadAllText(currentPath)
                .Replace(
                    "\"canonicalSignature\": \"<number>% increased Attack Speed\"",
                    "\"canonicalSignature\": \"+<number> to Level of Socketed Gems\"",
                    StringComparison.Ordinal);
            File.WriteAllText(currentPath, json);

            var baseline = UniqueCorpusGateAnalyzer.AnalyzeDirectory(baselineDirectory);
            var current = UniqueCorpusGateAnalyzer.AnalyzeDirectory(currentDirectory);
            var comparison = UniqueCorpusGateAnalyzer.Compare(current, baseline);

            Assert.Contains(
                comparison.SignatureFamilyRegressions,
                regression =>
                    regression.NormalizedSignature == "+<number> to Level of Socketed Gems" &&
                    regression.BaselineSupported > 0 &&
                    regression.CurrentSupported == 0);
        }
        finally
        {
            Directory.Delete(baselineDirectory, recursive: true);
            Directory.Delete(currentDirectory, recursive: true);
        }
    }

    [Fact]
    public void CommandLineParser_RequiresInputAndDocumentsUsage()
    {
        var missing = UniqueCorpusGateCommandLineParser.Parse(["unique-corpus-gate"]);
        Assert.False(missing.IsValid);
        Assert.Contains(missing.Errors, error => error.Contains("--input", StringComparison.Ordinal));

        var parsed = UniqueCorpusGateCommandLineParser.Parse(
        [
            "unique-corpus-gate",
            "--input",
            "captures",
            "--output",
            "report.json",
            "--baseline",
            "baseline.json",
            "--strict",
            "--max-unclassified-cluster-components",
            "0",
            "--max-supported-coverage-drop-percent",
            "1.5",
        ]);
        Assert.True(parsed.IsValid);
        Assert.Equal("captures", parsed.Request!.InputDirectory);
        Assert.True(parsed.Request.Strict);
        Assert.Equal(0, parsed.Request.MaxUnclassifiedClusterComponents);
        Assert.Equal(1.5m, parsed.Request.MaxSupportedCoverageDropPercent);

        var usage = UniqueCorpusGateCommandLineParser.GetUsage();
        Assert.Contains("unique-corpus-gate", usage, StringComparison.Ordinal);
        Assert.Contains("--input <directory>", usage, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportPrinter_RoundTripsJsonSchema()
    {
        var report = AnalyzeFixtureCorpus();
        var path = Path.Combine(Path.GetTempPath(), $"unique-corpus-gate-{Guid.NewGuid():N}.json");
        try
        {
            UniqueCorpusGateReportPrinter.WriteJson(report, path);
            var loaded = UniqueCorpusGateReportPrinter.ReadJson(path);
            Assert.Equal(report.Schema, loaded.Schema);
            Assert.Equal(report.Outcomes.Supported, loaded.Outcomes.Supported);
            Assert.Equal(report.RootCauseClusters.Count, loaded.RootCauseClusters.Count);
            Assert.Contains("poenhance.unique-corpus-gate.v1", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static UniqueCorpusGateReport AnalyzeFixtureCorpus()
    {
        return UniqueCorpusGateAnalyzer.AnalyzeDirectory(FixtureDirectory());
    }

    private static string FixtureDirectory()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "UniqueCorpusGate");
        Assert.True(Directory.Exists(path), $"Fixture directory missing: {path}");
        return path;
    }

    private static string CreateTemporaryCorpus(params string[] fixtureFileNames)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"unique-corpus-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        foreach (var fileName in fixtureFileNames)
        {
            File.Copy(
                Path.Combine(FixtureDirectory(), fileName),
                Path.Combine(directory, fileName));
        }

        return directory;
    }
}
