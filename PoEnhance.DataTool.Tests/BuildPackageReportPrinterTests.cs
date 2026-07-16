using PoEnhance.DataImport;

namespace PoEnhance.DataTool.Tests;

public sealed class BuildPackageReportPrinterTests
{
    [Fact]
    public void Print_GroupsWarningAndErrorDiagnosticsByCode()
    {
        var output = Print(CreateResult(
            Diagnostic("CODE_B", ImportDiagnosticSeverity.Warning, "b1"),
            Diagnostic("CODE_A", ImportDiagnosticSeverity.Warning, "a1"),
            Diagnostic("CODE_A", ImportDiagnosticSeverity.Warning, "a2"),
            Diagnostic("CODE_ERROR", ImportDiagnosticSeverity.Error, "e1")));

        Assert.Contains("  Warning: 3", output, StringComparison.Ordinal);
        Assert.Contains("  Error: 1", output, StringComparison.Ordinal);
        Assert.Contains("  Error: CODE_ERROR count=1", output, StringComparison.Ordinal);
        Assert.Contains("  Warning: CODE_A count=2", output, StringComparison.Ordinal);
        Assert.Contains("  Warning: CODE_B count=1", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Warning: CODE_A (a1):", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Print_LimitsGroupedDiagnosticRepresentativesToFive()
    {
        var output = Print(CreateResult(
            Diagnostic("SAME_CODE", ImportDiagnosticSeverity.Warning, "r1"),
            Diagnostic("SAME_CODE", ImportDiagnosticSeverity.Warning, "r2"),
            Diagnostic("SAME_CODE", ImportDiagnosticSeverity.Warning, "r3"),
            Diagnostic("SAME_CODE", ImportDiagnosticSeverity.Warning, "r4"),
            Diagnostic("SAME_CODE", ImportDiagnosticSeverity.Warning, "r5"),
            Diagnostic("SAME_CODE", ImportDiagnosticSeverity.Warning, "r6")));

        Assert.Contains("  Warning: SAME_CODE count=6", output, StringComparison.Ordinal);
        Assert.Equal(5, CountOccurrences(output, "    Example"));
        Assert.Contains("Example (r5):", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Example (r6):", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Print_VerboseDiagnosticsPrintsEveryWarningAndError()
    {
        var output = Print(
            CreateResult(
                Diagnostic("SAME_CODE", ImportDiagnosticSeverity.Warning, "r1"),
                Diagnostic("SAME_CODE", ImportDiagnosticSeverity.Warning, "r2"),
                Diagnostic("SAME_CODE", ImportDiagnosticSeverity.Warning, "r3"),
                Diagnostic("SAME_CODE", ImportDiagnosticSeverity.Warning, "r4"),
                Diagnostic("SAME_CODE", ImportDiagnosticSeverity.Warning, "r5"),
                Diagnostic("SAME_CODE", ImportDiagnosticSeverity.Warning, "r6")),
            verboseDiagnostics: true);

        Assert.DoesNotContain("count=6", output, StringComparison.Ordinal);
        Assert.Contains("  Warning: SAME_CODE (r1): diagnostic for r1", output, StringComparison.Ordinal);
        Assert.Contains("  Warning: SAME_CODE (r6): diagnostic for r6", output, StringComparison.Ordinal);
        Assert.Equal(6, CountOccurrences(output, "  Warning: SAME_CODE ("));
    }

    [Fact]
    public void Print_GroupsDiagnosticsDeterministically()
    {
        var output = Print(CreateResult(
            Diagnostic("CODE_B", ImportDiagnosticSeverity.Warning, "b2"),
            Diagnostic("CODE_A", ImportDiagnosticSeverity.Warning, "a2"),
            Diagnostic("CODE_B", ImportDiagnosticSeverity.Warning, "b1"),
            Diagnostic("CODE_A", ImportDiagnosticSeverity.Warning, "a1")));

        Assert.True(output.IndexOf("  Warning: CODE_A count=2", StringComparison.Ordinal) <
            output.IndexOf("  Warning: CODE_B count=2", StringComparison.Ordinal));
        Assert.True(output.IndexOf("Example (a1):", StringComparison.Ordinal) <
            output.IndexOf("Example (a2):", StringComparison.Ordinal));
        Assert.True(output.IndexOf("Example (b1):", StringComparison.Ordinal) <
            output.IndexOf("Example (b2):", StringComparison.Ordinal));
    }

    [Fact]
    public void Print_GroupedDiagnosticsPreservesTotalCounts()
    {
        var output = Print(CreateResult(
            Diagnostic("WARNING_A", ImportDiagnosticSeverity.Warning, "w1"),
            Diagnostic("WARNING_A", ImportDiagnosticSeverity.Warning, "w2"),
            Diagnostic("WARNING_B", ImportDiagnosticSeverity.Warning, "w3"),
            Diagnostic("ERROR_A", ImportDiagnosticSeverity.Error, "e1"),
            Diagnostic("INFO_A", ImportDiagnosticSeverity.Information, "i1")));

        Assert.Contains("  Information: 1", output, StringComparison.Ordinal);
        Assert.Contains("  Warning: 3", output, StringComparison.Ordinal);
        Assert.Contains("  Error: 1", output, StringComparison.Ordinal);
        Assert.Contains("  Warning: WARNING_A count=2", output, StringComparison.Ordinal);
        Assert.Contains("  Warning: WARNING_B count=1", output, StringComparison.Ordinal);
        Assert.Contains("  Error: ERROR_A count=1", output, StringComparison.Ordinal);
        Assert.DoesNotContain("INFO_A", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Print_ReportsItemPropertySemanticCount()
    {
        var output = Print(new GameDataPackageBuildResult
        {
            ExitCode = GameDataPackageBuildExitCode.Success,
            FinalCounts = new GameDataPackageBuildRecordCounts
            {
                ItemPropertySemantics = 6,
            },
        });

        Assert.Contains("  ItemPropertySemantics: 6", output, StringComparison.Ordinal);
    }

    private static string Print(GameDataPackageBuildResult result, bool verboseDiagnostics = false)
    {
        using var writer = new StringWriter();

        BuildPackageReportPrinter.Print(result, writer, verboseDiagnostics);

        return writer.ToString();
    }

    private static GameDataPackageBuildResult CreateResult(params ImportDiagnostic[] diagnostics)
    {
        return new GameDataPackageBuildResult
        {
            ExitCode = GameDataPackageBuildExitCode.Success,
            Diagnostics = diagnostics,
            SourceSummaries =
            [
                new GameDataPackageBuildSourceSummary
                {
                    SourceName = "Modifiers",
                    SourceRecordsRead = 10,
                    RecordsImported = 4,
                    RecordsSkipped = 6,
                },
            ],
            FinalCounts = new GameDataPackageBuildRecordCounts
            {
                Modifiers = 4,
            },
        };
    }

    private static ImportDiagnostic Diagnostic(
        string code,
        ImportDiagnosticSeverity severity,
        string sourceRecordId)
    {
        return new ImportDiagnostic(code, severity, sourceRecordId, $"diagnostic for {sourceRecordId}");
    }

    private static int CountOccurrences(string value, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }
}
