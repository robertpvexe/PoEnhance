using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class GameDataPackageBuilderTests
{
    private readonly GameDataPackageBuilder _builder = new();
    private readonly RePoeBaseItemImporter _importer = new();

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

}
