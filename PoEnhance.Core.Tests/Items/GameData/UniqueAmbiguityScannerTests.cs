using System.Text.Json;

namespace PoEnhance.Core.Tests.Items.GameData;

/// <summary>
/// U.2.1 test-only automated Unique ambiguity scanner.
/// Detecting Ambiguous/Unsupported findings is intended success — do not fail on their presence.
/// </summary>
public sealed class UniqueAmbiguityScannerTests
{
    [Fact]
    public async Task Scanner_EnumeratesCurrentUniques_WritesDeterministicArtifacts()
    {
        var packagePath = ResolvePackagePath();
        Assert.True(File.Exists(packagePath), $"Package not found: {packagePath}");

        var outputRoot = ResolveOutputDirectory();
        Directory.CreateDirectory(outputRoot);
        // Isolate this run so consecutive invocations can compare cleanly when desired.
        var outputDirectory = Path.Combine(
            outputRoot,
            "run-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
        Directory.CreateDirectory(outputDirectory);

        var capturesDirectory = ResolveReplayReadyCapturesDirectory();

        var artifacts = await UniqueAmbiguityScanner.RunAsync(
            packagePath,
            outputDirectory,
            capturesDirectory);

        Assert.True(
            artifacts.Summary.Denominator.UniqueIdentitiesEnumerated > 0,
            "Scanner must examine a nonempty current-Unique identity set.");
        Assert.True(
            artifacts.Summary.Denominator.UniqueIdentitiesAssessable > 0,
            "Scanner must assess at least one current Unique identity.");
        Assert.True(
            artifacts.Summary.Denominator.ModRowsAssessed > 0,
            "Scanner must assess at least one Current Unique-kind modifier block.");

        Assert.True(File.Exists(artifacts.FindingsJsonPath));
        Assert.True(File.Exists(artifacts.FindingsCsvPath));
        Assert.True(File.Exists(artifacts.SummaryMarkdownPath));
        Assert.True(File.Exists(artifacts.SummaryJsonPath));

        var statusCounts = artifacts.Summary.Denominator.FindingsByStatus;
        Assert.Contains(UniqueAmbiguityScanner.StatusExactResolved, statusCounts);
        Assert.Contains(UniqueAmbiguityScanner.StatusAmbiguous, statusCounts);
        Assert.Contains(UniqueAmbiguityScanner.StatusUnsupported, statusCounts);
        Assert.Contains(UniqueAmbiguityScanner.StatusInsufficientEvidence, statusCounts);
        Assert.Contains(UniqueAmbiguityScanner.StatusNotEvaluated, statusCounts);

        var accounted = statusCounts.Values.Sum();
        Assert.Equal(artifacts.Summary.Denominator.FindingsTotalAfterDedup, accounted);
        Assert.Equal(artifacts.Findings.Count, accounted);

        Assert.Contains(
            artifacts.Findings,
            f => f.Check == UniqueAmbiguityScanner.CheckA_CataloguePreflight);
        Assert.Contains(
            artifacts.Findings,
            f => f.Check == UniqueAmbiguityScanner.CheckC_SerializedTradeLive &&
                 f.Status == UniqueAmbiguityScanner.StatusNotEvaluated);

        // Check B runs when genuine clipboard fixtures are present; absence is not a scanner fault.
        if (Directory.Exists(capturesDirectory) &&
            Directory.GetFiles(capturesDirectory, "*.clipboard.txt").Length > 0)
        {
            Assert.True(artifacts.Summary.Denominator.GenuineClipboardCapturesReplayed > 0);
            Assert.Contains(
                artifacts.Findings,
                f => f.Check == UniqueAmbiguityScanner.CheckB_GenuineClipboardReplay &&
                     f.EvidenceTier == UniqueAmbiguityScanner.EvidenceGenuineCapture);
        }

        // Ambiguous/Unsupported presence is the intended diagnostic signal — never fail for it.
        _ = statusCounts[UniqueAmbiguityScanner.StatusAmbiguous];
        _ = statusCounts[UniqueAmbiguityScanner.StatusUnsupported];

        // Stable sort + unique dedup keys.
        Assert.Equal(
            artifacts.Findings.Select(f => f.DedupKey).Distinct(StringComparer.Ordinal).Count(),
            artifacts.Findings.Count);

        for (var i = 1; i < artifacts.Findings.Count; i++)
        {
            var previous = artifacts.Findings[i - 1];
            var current = artifacts.Findings[i];
            var cmp = string.Compare(previous.Check, current.Check, StringComparison.Ordinal);
            if (cmp > 0)
            {
                Assert.Fail($"Findings are not stably sorted by Check at index {i}.");
            }
        }

        // Round-trip JSON is well-formed.
        await using (var stream = File.OpenRead(artifacts.FindingsJsonPath))
        {
            using var document = await JsonDocument.ParseAsync(stream);
            Assert.True(document.RootElement.TryGetProperty("Findings", out var findingsElement));
            Assert.Equal(JsonValueKind.Array, findingsElement.ValueKind);
            Assert.Equal(artifacts.Findings.Count, findingsElement.GetArrayLength());
        }

        // Determinism: second run into a sibling directory must match sorted findings/counts
        // excluding GeneratedAtUtc / OutputDirectory / absolute run folder noise.
        var secondDirectory = Path.Combine(outputRoot, "run-determinism-b");
        if (Directory.Exists(secondDirectory))
        {
            Directory.Delete(secondDirectory, recursive: true);
        }

        Directory.CreateDirectory(secondDirectory);
        var second = await UniqueAmbiguityScanner.RunAsync(
            packagePath,
            secondDirectory,
            capturesDirectory);

        Assert.Equal(
            artifacts.Summary.Denominator.UniqueIdentitiesEnumerated,
            second.Summary.Denominator.UniqueIdentitiesEnumerated);
        Assert.Equal(
            artifacts.Summary.Denominator.ModRowsAssessed,
            second.Summary.Denominator.ModRowsAssessed);
        Assert.Equal(
            artifacts.Summary.Denominator.FindingsByStatus,
            second.Summary.Denominator.FindingsByStatus);
        Assert.Equal(artifacts.Findings.Count, second.Findings.Count);
        Assert.Equal(
            artifacts.Findings.Select(NormalizeFindingForCompare).ToArray(),
            second.Findings.Select(NormalizeFindingForCompare).ToArray());

        // Also write a stable "latest" pointer copy under the env/root output for owner inspection.
        CopyArtifactsToLatest(artifacts, outputRoot);
    }

    private static string NormalizeFindingForCompare(UniqueAmbiguityScanner.Finding finding) =>
        string.Join(
            '\u001f',
            finding.DedupKey,
            finding.Check,
            finding.Status,
            finding.AmbiguityKind,
            finding.UniqueName,
            finding.UniqueIdentityId,
            finding.UniqueVersionId,
            finding.UniqueBlockId,
            finding.BaseType,
            finding.ModText,
            finding.Reason,
            finding.DiagnosticCode,
            string.Join(',', finding.CandidateStatIds),
            string.Join(',', finding.CandidateModifierIds),
            string.Join(',', finding.ConflictCandidateModifierIds),
            finding.ConflictKind,
            finding.PipelineStage,
            finding.EvidenceTier,
            finding.CaptureFileName,
            finding.ParsedModifierIndex?.ToString());

    private static void CopyArtifactsToLatest(UniqueAmbiguityScanner.ScanArtifacts artifacts, string outputRoot)
    {
        var latest = Path.Combine(outputRoot, "latest");
        Directory.CreateDirectory(latest);
        File.Copy(artifacts.FindingsJsonPath, Path.Combine(latest, Path.GetFileName(artifacts.FindingsJsonPath)), overwrite: true);
        File.Copy(artifacts.FindingsCsvPath, Path.Combine(latest, Path.GetFileName(artifacts.FindingsCsvPath)), overwrite: true);
        File.Copy(artifacts.SummaryMarkdownPath, Path.Combine(latest, Path.GetFileName(artifacts.SummaryMarkdownPath)), overwrite: true);
        File.Copy(artifacts.SummaryJsonPath, Path.Combine(latest, Path.GetFileName(artifacts.SummaryJsonPath)), overwrite: true);
    }

    private static string ResolvePackagePath() =>
        Environment.GetEnvironmentVariable("POENHANCE_GAMEDATA_AUDIT_PATH")
        ?? Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));

    private static string ResolveOutputDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("POENHANCE_UNIQUE_SCAN_OUTPUT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        return Path.Combine(Path.GetTempPath(), "PoEnhance_U2_UniqueScan");
    }

    private static string ResolveReplayReadyCapturesDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "PoEnhance.Core.Tests",
                "TestData",
                "Unique",
                "ReplayReadyCaptures");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        // Fallback: output-copied fixtures next to the test assembly.
        var outputCopy = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "Unique",
            "ReplayReadyCaptures");
        return outputCopy;
    }
}
