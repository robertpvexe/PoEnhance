namespace PoEnhance.DataTool.Tests;

public sealed class RefreshGameDataScriptContractTests
{
    [Fact]
    public void RefreshScript_IsOneNonActivatingWrapperOverExistingBuildPackagePipeline()
    {
        var scriptPath = FindScript();
        var script = File.ReadAllText(scriptPath);

        Assert.Contains(".SYNOPSIS", script, StringComparison.Ordinal);
        Assert.Contains("[Parameter(Mandatory)] [string]$SourceRoot", script, StringComparison.Ordinal);
        Assert.Contains("[Parameter(Mandatory)] [string]$SourceDataRoot", script, StringComparison.Ordinal);
        Assert.Contains("[Parameter(Mandatory)] [string]$HistoricalSourceRoot", script, StringComparison.Ordinal);
        Assert.Contains("[Parameter(Mandatory)] [string]$HistoricalSourceDataRoot", script, StringComparison.Ordinal);
        Assert.Contains("'build-package'", script, StringComparison.Ordinal);
        Assert.Contains("'--source-snapshot-dir'", script, StringComparison.Ordinal);
        Assert.Contains("'--historical-source-version'", script, StringComparison.Ordinal);
        Assert.Contains("PoEnhance.DataTool", script, StringComparison.Ordinal);
        Assert.Contains("active GameData artifact changed during refresh", script, StringComparison.Ordinal);
        Assert.Contains("Activation: not performed.", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git commit", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git push", script, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindScript()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "scripts", "Refresh-GameData.ps1");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate scripts/Refresh-GameData.ps1 from the test output.");
    }
}
