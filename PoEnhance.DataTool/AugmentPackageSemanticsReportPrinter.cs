using PoEnhance.DataImport;

namespace PoEnhance.DataTool;

public static class AugmentPackageSemanticsReportPrinter
{
    public static void Print(GameDataPackageSemanticAugmentationResult result, TextWriter writer)
    {
        writer.WriteLine($"ExitCode: {(int)result.ExitCode} ({result.ExitCode})");
        writer.WriteLine($"Errors: {result.Diagnostics.Count(diagnostic => diagnostic.Severity == ImportDiagnosticSeverity.Error)}");
        foreach (var diagnostic in result.Diagnostics)
        {
            var sourceRecord = string.IsNullOrWhiteSpace(diagnostic.SourceRecordId)
                ? string.Empty
                : $" ({diagnostic.SourceRecordId})";
            writer.WriteLine($"  {diagnostic.Severity}: {diagnostic.Code}{sourceRecord}: {diagnostic.Message}");
        }

        writer.WriteLine("FinalCounts:");
        writer.WriteLine($"  ItemBases: {result.FinalCounts.ItemBases}");
        writer.WriteLine($"  Modifiers: {result.FinalCounts.Modifiers}");
        writer.WriteLine($"  Stats: {result.FinalCounts.Stats}");
        writer.WriteLine($"  StatTranslations: {result.FinalCounts.StatTranslations}");
        writer.WriteLine($"  ItemPropertySemantics: {result.FinalCounts.ItemPropertySemantics}");

        WriteValue(writer, "InputPackagePath", result.InputPackagePath);
        WriteValue(writer, "InputPackageSizeBytes", result.InputPackageSizeBytes);
        WriteValue(writer, "InputPackageSHA256", result.InputPackageSha256);
        WriteValue(writer, "OutputPath", result.OutputPath);
        WriteValue(writer, "OutputFileSizeBytes", result.OutputFileSizeBytes);
        WriteValue(writer, "SHA256", result.Sha256);
    }

    private static void WriteValue(TextWriter writer, string label, object? value)
    {
        if (value is not null && !string.IsNullOrWhiteSpace(value.ToString()))
        {
            writer.WriteLine($"{label}: {value}");
        }
    }
}
