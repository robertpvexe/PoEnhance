using System.Text;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class RePoeBaseItemImporterTests
{
    private readonly RePoeBaseItemImporter _importer = new();

    [Fact]
    public void Import_ReducedFixture_ImportsExpectedRecords()
    {
        var result = _importer.Import(RePoeImportTestFixtures.ReducedBaseItemsPath);

        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(6, result.SourceRecordsRead);
        Assert.Equal(6, result.RecordsImported);
        Assert.Equal(0, result.RecordsSkipped);
        Assert.Equal(ExpectedOrderedIds, result.ImportedRecords.Select(record => record.Id!).ToArray());
    }

    [Fact]
    public void Import_GoldRing_PreservesVerifiedSourceFields()
    {
        var result = _importer.Import(RePoeImportTestFixtures.ReducedBaseItemsPath);

        var goldRing = FindById(result, "Metadata/Items/Rings/Ring4");

        Assert.Equal("Metadata/Items/Rings/Ring4", goldRing.Id);
        Assert.Equal("Gold Ring", goldRing.Name);
        Assert.Equal("Ring", goldRing.ItemClass);
        Assert.Null(goldRing.RequiredLevel);
        Assert.Equal("item", goldRing.Domain);
        Assert.Equal(["default", "ring"], goldRing.Tags);
        AssertRePoeSource(goldRing, "Metadata/Items/Rings/Ring4");
    }

    [Fact]
    public void Import_GraniteFlask_PreservesClassAndNormalizesTags()
    {
        var result = _importer.Import(RePoeImportTestFixtures.ReducedBaseItemsPath);

        var graniteFlask = FindById(result, "Metadata/Items/Flasks/FlaskUtility5");

        Assert.Equal("Granite Flask", graniteFlask.Name);
        Assert.Equal("UtilityFlask", graniteFlask.ItemClass);
        Assert.Equal("flask", graniteFlask.Domain);
        Assert.Equal(["default", "flask", "not_for_sale", "utility_flask"], graniteFlask.Tags);
        AssertRePoeSource(graniteFlask, "Metadata/Items/Flasks/FlaskUtility5");
    }

    [Fact]
    public void Import_RepresentativeBases_PreservesClassesAndRequirements()
    {
        var result = _importer.Import(RePoeImportTestFixtures.ReducedBaseItemsPath);

        var armour = FindById(result, "Metadata/Items/Armours/BodyArmours/BodyStrDex10");
        Assert.Equal("Full Wyrmscale", armour.Name);
        Assert.Equal("Body Armour", armour.ItemClass);
        Assert.Equal(46, armour.RequiredLevel);

        var weapon = FindById(result, "Metadata/Items/Weapons/TwoHandWeapons/TwoHandAxes/TwoHandAxe17");
        Assert.Equal("Vaal Axe", weapon.Name);
        Assert.Equal("Two Hand Axe", weapon.ItemClass);
        Assert.Equal(64, weapon.RequiredLevel);
        var weaponProperties = Assert.IsType<ItemBaseWeaponProperties>(weapon.WeaponProperties);
        Assert.Equal(104, weaponProperties.PhysicalDamageMinimum);
        Assert.Equal(174, weaponProperties.PhysicalDamageMaximum);
        Assert.Equal(870, weaponProperties.AttackTimeMilliseconds);
        Assert.Equal(5m, weaponProperties.CriticalStrikeChancePercent);
        var weaponPropertySource = Assert.Single(weaponProperties.Sources);
        Assert.Equal("repoe", weaponPropertySource.SourceId);
        Assert.Equal(weapon.Id, weaponPropertySource.ExternalId);

        var jewel = FindById(result, "Metadata/Items/Jewels/JewelInt");
        Assert.Equal("Cobalt Jewel", jewel.Name);
        Assert.Equal("Jewel", jewel.ItemClass);
        Assert.Null(jewel.RequiredLevel);

        var currency = FindById(result, "Metadata/Items/AtlasExiles/AddModToRareCrusader");
        Assert.Equal("Crusader's Exalted Orb", currency.Name);
        Assert.Equal("StackableCurrency", currency.ItemClass);
        Assert.Null(currency.RequiredLevel);
    }

    [Fact]
    public void Import_SourceReferencesValidateAgainstRePoeManifest()
    {
        var result = _importer.Import(RePoeImportTestFixtures.ReducedBaseItemsPath);
        var manifest = RePoeImportTestFixtures.CreateManifestWithRePoeSource();
        var package = new GameDataPackage
        {
            Manifest = manifest,
            ItemBases = result.ImportedRecords,
            Modifiers = [],
        };

        var validationResult = GameDataPackageValidator.Validate(package);

        Assert.True(validationResult.IsValid);
        Assert.All(result.ImportedRecords, record =>
        {
            var source = Assert.Single(record.Sources);
            Assert.Equal("repoe", source.SourceId);
            Assert.Equal(record.Id, source.ExternalId);
            Assert.Null(source.ExternalUri);
        });
    }

    [Fact]
    public void Import_MalformedRecords_SkipsOnlyInvalidRecordsWithDiagnostics()
    {
        var json = """
            {
              "Metadata/Items/Valid/Good": {
                "name": "Good Base",
                "item_class": "Ring",
                "requirements": null,
                "tags": ["ring"]
              },
              "Metadata/Items/Invalid/MissingName": {
                "item_class": "Ring",
                "requirements": null,
                "tags": ["ring"]
              },
              "Metadata/Items/Invalid/MissingClass": {
                "name": "No Class",
                "requirements": null,
                "tags": ["ring"]
              },
              "Metadata/Items/Invalid/BadLevel": {
                "name": "Bad Level",
                "item_class": "Ring",
                "requirements": { "level": -1 },
                "tags": ["ring"]
              }
            }
            """;

        var result = ImportJson(json);

        Assert.False(result.HasErrors);
        Assert.Equal(4, result.SourceRecordsRead);
        Assert.Equal(1, result.RecordsImported);
        Assert.Equal(3, result.RecordsSkipped);
        Assert.Equal("Metadata/Items/Valid/Good", Assert.Single(result.ImportedRecords).Id);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.RecordMissingName);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.RecordMissingItemClass);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.RecordInvalidRequiredLevel);
    }

    [Fact]
    public void Import_MalformedJson_ReturnsClearError()
    {
        var result = ImportJson("{");

        Assert.True(result.HasErrors);
        Assert.Equal(0, result.SourceRecordsRead);
        Assert.Equal(0, result.RecordsImported);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.JsonMalformed, ImportDiagnosticSeverity.Error);
    }

    [Fact]
    public void Import_InvalidNumericalWeaponFacts_RemainsImportedWithExplicitlyAbsentFacts()
    {
        var json = """
            {
              "Metadata/Items/Test/BadWeapon": {
                "name": "Bad Weapon",
                "item_class": "One Hand Axe",
                "requirements": null,
                "tags": ["weapon"],
                "properties": {
                  "physical_damage_min": -1,
                  "physical_damage_max": "20",
                  "attack_time": 0,
                  "critical_strike_chance": -5
                }
              }
            }
            """;

        var result = ImportJson(json);

        Assert.False(result.HasErrors);
        var weapon = Assert.Single(result.ImportedRecords);
        Assert.Null(weapon.WeaponProperties);
        Assert.Equal(4, result.Diagnostics.Count(diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.RecordInvalidWeaponProperties));
    }

    [Fact]
    public void Import_DefensiveRangesAndBlock_PreserveExactValuesAndProvenance()
    {
        var json = """
            {
              "Metadata/Items/Test/Shield": {
                "name": "Test Shield",
                "item_class": "Shield",
                "requirements": null,
                "tags": ["armour"],
                "properties": {
                  "armour": { "min": 10, "max": 20 },
                  "evasion": { "min": 30, "max": 40 },
                  "energy_shield": { "min": 5, "max": 7 },
                  "ward": { "min": 8, "max": 9 },
                  "block": 24
                }
              }
            }
            """;

        var result = ImportJson(json);

        var properties = Assert.IsType<ItemBaseDefenceProperties>(Assert.Single(result.ImportedRecords).DefenceProperties);
        Assert.Equal((10, 20), (properties.ArmourMinimum, properties.ArmourMaximum));
        Assert.Equal((30, 40), (properties.EvasionRatingMinimum, properties.EvasionRatingMaximum));
        Assert.Equal((5, 7), (properties.EnergyShieldMinimum, properties.EnergyShieldMaximum));
        Assert.Equal((8, 9), (properties.WardMinimum, properties.WardMaximum));
        Assert.Equal(24, properties.ChanceToBlockPercent);
        var source = Assert.Single(properties.Sources);
        Assert.Equal("repoe", source.SourceId);
        Assert.Equal("Metadata/Items/Test/Shield", source.ExternalId);
    }

    [Fact]
    public void Import_MalformedDefensiveValues_AreAbsentWithFocusedDiagnostics()
    {
        var json = """
            {
              "Metadata/Items/Test/BadArmour": {
                "name": "Bad Armour",
                "item_class": "Body Armour",
                "requirements": null,
                "tags": ["armour"],
                "properties": {
                  "armour": { "min": 20, "max": 10 },
                  "evasion": "bad",
                  "energy_shield": { "min": -1, "max": 7 },
                  "ward": { "min": 8 },
                  "block": -1
                }
              }
            }
            """;

        var result = ImportJson(json);

        Assert.Null(Assert.Single(result.ImportedRecords).DefenceProperties);
        Assert.Equal(5, result.Diagnostics.Count(diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.RecordInvalidDefenceProperties));
    }

    [Fact]
    public void Import_UnsupportedRootShape_ReturnsSchemaUnsupported()
    {
        var result = ImportJson("[]");

        Assert.True(result.HasErrors);
        Assert.Equal(0, result.SourceRecordsRead);
        Assert.Equal(0, result.RecordsImported);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.SchemaUnsupported, ImportDiagnosticSeverity.Error);
    }

    [Fact]
    public void Import_UnknownOptionalFields_AreIgnoredSafely()
    {
        var json = """
            {
              "Metadata/Items/Test/UnknownField": {
                "name": "Unknown Field Base",
                "item_class": "Ring",
                "requirements": null,
                "tags": ["ring"],
                "future_optional_field": {
                  "nested": true
                }
              }
            }
            """;

        var result = ImportJson(json);

        Assert.False(result.HasErrors);
        var record = Assert.Single(result.ImportedRecords);
        Assert.Equal("Unknown Field Base", record.Name);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Import_NormalizesTagsAndOrdersRecordsDeterministically()
    {
        var json = """
            {
              "Metadata/Items/Test/Z": {
                "name": "Z Base",
                "item_class": "Ring",
                "requirements": null,
                "tags": [" z ", "", "A", "a", "b"]
              },
              "Metadata/Items/Test/A": {
                "name": "A Base",
                "item_class": "Ring",
                "requirements": null,
                "tags": ["ring"]
              }
            }
            """;

        var result = ImportJson(json);

        Assert.False(result.HasErrors);
        Assert.Equal(["Metadata/Items/Test/A", "Metadata/Items/Test/Z"], result.ImportedRecords.Select(record => record.Id!).ToArray());

        var zBase = FindById(result, "Metadata/Items/Test/Z");
        Assert.Equal(["A", "b", "z"], zBase.Tags);
    }

    [Fact]
    public void Import_NonArrayTags_ImportsRecordWithWarningAndEmptyTags()
    {
        var json = """
            {
              "Metadata/Items/Test/BadTags": {
                "name": "Bad Tags",
                "item_class": "Ring",
                "requirements": null,
                "tags": "ring"
              }
            }
            """;

        var result = ImportJson(json);

        Assert.False(result.HasErrors);
        var record = Assert.Single(result.ImportedRecords);
        Assert.Empty(record.Tags);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.RecordInvalidTags, ImportDiagnosticSeverity.Warning);
    }

    private ImportResult<ItemBaseRecord> ImportJson(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return _importer.Import(stream);
    }

    private static ItemBaseRecord FindById(ImportResult<ItemBaseRecord> result, string id)
    {
        return result.ImportedRecords.Single(record => record.Id == id);
    }

    private static void AssertRePoeSource(ItemBaseRecord record, string externalId)
    {
        var source = Assert.Single(record.Sources);
        Assert.Equal("repoe", source.SourceId);
        Assert.Equal(externalId, source.ExternalId);
        Assert.Null(source.ExternalUri);
    }

    private static void AssertHasDiagnostic(
        ImportResult<ItemBaseRecord> result,
        string code,
        ImportDiagnosticSeverity? severity = null)
    {
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == code &&
            (!severity.HasValue || diagnostic.Severity == severity.Value));
    }

    private static readonly string[] ExpectedOrderedIds =
    [
        "Metadata/Items/Armours/BodyArmours/BodyStrDex10",
        "Metadata/Items/AtlasExiles/AddModToRareCrusader",
        "Metadata/Items/Flasks/FlaskUtility5",
        "Metadata/Items/Jewels/JewelInt",
        "Metadata/Items/Rings/Ring4",
        "Metadata/Items/Weapons/TwoHandWeapons/TwoHandAxes/TwoHandAxe17",
    ];
}
