using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class UniqueBlockIdentityCollisionAuditorTests
{
    [Fact]
    public void Audit_ActivePackage_LegacyIdentityLens_ReportsDistinctNumericDomainCollisions()
    {
        var catalog = LoadActiveCatalog();
        var audit = UniqueBlockIdentityCollisionAuditor.Audit(catalog, useLegacyIdentity: true);

        Assert.True(audit.LegacyCollisionGroups > 0);
        Assert.True(audit.LegacyClassificationCounts.GetValueOrDefault(
            UniqueBlockIdentityCollisionAuditor.CollisionClass.DistinctNumericValueDomain) > 0);

        var structuralDomainCollisions = audit.LegacyGroups
            .Where(group => group.Classification ==
                UniqueBlockIdentityCollisionAuditor.CollisionClass.DistinctNumericValueDomain &&
                group.Blocks.Select(block => string.Join('\n', block.CanonicalSignatures))
                    .Distinct(StringComparer.Ordinal)
                    .Count() == 1 &&
                group.Blocks.Select(block =>
                        PoBUniqueCatalogImporter.ExtractSourceValueDomainKey(block.Lines))
                    .Distinct(StringComparer.Ordinal)
                    .Count() > 1)
            .ToArray();
        Assert.NotEmpty(structuralDomainCollisions);
        Assert.Contains(structuralDomainCollisions, group => group.Blocks.Count >= 2);
    }

    [Fact]
    public void Audit_ActivePackage_CurrentIdentityLens_KeepsDistinctDomainsAndLegitimateMerges()
    {
        var catalog = LoadActiveCatalog();
        var legacy = UniqueBlockIdentityCollisionAuditor.Audit(catalog, useLegacyIdentity: true);
        var current = UniqueBlockIdentityCollisionAuditor.Audit(catalog, useLegacyIdentity: false);

        Assert.True(legacy.LegacyCollisionGroups > current.NewCollisionGroups);
        Assert.Equal(0, current.NewClassificationCounts.GetValueOrDefault(
            UniqueBlockIdentityCollisionAuditor.CollisionClass.DistinctNumericValueDomain));
        Assert.True(current.NewClassificationCounts.GetValueOrDefault(
            UniqueBlockIdentityCollisionAuditor.CollisionClass.LegitimateEquivalentObservation) > 0);

        Assert.Contains(
            current.NewGroups,
            group => group.Classification ==
                UniqueBlockIdentityCollisionAuditor.CollisionClass.LegitimateEquivalentObservation &&
                group.Blocks
                    .SelectMany(block => block.SourceObservationIds)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count() > 1);
    }

    private static UniqueItemCatalog LoadActiveCatalog()
    {
        var package = Assert.IsType<GameDataPackage>(
            GameDataPackageJson.Deserialize(File.ReadAllText(GetActivePackagePath())));
        return package.UniqueItems!;
    }

    private static string GetActivePackagePath()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "artifacts",
            "poenhance-game-data.json"));
        if (!File.Exists(path))
        {
            path = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "artifacts",
                "poenhance-game-data.json"));
        }

        Assert.True(File.Exists(path), $"Active GameData package not found: {path}");
        return path;
    }
}
