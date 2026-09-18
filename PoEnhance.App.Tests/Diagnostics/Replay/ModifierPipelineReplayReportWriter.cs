using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

internal static class ModifierPipelineReplayReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task WriteAsync(
        ModifierPipelineReplayReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        Directory.CreateDirectory(outputDirectory);

        var summaryPath = Path.Combine(outputDirectory, "replay-summary.json");
        var itemsPath = Path.Combine(outputDirectory, "replay-items.json");
        var diffPath = Path.Combine(outputDirectory, "replay-diff.json");
        var csvPath = Path.Combine(outputDirectory, "replay-diff.csv");
        var consolePath = Path.Combine(outputDirectory, "replay-console.txt");

        var summaryJson = JsonSerializer.Serialize(
            new
            {
                report.Schema,
                report.GeneratedAtUtc,
                report.InputDirectory,
                report.OutputDirectory,
                report.Summary,
                TimelessFamily = report.TimelessAnalysis?.FamilyOutcome,
                report.WorkflowNote,
                FixtureGapProven = report.FixtureGap?.MisleadingGreenFixtureGapProven,
            },
            JsonOptions);
        var itemsJson = JsonSerializer.Serialize(
            report.Items.Select(item => new
            {
                item.FileName,
                item.ItemName,
                item.Classification,
                item.Reason,
                item.RawClipboardSha256,
                item.CapturedGameDataVersion,
                item.CapturedGameDataSha256,
                item.ReplayGameDataVersion,
                item.ReplayGameDataSha256,
                Captured = item.Captured,
                Replayed = item.Replayed,
            }),
            JsonOptions);
        var diffJson = JsonSerializer.Serialize(
            new
            {
                report.TimelessAnalysis,
                report.FixtureGap,
                Diffs = report.Items
                    .Where(item => item.Deltas.Count > 0 ||
                                   item.Classification != ModifierPipelineReplayDivergenceClass.ExactMatch)
                    .Select(item => new
                    {
                        item.FileName,
                        item.ItemName,
                        item.Classification,
                        item.Reason,
                        item.Deltas,
                    }),
            },
            JsonOptions);

        await File.WriteAllTextAsync(summaryPath, summaryJson, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(itemsPath, itemsJson, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(diffPath, diffJson, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(csvPath, BuildCsv(report), cancellationToken).ConfigureAwait(false);

        var console = FormatConsole(report);
        await File.WriteAllTextAsync(consolePath, console, cancellationToken).ConfigureAwait(false);
        Console.WriteLine(console);
    }

    public static string FormatConsole(ModifierPipelineReplayReport report)
    {
        var summary = report.Summary;
        var sb = new StringBuilder();
        sb.AppendLine("Modifier Pipeline Replay (A.5.4)");
        sb.AppendLine($"Input: {summary.InputDirectory}");
        sb.AppendLine($"ReplayReady: {summary.ReplayReady}");
        sb.AppendLine($"AuditOnlyOrSchemaRefused: {summary.AuditOnlyOrSchemaRefused}");
        sb.AppendLine($"Replayed: {summary.Replayed}");
        sb.AppendLine($"GameDataMismatch: {summary.GameDataMismatch}");
        sb.AppendLine($"ExactMatch: {summary.ExactMatch}");
        sb.AppendLine($"InputEquivalentOutputDivergence: {summary.InputEquivalentOutputDivergence}");
        sb.AppendLine($"CaptureFieldUnavailable: {summary.CaptureFieldUnavailable}");
        sb.AppendLine($"ProviderContextUnverified: {summary.ProviderContextUnverified}");
        sb.AppendLine($"ReplayError: {summary.ReplayError}");
        sb.AppendLine($"Timeless result: {report.TimelessAnalysis?.FamilyOutcome ?? "NONE"}");
        if (report.FixtureGap is not null)
        {
            sb.AppendLine(
                $"Misleading-green fixture gap proven: {report.FixtureGap.MisleadingGreenFixtureGapProven}");
        }

        return sb.ToString();
    }

    private static string BuildCsv(ModifierPipelineReplayReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("fileName,itemName,classification,field,layer,capturedValue,replayValue,deltaClassification");
        foreach (var item in report.Items)
        {
            if (item.Deltas.Count == 0)
            {
                sb.AppendLine(string.Join(',',
                    Csv(item.FileName),
                    Csv(item.ItemName),
                    Csv(item.Classification),
                    "",
                    "",
                    "",
                    "",
                    ""));
                continue;
            }

            foreach (var delta in item.Deltas)
            {
                sb.AppendLine(string.Join(',',
                    Csv(item.FileName),
                    Csv(item.ItemName),
                    Csv(item.Classification),
                    Csv(delta.Field),
                    Csv(delta.Layer),
                    Csv(delta.CapturedValue),
                    Csv(delta.ReplayValue),
                    Csv(delta.Classification)));
            }
        }

        return sb.ToString();
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        {
            return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return text;
    }
}
