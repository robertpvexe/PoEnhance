namespace PoEnhance.DataImport.Tests;

public sealed class UniqueRepresentationStructuralAuditTests
{
    [Fact]
    public async Task Audit_ActivePackage_WritesStructuralMetrics()
    {
        var packagePath = Environment.GetEnvironmentVariable("POENHANCE_GAMEDATA_AUDIT_PATH")
            ?? Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), $"Package not found: {packagePath}");

        var metrics = await UniqueRepresentationStructuralAudit.AnalyzePackageAsync(packagePath);
        var outputPath = Environment.GetEnvironmentVariable("POENHANCE_GAMEDATA_AUDIT_OUTPUT")
            ?? Path.Combine(Path.GetTempPath(), "PoEnhance-UniqueRepresentationAudit.json");
        await File.WriteAllTextAsync(
            outputPath,
            UniqueRepresentationStructuralAudit.Serialize(metrics));
        Assert.True(metrics.NewCollisionGroups >= 0);
    }
}
