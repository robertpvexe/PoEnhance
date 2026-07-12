using PoEnhance.DataImport;

namespace PoEnhance.DataTool;

public static class BuildPackageReportPrinter
{
    public static void Print(GameDataPackageBuildResult result, TextWriter writer)
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
            foreach (var diagnostic in warningsAndErrors)
            {
                var sourceRecord = string.IsNullOrWhiteSpace(diagnostic.SourceRecordId)
                    ? string.Empty
                    : $" ({diagnostic.SourceRecordId})";
                writer.WriteLine($"  {diagnostic.Severity}: {diagnostic.Code}{sourceRecord}: {diagnostic.Message}");
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
}
