using System.Globalization;
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
    };

    public static void Print(UniqueCorpusGateReport report, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine("Unique Runtime Corpus Gate");
        writer.WriteLine($"Schema: {report.Schema}");
        writer.WriteLine($"Input: {report.InputDirectory}");
        writer.WriteLine(
            $"Files: {report.Identity.CaptureFileCount} examined, {report.Identity.ParsedCaptureCount} parsed, {report.Identity.SkippedFileCount} skipped, {report.Identity.DeduplicatedCaptureCount} duplicates dropped, {report.Identity.AnalyzedCaptureCount} analyzed");
        writer.WriteLine(
            $"Items: {report.Identity.DistinctItemNameCount} names, {report.Identity.DistinctItemIdentityCount} identities, {report.Identity.ModifierComponentCount} components");
        writer.WriteLine(
            $"Outcomes: Supported {report.Outcomes.Supported} ({FormatPercent(report.Outcomes.SupportedPercent)}), Ambiguous {report.Outcomes.Ambiguous} ({FormatPercent(report.Outcomes.AmbiguousPercent)}), Unsupported {report.Outcomes.Unsupported} ({FormatPercent(report.Outcomes.UnsupportedPercent)}), Other {report.Outcomes.Other} ({FormatPercent(report.Outcomes.OtherPercent)})");

        writer.WriteLine();
        writer.WriteLine("Failure stages");
        foreach (var stage in report.FailureStages)
        {
            writer.WriteLine($"  {stage.Key}: {stage.Total}");
        }

        writer.WriteLine();
        writer.WriteLine("Source families");
        foreach (var family in report.OutcomesBySourceFamily)
        {
            writer.WriteLine(
                $"  {family.Key}: {family.Total} (Supported {family.Supported}, Ambiguous {family.Ambiguous}, Unsupported {family.Unsupported}, Other {family.Other})");
        }

        writer.WriteLine();
        writer.WriteLine("Ranked backlog (top 15)");
        foreach (var cluster in report.RankedBacklog.Take(15))
        {
            writer.WriteLine(
                $"  {cluster.RootCauseKey}: items={cluster.DistinctItemCount} components={cluster.ComponentCount} signatures={cluster.DistinctSignatureCount} stage={cluster.Stage} family={cluster.SourceFamily}");
            if (cluster.TopSignatures.Count > 0)
            {
                writer.WriteLine($"    signatures: {string.Join(" | ", cluster.TopSignatures.Take(3))}");
            }
        }

        writer.WriteLine();
        writer.WriteLine("Repeated signature families (top 15)");
        foreach (var family in report.SignatureFamilies.Take(15))
        {
            writer.WriteLine(
                $"  [{family.SourceFamily}] {family.NormalizedSignature}: items={family.DistinctItemCount} components={family.ComponentCount} Supported={family.Outcomes.Supported} Ambiguous={family.Outcomes.Ambiguous} Unsupported={family.Outcomes.Unsupported}");
        }

        if (report.Comparison is { } comparison)
        {
            writer.WriteLine();
            writer.WriteLine("Baseline comparison");
            writer.WriteLine(
                $"  Supported Δ {Signed(comparison.Outcomes.SupportedDelta)}, Ambiguous Δ {Signed(comparison.Outcomes.AmbiguousDelta)}, Unsupported Δ {Signed(comparison.Outcomes.UnsupportedDelta)}, Supported% Δ {Signed(comparison.Outcomes.SupportedPercentDelta)}");
            if (comparison.IntroducedClusterKeys.Count > 0)
            {
                writer.WriteLine($"  Introduced clusters: {string.Join(", ", comparison.IntroducedClusterKeys)}");
            }

            if (comparison.ResolvedClusterKeys.Count > 0)
            {
                writer.WriteLine($"  Resolved clusters: {string.Join(", ", comparison.ResolvedClusterKeys)}");
            }

            if (comparison.SignatureFamilyRegressions.Count > 0)
            {
                writer.WriteLine("  Signature regressions:");
                foreach (var regression in comparison.SignatureFamilyRegressions)
                {
                    writer.WriteLine(
                        $"    {regression.SourceFamily} | {regression.NormalizedSignature}: Supported {regression.BaselineSupported}->{regression.CurrentSupported}");
                }
            }
        }

        if (report.StrictGate is { } gate)
        {
            writer.WriteLine();
            writer.WriteLine(gate.Passed ? "Strict gate: PASS" : "Strict gate: FAIL");
            foreach (var failure in gate.Failures)
            {
                writer.WriteLine($"  {failure}");
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

    public static void WriteJson(UniqueCorpusGateReport report, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    public static UniqueCorpusGateReport ReadJson(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<UniqueCorpusGateReport>(json, JsonOptions) ??
            throw new InvalidDataException($"Baseline report was empty: {path}");
    }

    private static string FormatPercent(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string Signed(int value) =>
        value > 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);

    private static string Signed(decimal value) =>
        value > 0
            ? $"+{value.ToString("0.##", CultureInfo.InvariantCulture)}"
            : value.ToString("0.##", CultureInfo.InvariantCulture);
}
