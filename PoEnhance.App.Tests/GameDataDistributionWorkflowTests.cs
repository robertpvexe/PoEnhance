using System.Text.Json;

namespace PoEnhance.App.Tests;

public sealed class GameDataDistributionWorkflowTests
{
    [Fact]
    public void PinnedMetadata_DefinesValidatedPackageAndEveryAcquiredSource()
    {
        using var document = JsonDocument.Parse(ReadRepositoryFile(
            "data",
            "game-data",
            "sources.json"));
        var root = document.RootElement;
        var package = root.GetProperty("package");

        Assert.Equal(3, package.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(
            "3.29.1.2.2-unique-block-identity",
            package.GetProperty("dataVersion").GetString());
        Assert.Equal("2026-09-02T08:00:00+00:00", package.GetProperty("createdAtUtc").GetString());
        Assert.Equal(187974871, package.GetProperty("sizeBytes").GetInt64());
        Assert.Equal(
            "65e37fd0e76b9318e89aa311cf8ac93a1d0821d4ca90d6ffc50642405134f07a",
            package.GetProperty("sha256").GetString());
        Assert.Equal(353, package.GetProperty("foulbornRelationshipCount").GetInt32());
        Assert.Equal(353, package.GetProperty("exactFoulbornRelationshipCount").GetInt32());
        Assert.Equal(0, package.GetProperty("unsupportedFoulbornRelationshipCount").GetInt32());

        Assert.Equal(
            "34a9bd548eba7c3b62ab1d1f19a99ae8b12f1564",
            root.GetProperty("currentRePoe").GetProperty("commitSha").GetString());
        Assert.Equal(
            "be8246f83dd452f86b90bd27c6de85945bf68ce2",
            root.GetProperty("currentRePoe").GetProperty("hostedExportCommitSha").GetString());
        Assert.Equal(
            "c50acab2ed660a70511e7f91ee09db4e632089e4",
            root.GetProperty("historicalRePoe").GetProperty("commitSha").GetString());
        Assert.Equal(
            "5098abdf44ad4fa0fc5e63d995575e10e82ca75b",
            root.GetProperty("historicalRePoe").GetProperty("hostedExportCommitSha").GetString());
        Assert.Equal(
            "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
            root.GetProperty("pathOfBuilding").GetProperty("commitSha").GetString());
        Assert.Equal("v2.67.2", root.GetProperty("pathOfBuilding").GetProperty("tag").GetString());
        Assert.Equal(
            "6f2bb701c410750602477db001d2816f509cfda8fdcf2d3f6d68ebc9047c72aa",
            root.GetProperty("pathOfBuilding").GetProperty("evaluatedUniquesSha256").GetString());
    }

    [Fact]
    public void SetupScript_AcquiresBuildsTwiceVerifiesAndAtomicallyActivates()
    {
        var script = ReadRepositoryFile("scripts", "Setup-GameData.ps1");

        Assert.Contains("Ensure-GitCheckout", script, StringComparison.Ordinal);
        Assert.Contains("Remove-InvalidDirectTempGitCache", script, StringComparison.Ordinal);
        Assert.Contains("Ensure-HostedExportCheckout", script, StringComparison.Ordinal);
        Assert.Contains("Export-PinnedData", script, StringComparison.Ordinal);
        Assert.Contains("core.autocrlf=false", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-ReproductionBuild $firstBuildRoot", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-ReproductionBuild $secondBuildRoot", script, StringComparison.Ordinal);
        Assert.Contains("evaluatedUniquesSha256", script, StringComparison.Ordinal);
        Assert.Contains("foulbornRelationshipCount", script, StringComparison.Ordinal);
        Assert.Contains("PoEnhance-StageE5-GeneratedSpecial", script, StringComparison.Ordinal);
        Assert.Contains("candidate-build-1\\source-snapshot", script, StringComparison.Ordinal);
        Assert.Contains("historical-input", script, StringComparison.Ordinal);
        Assert.Contains("stage-e5-setup-verification.json", script, StringComparison.Ordinal);
        Assert.Contains("Move-Item -LiteralPath $stagedActive -Destination $activeArtifact -Force", script, StringComparison.Ordinal);
        Assert.DoesNotContain("--game-data", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-WebRequest", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PoEnhance-E5-Shako", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RefreshScript_SupportsFreshCloneAndForwardsFixedTimestamp()
    {
        var script = ReadRepositoryFile("scripts", "Refresh-GameData.ps1");

        Assert.Contains("[string]$CreatedAtUtc", script, StringComparison.Ordinal);
        Assert.Contains("'--created-at-utc'", script, StringComparison.Ordinal);
        Assert.Contains("$activeExistedBefore", script, StringComparison.Ordinal);
        Assert.Contains("absolute path is serialized", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Active GameData artifact is missing", script, StringComparison.Ordinal);
        Assert.Contains("Activation: not performed.", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationProject_FailsExplicitlyWithoutGeneratedPackageAndCopiesItWhenPresent()
    {
        var project = ReadRepositoryFile("PoEnhance.App", "PoEnhance.App.csproj");

        Assert.Contains("EnsureGameDataPackageExists", project, StringComparison.Ordinal);
        Assert.Contains("BeforeTargets=\"PrepareForBuild\"", project, StringComparison.Ordinal);
        Assert.Contains("Setup-GameData.ps1", project, StringComparison.Ordinal);
        Assert.Contains("<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>", project, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Condition=\"Exists('..\\artifacts\\poenhance-game-data.json')\"",
            project,
            StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory.FullName, .. pathParts]));
    }
}
