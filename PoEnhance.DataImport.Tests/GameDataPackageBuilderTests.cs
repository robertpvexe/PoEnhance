using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class GameDataPackageBuilderTests
{
    private readonly GameDataPackageBuilder _builder = new();
    private readonly RePoeBaseItemImporter _importer = new();
    private readonly RePoeStatsImporter _statsImporter = new();
    private readonly RePoeStatTranslationsImporter _translationsImporter = new();

    [Fact]
    public void CreatePackage_WithImportedBasesAndRePoeManifest_ReturnsValidPackage()
    {
        var importResult = _importer.Import(RePoeImportTestFixtures.ReducedBaseItemsPath);

        var result = _builder.CreatePackage(
            RePoeImportTestFixtures.CreateManifestWithRePoeSource(),
            importResult.ImportedRecords);

        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Package);
        Assert.Equal(6, result.Package.ItemBases.Count);
        Assert.Empty(result.Package.Modifiers);
        Assert.True(GameDataPackageValidator.Validate(result.Package).IsValid);
    }

    [Fact]
    public void CreatePackage_ManifestWithoutRePoeSource_ReturnsDiagnostic()
    {
        var importResult = _importer.Import(RePoeImportTestFixtures.ReducedBaseItemsPath);
        var manifest = RePoeImportTestFixtures.CreateManifestWithRePoeSource() with
        {
            Sources =
            [
                new GameDataPackageSource
                {
                    SourceId = "poedb",
                    RetrievedAtUtc = new DateTimeOffset(2026, 7, 9, 12, 5, 0, TimeSpan.Zero),
                },
            ],
        };

        var result = _builder.CreatePackage(manifest, importResult.ImportedRecords);

        Assert.True(result.HasErrors);
        Assert.Null(result.Package);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.PackageRePoeSourceMissing &&
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
    }

    [Fact]
    public void CreatePackage_JsonRoundTripPreservesImportedRecordsAndSourceMetadata()
    {
        var importResult = _importer.Import(RePoeImportTestFixtures.ReducedBaseItemsPath);
        var createResult = _builder.CreatePackage(
            RePoeImportTestFixtures.CreateManifestWithRePoeSource(),
            importResult.ImportedRecords);

        Assert.NotNull(createResult.Package);
        var json = GameDataPackageJson.Serialize(createResult.Package);
        var roundTrippedPackage = GameDataPackageJson.Deserialize(json);

        Assert.NotNull(roundTrippedPackage);
        Assert.True(GameDataPackageValidator.Validate(roundTrippedPackage).IsValid);
        Assert.Equal(createResult.Package.ItemBases.Count, roundTrippedPackage.ItemBases.Count);

        var goldRing = roundTrippedPackage.ItemBases.Single(record => record.Id == "Metadata/Items/Rings/Ring4");
        Assert.Equal("Gold Ring", goldRing.Name);
        Assert.Equal("Ring", goldRing.ItemClass);
        Assert.Equal(["default", "ring"], goldRing.Tags);

        var source = Assert.Single(goldRing.Sources);
        Assert.Equal("repoe", source.SourceId);
        Assert.Equal("Metadata/Items/Rings/Ring4", source.ExternalId);
        Assert.Null(source.ExternalUri);
    }

    [Fact]
    public void CreatePackage_WithImportedStatsAndTranslations_ReturnsValidCompletePackage()
    {
        var baseItems = _importer.Import(RePoeImportTestFixtures.ReducedBaseItemsPath).ImportedRecords;
        var stats = _statsImporter.Import(RePoeImportTestFixtures.ReducedStatsPath).ImportedRecords;
        var translations = _translationsImporter
            .Import(RePoeImportTestFixtures.ReducedStatTranslationsPath, stats)
            .ImportedRecords;
        var modifiers = new[]
        {
            new ModifierDefinition
            {
                Id = "mod.test.maximum-life",
                GroupId = "mod-group.test.maximum-life",
                Name = "Test Life",
                GenerationType = ModifierGenerationType.Prefix,
                Tier = 1,
                Stats =
                [
                    new ModifierStat
                    {
                        Index = 0,
                        StatId = "base_maximum_life",
                        MinValue = 10m,
                        MaxValue = 20m,
                    },
                ],
                Sources =
                [
                    new GameDataSourceReference
                    {
                        SourceId = "repoe",
                        ExternalId = "TestLife",
                    },
                ],
            },
        };

        var result = _builder.CreatePackage(
            RePoeImportTestFixtures.CreateManifestWithRePoeSource(),
            baseItems,
            modifiers,
            stats,
            translations);

        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Package);
        Assert.Equal(6, result.Package.ItemBases.Count);
        Assert.Single(result.Package.Modifiers);
        Assert.Equal(19, result.Package.Stats.Count);
        Assert.Equal(6, result.Package.StatTranslations.Count);
        Assert.True(GameDataPackageValidator.Validate(result.Package).IsValid);
    }

    [Fact]
    public void CreatePackage_MissingModifierStatReference_ReturnsDiagnosticAndNoPackage()
    {
        var stats = _statsImporter.Import(RePoeImportTestFixtures.ReducedStatsPath).ImportedRecords;
        var modifiers = new[]
        {
            new ModifierDefinition
            {
                Id = "mod.test.missing-stat",
                GroupId = "mod-group.test.missing-stat",
                GenerationType = ModifierGenerationType.Prefix,
                Tier = 1,
                Stats =
                [
                    new ModifierStat
                    {
                        Index = 0,
                        StatId = "missing_stat_id",
                    },
                ],
            },
        };

        var result = _builder.CreatePackage(
            RePoeImportTestFixtures.CreateManifestWithRePoeSource(),
            itemBases: [],
            modifiers,
            stats,
            statTranslations: []);

        Assert.True(result.HasErrors);
        Assert.Null(result.Package);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.PackageModifierStatReferenceMissing &&
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
    }

}
