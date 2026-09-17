using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoEnhance.DataTool.UniqueCorpusGate;

public static class UniqueCorpusGateReportPrinter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Print(UniqueCorpusGateReport report, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine("Unique Runtime Corpus Gate");
        writer.WriteLine($"Schema: {report.Schema}");
        writer.WriteLine($"Input: {report.InputDirectory}");
        writer.WriteLine(
            $"Corpus: {report.Identity.DistinctItemIdentityCount} items / {report.Identity.ModifierComponentCount} modifiers");
        writer.WriteLine(
            $"Replay readiness: ReplayReady {report.Identity.ReplayReadyCaptureCount}, AuditOnly {report.Identity.AuditOnlyCaptureCount}, missing-raw {report.Identity.MissingRawClipboardCount}, missing-GameData-identity {report.Identity.MissingGameDataIdentityCount}");
        if (report.Identity.DistinctGameDataVersions.Count > 0 ||
            report.Identity.DistinctGameDataSha256Values.Count > 0 ||
            report.Identity.DistinctReplaySchemaVersions.Count > 0)
        {
            writer.WriteLine(
                $"Replay context: schemas=[{string.Join(", ", report.Identity.DistinctReplaySchemaVersions)}]; versions=[{string.Join(", ", report.Identity.DistinctGameDataVersions)}]; shaCount={report.Identity.DistinctGameDataSha256Values.Count}");
        }

        writer.WriteLine(
            $"Files: {report.Identity.CaptureFileCount} examined, {report.Identity.ParsedCaptureCount} parsed, {report.Identity.SkippedFileCount} skipped, {report.Identity.DeduplicatedCaptureCount} duplicates dropped, {report.Identity.AnalyzedCaptureCount} analyzed");
        writer.WriteLine(
            $"Outcomes: Supported {report.Outcomes.Supported} ({FormatPercent(report.Outcomes.SupportedPercent)}), Ambiguous {report.Outcomes.Ambiguous} ({FormatPercent(report.Outcomes.AmbiguousPercent)}), Unsupported {report.Outcomes.Unsupported} ({FormatPercent(report.Outcomes.UnsupportedPercent)}), Other {report.Outcomes.Other} ({FormatPercent(report.Outcomes.OtherPercent)})");

        if (report.ObservationalDiff is { } diff)
        {
            writer.WriteLine($"Hard regressions: {diff.HardRegressionCount}");
            writer.WriteLine($"Review-required: {diff.ReviewRequiredCount}");
            writer.WriteLine($"Improvements: {diff.ImprovementCount}");
            writer.WriteLine($"New rows: {diff.NewRowCount}");
        }
        else
        {
            writer.WriteLine("Hard regressions: n/a (no observational baseline)");
            writer.WriteLine("Review-required: n/a (no observational baseline)");
            writer.WriteLine("Improvements: n/a (no observational baseline)");
        }

        var invariantViolations = report.Invariants.Sum(invariant => invariant.Violations);
        writer.WriteLine($"Invariant violations: {invariantViolations}");
        var goldenPassed = report.GoldenControls.Count(control => control.Passed);
        writer.WriteLine($"Golden controls: {goldenPassed}/{report.GoldenControls.Count} passed");

        var topFailure = report.StructuralFailureClasses.FirstOrDefault();
        if (topFailure is not null)
        {
            writer.WriteLine(
                $"Top failure class: {ShortFingerprint(topFailure.Fingerprint)} affected {topFailure.AffectedItemCount} items / {topFailure.AffectedModifierCount} mods ({topFailure.DiagnosticFamily})");
        }

        writer.WriteLine();
        writer.WriteLine("Failure stages");
        foreach (var stage in report.FailureStages)
        {
            writer.WriteLine($"  {stage.Key}: {stage.Total}");
        }

        writer.WriteLine();
        writer.WriteLine("Structural failure classes (top 15)");
        foreach (var failure in report.StructuralFailureClasses.Take(15))
        {
            writer.WriteLine(
                $"  items={failure.AffectedItemCount} mods={failure.AffectedModifierCount} layer={failure.EarliestLayer} diag={failure.DiagnosticFamily}");
            writer.WriteLine($"    examples: {string.Join("; ", failure.ExampleItems)}");
            writer.WriteLine($"    fingerprint: {ShortFingerprint(failure.Fingerprint)}");
        }

        writer.WriteLine();
        writer.WriteLine("Invariants");
        foreach (var invariant in report.Invariants)
        {
            writer.WriteLine(
                $"  {invariant.Id}: evaluable={invariant.Evaluable} pass={invariant.Pass} violations={invariant.Violations}");
        }

        writer.WriteLine();
        writer.WriteLine("Golden controls");
        foreach (var golden in report.GoldenControls)
        {
            writer.WriteLine(
                $"  {(golden.Passed ? "PASS" : "FAIL")} {golden.Id}: matched={golden.MatchedRowCount} pass={golden.PassingRowCount} fail={golden.FailingRowCount}");
            if (!golden.Passed || golden.ExampleItems.Count > 0)
            {
                writer.WriteLine($"    {golden.Detail}");
            }
        }

        writer.WriteLine();
        writer.WriteLine("Ranked backlog (top 15)");
        foreach (var cluster in report.RankedBacklog.Take(15))
        {
            writer.WriteLine(
                $"  {cluster.RootCauseKey}: items={cluster.DistinctItemCount} components={cluster.ComponentCount} signatures={cluster.DistinctSignatureCount} stage={cluster.Stage} family={cluster.SourceFamily}");
        }

        if (report.Comparison is { } comparison)
        {
            writer.WriteLine();
            writer.WriteLine("Legacy report baseline comparison");
            writer.WriteLine(
                $"  Supported Δ {Signed(comparison.Outcomes.SupportedDelta)}, Ambiguous Δ {Signed(comparison.Outcomes.AmbiguousDelta)}, Unsupported Δ {Signed(comparison.Outcomes.UnsupportedDelta)}, Supported% Δ {Signed(comparison.Outcomes.SupportedPercentDelta)}");
        }

        if (report.StrictGate is { } gate)
        {
            writer.WriteLine();
            writer.WriteLine(gate.Passed ? "Strict gate: PASS" : "Strict gate: FAIL");
            foreach (var failure in gate.Failures.Take(40))
            {
                writer.WriteLine($"  {failure}");
            }

            if (gate.Failures.Count > 40)
            {
                writer.WriteLine($"  ... {gate.Failures.Count - 40} more");
            }
        }

        if (report.Identity.SkippedFiles.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine("Skipped files");
            foreach (var skipped in report.Identity.SkippedFiles)
            {
                writer.WriteLine($"  {skipped.FileName}: {skipped.Reason}");
            }
        }
    }

    public static void PrintBaselineUpdateSummary(
        UniqueCorpusGateObservationalDiff summary,
        string path,
        TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine();
        writer.WriteLine("=== OBSERVATIONAL BASELINE UPDATE ===");
        writer.WriteLine($"Wrote: {path}");
        writer.WriteLine($"Baseline rows: {summary.BaselineRowCount} → current/next rows: {summary.CurrentRowCount}");
        writer.WriteLine($"Hard regressions vs previous: {summary.HardRegressionCount}");
        writer.WriteLine($"Review-required vs previous: {summary.ReviewRequiredCount}");
        writer.WriteLine($"Improvements vs previous: {summary.ImprovementCount}");
        writer.WriteLine($"New rows vs previous: {summary.NewRowCount}");
        writer.WriteLine("NOTE: Observational baseline is a snapshot, not a declaration that every state is correct.");
        writer.WriteLine("Golden controls remain separate and are never auto-updated from broken corpus states.");
    }

    public static void WriteJson(UniqueCorpusGateReport report, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        EnsureDirectory(outputPath);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    public static UniqueCorpusGateReport ReadJson(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<UniqueCorpusGateReport>(json, JsonOptions) ??
            throw new InvalidDataException($"Baseline report was empty: {path}");
    }

    public static void WriteFailuresCsv(UniqueCorpusGateReport report, string path)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        EnsureDirectory(path);
        var builder = new StringBuilder();
        builder.AppendLine(
            "Kind,ReasonCode,RowKey,ItemName,FingerprintBefore,FingerprintAfter,Message");
        foreach (var change in report.ObservationalDiff?.Changes ?? [])
        {
            if (change.Kind is UniqueCorpusGateChangeKind.Neutral or UniqueCorpusGateChangeKind.New &&
                change.ReasonCode == "NEW_ROW")
            {
                // still include NEW and all non-neutral; include NEW
            }

            if (change.Kind == UniqueCorpusGateChangeKind.Neutral)
            {
                continue;
            }

            builder.Append(Csv(change.Kind.ToString())).Append(',')
                .Append(Csv(change.ReasonCode)).Append(',')
                .Append(Csv(change.RowKey)).Append(',')
                .Append(Csv(change.ItemName)).Append(',')
                .Append(Csv(change.FingerprintBefore)).Append(',')
                .Append(Csv(change.FingerprintAfter)).Append(',')
                .Append(Csv(change.Message)).AppendLine();
        }

        foreach (var failure in report.StructuralFailureClasses)
        {
            builder.Append(Csv("StructuralFailure")).Append(',')
                .Append(Csv(failure.DiagnosticFamily)).Append(',')
                .Append(Csv(failure.Fingerprint)).Append(',')
                .Append(Csv(string.Join(';', failure.ExampleItems))).Append(',')
                .Append(',')
                .Append(',')
                .Append(Csv($"items={failure.AffectedItemCount};mods={failure.AffectedModifierCount};layer={failure.EarliestLayer}"))
                .AppendLine();
        }

        File.WriteAllText(path, builder.ToString());
    }

    public static void WriteOutputBundle(UniqueCorpusGateReport report, string prefix)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        WriteJson(report, prefix + "-summary.json");
        if (report.ObservationalDiff is { } diff)
        {
            UniqueCorpusGateBaselineIO.WriteDiff(diff, prefix + "-diff.json");
        }

        WriteFailuresCsv(report, prefix + "-failures.csv");
    }

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string FormatPercent(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string Signed(int value) =>
        value > 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);

    private static string Signed(decimal value) =>
        value > 0
            ? $"+{value.ToString("0.##", CultureInfo.InvariantCulture)}"
            : value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string ShortFingerprint(string fingerprint) =>
        fingerprint.Length <= 120 ? fingerprint : fingerprint[..117] + "...";

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }
}
