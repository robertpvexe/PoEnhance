using System.Security.Cryptography;
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

        // Package identity is authoritative in sources.json; assert structural completeness
        // and that the on-disk active artifact matches the pin (do not hardcode a historic dataVersion).
        Assert.Equal(4, package.GetProperty("schemaVersion").GetInt32());
        var dataVersion = package.GetProperty("dataVersion").GetString();
        Assert.False(string.IsNullOrWhiteSpace(dataVersion));
        Assert.False(string.IsNullOrWhiteSpace(package.GetProperty("createdAtUtc").GetString()));
        var sizeBytes = package.GetProperty("sizeBytes").GetInt64();
        Assert.True(sizeBytes > 0);
        var sha256 = package.GetProperty("sha256").GetString();
        Assert.False(string.IsNullOrWhiteSpace(sha256));
        Assert.Equal(353, package.GetProperty("foulbornRelationshipCount").GetInt32());
        Assert.Equal(353, package.GetProperty("exactFoulbornRelationshipCount").GetInt32());
        Assert.Equal(0, package.GetProperty("unsupportedFoulbornRelationshipCount").GetInt32());

        var activePath = ResolveRepositoryPath("artifacts", "poenhance-game-data.json");
        Assert.True(File.Exists(activePath), $"Active GameData package missing: {activePath}");
        Assert.Equal(sizeBytes, new FileInfo(activePath).Length);
        Assert.Equal(sha256, ComputeSha256Hex(activePath), ignoreCase: true);

        var currentRePoe = root.GetProperty("currentRePoe");
        Assert.False(string.IsNullOrWhiteSpace(currentRePoe.GetProperty("commitSha").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(currentRePoe.GetProperty("hostedExportCommitSha").GetString()));
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

    private static string ReadRepositoryFile(params string[] pathParts) =>
        File.ReadAllText(ResolveRepositoryPath(pathParts));

    private static string ResolveRepositoryPath(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. pathParts]);
    }

    private static string ComputeSha256Hex(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
