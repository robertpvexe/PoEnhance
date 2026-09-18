using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

internal static class TimelessAblationReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task WriteAsync(
        TimelessAblationReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "timeless-input-diff.json"),
            JsonSerializer.Serialize(report.InputDiffs, JsonOptions),
            cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "timeless-parser-diff.json"),
            JsonSerializer.Serialize(report.ParserDiffs, JsonOptions),
            cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "timeless-ablation-matrix.csv"),
            ToCsv(report.AblationRows),
            cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "timeless-reverse-addition-matrix.csv"),
            ToCsv(report.ReverseAdditionRows),
            cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "timeless-minimal-trigger.json"),
            JsonSerializer.Serialize(report.MinimalTrigger, JsonOptions),
            cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "version-mismatch-structural-classes.json"),
            JsonSerializer.Serialize(
                new
                {
                    report.CorpusBlast,
                    IntentionalMismatchControls = report.IntentionalMismatchControls,
                },
                JsonOptions),
            cancellationToken).ConfigureAwait(false);

        var summary = FormatSummary(report);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "ablation-summary.txt"),
            summary,
            cancellationToken).ConfigureAwait(false);
        Console.WriteLine(summary);
    }

    public static string FormatSummary(TimelessAblationReport report)
    {
        var realExact = report.AblationRows.Count(row =>
            row.TransformName == "identity_real" && row.Outcome == "EXACT");
        var realUnscalable = report.AblationRows.Count(row =>
            row.TransformName == "identity_real" && row.HasUnscalableValue);
        var fixtureExact = report.AblationRows.Count(row =>
            row.TransformName == "identity_fixture" && row.Outcome == "EXACT");
        var fixtureTotal = report.AblationRows.Count(row => row.TransformName == "identity_fixture");
        var normalizeExact = report.AblationRows.Count(row =>
            row.TransformName == "normalize_historic_to_plain" && row.Outcome == "EXACT");
        var restoreExact = report.ReverseAdditionRows.Count(row =>
            row.TransformName == "add_real_historic_wording" && row.Outcome == "EXACT");
        var annotationPreserving = report.MinimalTrigger.AnnotationExactPreservingTransforms
            .Select(entry => $"{entry.TransformName}:{entry.AffectedItemCount}")
            .ToArray();
        var reverseStillMismatch = report.MinimalTrigger.ReverseMismatchCausingTransforms
            .Where(entry => entry.AffectedItemCount > 0)
            .Select(entry => $"{entry.TransformName}:{entry.AffectedItemCount}")
            .ToArray();
        var intentionalPreserved = report.IntentionalMismatchControls.All(row => row.Outcome == "MISMATCH");

        var sb = new StringBuilder();
        sb.AppendLine("A.5.5/A.5.6.1 Timeless Ablation Summary (post-fix)");
        sb.AppendLine($"GameData: {report.GameDataVersion} / {report.GameDataSha256}");
        sb.AppendLine($"real Timeless Exact after fix: {realExact}/5");
        sb.AppendLine($"AID unscalable present: {realUnscalable}/5");
        sb.AppendLine($"annotation normalization preserves Exact: {normalizeExact}/5");
        sb.AppendLine($"annotation restoration preserves Exact: {restoreExact}/5");
        sb.AppendLine($"green fixture Exact count: {fixtureExact}/{fixtureTotal}");
        sb.AppendLine($"annotation Exact-preserving transforms: {string.Join(", ", annotationPreserving)}");
        sb.AppendLine(
            $"reverse transforms still causing mismatch: {(reverseStillMismatch.Length == 0 ? "none" : string.Join(", ", reverseStillMismatch))}");
        sb.AppendLine($"historical root-cause trigger: {report.MinimalTrigger.HistoricalTrigger}");
        sb.AppendLine($"historical evidence strength: {report.MinimalTrigger.HistoricalEvidenceStrength}");
        sb.AppendLine($"current evidence strength: {report.MinimalTrigger.EvidenceStrength}");
        sb.AppendLine($"family classification: {report.MinimalTrigger.FamilyClassification}");
        sb.AppendLine($"post-fix behavior: {report.MinimalTrigger.PostFixBehavior}");
        sb.AppendLine(
            $"broader corpus affected items/mods: {report.CorpusBlast?.BroaderAffectedItemCount}/{report.CorpusBlast?.BroaderAffectedModifierCount}");
        sb.AppendLine($"intentional mechanical mismatches preserved: {intentionalPreserved}");
        sb.AppendLine($"earliest responsible layer: {report.MinimalTrigger.EarliestResponsibleLayer}");
        return sb.ToString();
    }

    private static string ToCsv(IReadOnlyList<TimelessAblationRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "itemName,direction,transformName,outcome,inputSha256,parserSeedValueLineCount,uniqueIdentityStatus,uniqueIdentityDiagnostic,seedBlockDiagnostic,hasUnscalableValue,isSearchable,modifierIds,statIds,invalidReason");
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(',',
                Csv(row.ItemName),
                Csv(row.Direction),
                Csv(row.TransformName),
                Csv(row.Outcome),
                Csv(row.InputSha256),
                row.ParserSeedValueLineCount.ToString(CultureInfo.InvariantCulture),
                Csv(row.UniqueIdentityStatus),
                Csv(row.UniqueIdentityDiagnostic),
                Csv(row.SeedBlockDiagnostic),
                Csv(row.HasUnscalableValue.ToString()),
                Csv(row.IsSearchable?.ToString()),
                Csv(string.Join('|', row.ModifierIds)),
                Csv(string.Join('|', row.StatIds)),
                Csv(row.InvalidReason)));
        }

        return sb.ToString();
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n'))
        {
            return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return text;
    }
}
