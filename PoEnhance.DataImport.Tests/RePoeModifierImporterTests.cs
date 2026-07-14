using System.Text;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class RePoeModifierImporterTests
{
    private readonly RePoeModifierImporter _importer = new();

    [Fact]
    public void Import_ReducedFixture_ImportsExpectedModifiersDeterministically()
    {
        var result = _importer.Import(RePoeImportTestFixtures.ReducedModsPath);

        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(4, result.SourceRecordsRead);
        Assert.Equal(4, result.RecordsImported);
        Assert.Equal(0, result.RecordsSkipped);
        Assert.Equal(
            result.ImportedRecords.OrderBy(modifier => modifier.Id, StringComparer.Ordinal).Select(modifier => modifier.Id),
            result.ImportedRecords.Select(modifier => modifier.Id));
    }

    [Fact]
    public void Import_ModifierFields_PreservesProviderNeutralShape()
    {
        var result = _importer.Import(RePoeImportTestFixtures.ReducedModsPath);

        var life = result.ImportedRecords.Single(modifier => modifier.Id == "AbyssJewelAddedLife1");

        Assert.Equal("AbyssJewelLife", life.GroupId);
        Assert.Equal("Hale", life.Name);
        Assert.Equal(ModifierGenerationType.Prefix, life.GenerationType);
        Assert.Equal(1, life.RequiredLevel);
        Assert.Equal("abyss_jewel", life.Domain);
        Assert.Equal(["life", "resource"], life.Tags);
        Assert.Collection(
            life.Stats,
            stat =>
            {
                Assert.Equal(0, stat.Index);
                Assert.Equal("base_maximum_life", stat.StatId);
                Assert.Equal(21m, stat.MinValue);
                Assert.Equal(25m, stat.MaxValue);
            });
        Assert.Collection(
            life.SpawnWeights,
            weight =>
            {
                Assert.Equal("default", weight.Tag);
                Assert.Equal(3000, weight.Weight);
            });
        AssertRePoeSource(life, "AbyssJewelAddedLife1");
    }

    [Fact]
    public void Import_UniqueGeneration_MapsToImplicit()
    {
        var result = _importer.Import(RePoeImportTestFixtures.ReducedModsPath);

        var implicitModifier = result.ImportedRecords.Single(modifier =>
            modifier.Id == "ItemFoundRarityIncreaseImplicitRing1");

        Assert.Equal(ModifierGenerationType.Implicit, implicitModifier.GenerationType);
        Assert.Equal("base_item_found_rarity_+%", Assert.Single(implicitModifier.Stats).StatId);
    }

    [Fact]
    public void Import_MalformedRecords_SkipsInvalidRecordsWithDiagnostics()
    {
        var json = """
            {
              "ValidMod": {
                "domain": "item",
                "generation_type": "prefix",
                "groups": ["ValidGroup"],
                "stats": [
                  {
                    "id": "base_maximum_life",
                    "min": 1,
                    "max": 2
                  }
                ]
              },
              "MissingGroup": {
                "domain": "item",
                "generation_type": "prefix",
                "stats": [
                  {
                    "id": "base_maximum_life",
                    "min": 1,
                    "max": 2
                  }
                ]
              },
              "MissingStats": {
                "domain": "item",
                "generation_type": "prefix",
                "groups": ["MissingStatsGroup"],
                "stats": []
              },
              "InvalidStat": {
                "domain": "item",
                "generation_type": "prefix",
                "groups": ["InvalidStatGroup"],
                "stats": [
                  {
                    "id": "base_maximum_life",
                    "min": 5,
                    "max": 1
                  }
                ]
              }
            }
            """;

        var result = ImportJson(json);

        Assert.False(result.HasErrors);
        Assert.Equal(4, result.SourceRecordsRead);
        Assert.Equal(1, result.RecordsImported);
        Assert.Equal(3, result.RecordsSkipped);
        Assert.Equal("ValidMod", Assert.Single(result.ImportedRecords).Id);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.ModifierRecordMissingGroup);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.ModifierRecordMissingStats);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.ModifierRecordInvalidStat);
    }

    [Fact]
    public void Import_MalformedJson_ReturnsClearError()
    {
        var result = ImportJson("{");

        Assert.True(result.HasErrors);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.JsonMalformed, ImportDiagnosticSeverity.Error);
    }

    [Fact]
    public void Import_UnsupportedRootShape_ReturnsSchemaUnsupported()
    {
        var result = ImportJson("[]");

        Assert.True(result.HasErrors);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.SchemaUnsupported, ImportDiagnosticSeverity.Error);
    }

    [Fact]
    public void Import_PreservesSpawnWeightSourceOrder()
    {
        var json = """
            {
              "OrderedMod": {
                "domain": "item",
                "generation_type": "prefix",
                "groups": ["OrderedGroup"],
                "stats": [
                  {
                    "id": "base_maximum_life",
                    "min": 1,
                    "max": 2
                  }
                ],
                "spawn_weights": [
                  { "tag": "ring", "weight": 0 },
                  { "tag": "default", "weight": 1000 }
                ]
              }
            }
            """;

        var result = ImportJson(json);
        var modifier = Assert.Single(result.ImportedRecords);

        Assert.Equal(["ring", "default"], modifier.SpawnWeights.Select(weight => weight.Tag));
    }

    private ImportResult<ModifierDefinition> ImportJson(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return _importer.Import(stream);
    }

    private static void AssertRePoeSource(ModifierDefinition record, string externalId)
    {
        var source = Assert.Single(record.Sources);
        Assert.Equal("repoe", source.SourceId);
        Assert.Equal(externalId, source.ExternalId);
        Assert.Null(source.ExternalUri);
    }

    private static void AssertHasDiagnostic(
        ImportResult<ModifierDefinition> result,
        string code,
        ImportDiagnosticSeverity? severity = null)
    {
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == code &&
            (!severity.HasValue || diagnostic.Severity == severity.Value));
    }
}
