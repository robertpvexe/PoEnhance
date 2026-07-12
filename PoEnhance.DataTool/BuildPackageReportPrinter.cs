using PoEnhance.DataImport;

namespace PoEnhance.DataTool;

public static class BuildPackageReportPrinter
{
    private const int RepresentativeDiagnosticLimit = 5;

    public static void Print(
        GameDataPackageBuildResult result,
        TextWriter writer,
        bool verboseDiagnostics = false)
    {
        writer.WriteLine($"ExitCode: {(int)result.ExitCode} ({result.ExitCode})");

        writer.WriteLine("SourceRecords:");
        foreach (var summary in result.SourceSummaries)
        {
            writer.WriteLine(
                $"  {summary.SourceName}: read={summary.SourceRecordsRead}, imported={summary.RecordsImported}, skipped={summary.RecordsSkipped}");
        }

        writer.WriteLine("Diagnostics:");
        writer.WriteLine($"  Information: {Count(result, ImportDiagnosticSeverity.Information)}");
        writer.WriteLine($"  Warning: {Count(result, ImportDiagnosticSeverity.Warning)}");
        writer.WriteLine($"  Error: {Count(result, ImportDiagnosticSeverity.Error)}");

        var warningsAndErrors = result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is ImportDiagnosticSeverity.Warning or ImportDiagnosticSeverity.Error)
            .ToArray();
        if (warningsAndErrors.Length > 0)
        {
            writer.WriteLine("WarningsAndErrors:");

            if (verboseDiagnostics)
            {
                foreach (var diagnostic in warningsAndErrors)
                {
                    WriteDiagnostic(writer, diagnostic);
                }
            }
            else
            {
                foreach (var group in GroupDiagnostics(warningsAndErrors))
                {
                    writer.WriteLine($"  {group.Key.Severity}: {group.Key.Code} count={group.Count}");
                    foreach (var diagnostic in group.Representatives)
                    {
                        var sourceRecord = string.IsNullOrWhiteSpace(diagnostic.SourceRecordId)
                            ? string.Empty
                            : $" ({diagnostic.SourceRecordId})";
                        writer.WriteLine($"    Example{sourceRecord}: {diagnostic.Message}");
                    }
                }
            }
        }

        writer.WriteLine("FinalCounts:");
        writer.WriteLine($"  ItemBases: {result.FinalCounts.ItemBases}");
        writer.WriteLine($"  Modifiers: {result.FinalCounts.Modifiers}");
        writer.WriteLine($"  Stats: {result.FinalCounts.Stats}");
        writer.WriteLine($"  StatTranslations: {result.FinalCounts.StatTranslations}");

        if (!string.IsNullOrWhiteSpace(result.OutputPath))
        {
            writer.WriteLine($"OutputPath: {result.OutputPath}");
        }

        if (result.OutputFileSizeBytes.HasValue)
        {
            writer.WriteLine($"OutputFileSizeBytes: {result.OutputFileSizeBytes.Value}");
        }

        if (!string.IsNullOrWhiteSpace(result.Sha256))
        {
            writer.WriteLine($"SHA256: {result.Sha256}");
        }
    }

    private static int Count(GameDataPackageBuildResult result, ImportDiagnosticSeverity severity)
    {
        return result.Diagnostics.Count(diagnostic => diagnostic.Severity == severity);
    }

    private static IReadOnlyList<DiagnosticGroup> GroupDiagnostics(IEnumerable<ImportDiagnostic> diagnostics)
    {
        return diagnostics
            .Where(diagnostic => diagnostic.Severity is ImportDiagnosticSeverity.Warning or ImportDiagnosticSeverity.Error)
            .GroupBy(diagnostic => new DiagnosticGroupKey(diagnostic.Severity, diagnostic.Code))
            .OrderByDescending(group => group.Key.Severity)
            .ThenBy(group => group.Key.Code, StringComparer.Ordinal)
            .Select(group => new DiagnosticGroup(
                group.Key,
                group.Count(),
                group
                    .OrderBy(diagnostic => diagnostic.SourceRecordId ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
                    .Take(RepresentativeDiagnosticLimit)
                    .ToArray()))
            .ToArray();
    }

    private static void WriteDiagnostic(TextWriter writer, ImportDiagnostic diagnostic)
    {
        var sourceRecord = string.IsNullOrWhiteSpace(diagnostic.SourceRecordId)
            ? string.Empty
            : $" ({diagnostic.SourceRecordId})";
        writer.WriteLine($"  {diagnostic.Severity}: {diagnostic.Code}{sourceRecord}: {diagnostic.Message}");
    }

    private sealed record DiagnosticGroup(
        DiagnosticGroupKey Key,
        int Count,
        IReadOnlyList<ImportDiagnostic> Representatives);

    private sealed record DiagnosticGroupKey(
        ImportDiagnosticSeverity Severity,
        string Code);
}
