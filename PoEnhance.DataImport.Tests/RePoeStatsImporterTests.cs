using System.Text;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class RePoeStatsImporterTests
{
    private readonly RePoeStatsImporter _importer = new();

    [Fact]
    public void Import_ReducedFixture_ImportsExpectedStatsDeterministically()
    {
        var result = _importer.Import(RePoeImportTestFixtures.ReducedStatsPath);

        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(55, result.SourceRecordsRead);
        Assert.Equal(55, result.RecordsImported);
        Assert.Equal(0, result.RecordsSkipped);
        Assert.Equal(
            result.ImportedRecords.OrderBy(stat => stat.Id, StringComparer.Ordinal).Select(stat => stat.Id),
            result.ImportedRecords.Select(stat => stat.Id));
    }

    [Fact]
    public void Import_LocalAndAliasedStats_PreservesNeutralFields()
    {
        var result = _importer.Import(RePoeImportTestFixtures.ReducedStatsPath);

        var localAccuracy = result.ImportedRecords.Single(stat => stat.Id == "local_accuracy_rating");

        Assert.True(localAccuracy.IsLocal);
        Assert.Equal("main_hand_local_accuracy_rating", localAccuracy.MainHandAliasId);
        Assert.Equal("off_hand_local_accuracy_rating", localAccuracy.OffHandAliasId);
        AssertRePoeSource(localAccuracy, "local_accuracy_rating");

        var localAttackSpeed = result.ImportedRecords.Single(stat => stat.Id == "local_attack_speed_+%");
        Assert.True(localAttackSpeed.IsLocal);
        Assert.Equal("main_hand_local_attack_speed_+%", localAttackSpeed.MainHandAliasId);
        Assert.Equal("off_hand_local_attack_speed_+%", localAttackSpeed.OffHandAliasId);

        var localFlatCrit = result.ImportedRecords.Single(stat => stat.Id == "local_critical_strike_chance");
        Assert.True(localFlatCrit.IsLocal);
        Assert.Equal("main_hand_local_critical_strike_chance", localFlatCrit.MainHandAliasId);
        Assert.Equal("off_hand_local_critical_strike_chance", localFlatCrit.OffHandAliasId);

        var baseLife = result.ImportedRecords.Single(stat => stat.Id == "base_maximum_life");
        Assert.False(baseLife.IsLocal);
        Assert.Null(baseLife.MainHandAliasId);
        Assert.Null(baseLife.OffHandAliasId);
    }

    [Fact]
    public void Import_ExplicitNullAliasHandConditions_TreatsAsNoAlias()
    {
        var json = """
            {
              "active_fork_null_alias_stat": {
                "alias": {
                  "when_in_main_hand": null,
                  "when_in_off_hand": null
                },
                "is_local": true
              }
            }
            """;

        var result = ImportJson(json);
        var stat = Assert.Single(result.ImportedRecords);

        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(1, result.SourceRecordsRead);
        Assert.Equal(1, result.RecordsImported);
        Assert.Equal(0, result.RecordsSkipped);
        Assert.Equal("active_fork_null_alias_stat", stat.Id);
        Assert.True(stat.IsLocal);
        Assert.Null(stat.MainHandAliasId);
        Assert.Null(stat.OffHandAliasId);
        AssertRePoeSource(stat, "active_fork_null_alias_stat");
    }

    [Fact]
    public void Import_NonNullAliasHandConditions_PreservesAliases()
    {
        var json = """
            {
              "active_fork_string_alias_stat": {
                "alias": {
                  "when_in_main_hand": " main_hand_active_fork_string_alias_stat ",
                  "when_in_off_hand": " off_hand_active_fork_string_alias_stat "
                },
                "is_local": false
              }
            }
            """;

        var result = ImportJson(json);
        var stat = Assert.Single(result.ImportedRecords);

        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("main_hand_active_fork_string_alias_stat", stat.MainHandAliasId);
        Assert.Equal("off_hand_active_fork_string_alias_stat", stat.OffHandAliasId);
        Assert.False(stat.IsLocal);
    }

    [Fact]
    public void Import_OldEmptyAliasObjectFixtureShape_RemainsCompatible()
    {
        var json = """
            {
              "old_empty_alias_stat": {
                "alias": {},
                "is_local": false
              }
            }
            """;

        var result = ImportJson(json);
        var stat = Assert.Single(result.ImportedRecords);

        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("old_empty_alias_stat", stat.Id);
        Assert.Null(stat.MainHandAliasId);
        Assert.Null(stat.OffHandAliasId);
    }

    [Fact]
    public void Import_SourceReferencesValidateAgainstRePoeManifest()
    {
        var result = _importer.Import(RePoeImportTestFixtures.ReducedStatsPath);
        var package = new GameDataPackage
        {
            Manifest = RePoeImportTestFixtures.CreateManifestWithRePoeSource(),
            Stats = result.ImportedRecords,
        };

        var validationResult = GameDataPackageValidator.Validate(package);

        Assert.True(validationResult.IsValid);
        Assert.All(result.ImportedRecords, stat =>
        {
            var source = Assert.Single(stat.Sources);
            Assert.Equal("repoe", source.SourceId);
            Assert.Equal(stat.Id, source.ExternalId);
            Assert.Null(source.ExternalUri);
        });
    }

    [Fact]
    public void Import_MalformedRecords_SkipsOnlyInvalidRecordsWithDiagnostics()
    {
        var json = """
            {
              "valid_stat": {
                "alias": {},
                "is_local": false
              },
              "missing_is_local": {
                "alias": {}
              },
              "invalid_alias": {
                "alias": {
                  "when_in_main_hand": 123
                },
                "is_local": true
              }
            }
            """;

        var result = ImportJson(json);

        Assert.False(result.HasErrors);
        Assert.Equal(3, result.SourceRecordsRead);
        Assert.Equal(1, result.RecordsImported);
        Assert.Equal(2, result.RecordsSkipped);
        Assert.Equal("valid_stat", Assert.Single(result.ImportedRecords).Id);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.StatRecordMissingIsLocal);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.StatRecordInvalidAlias);
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

    private ImportResult<StatDefinition> ImportJson(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return _importer.Import(stream);
    }

    private static void AssertRePoeSource(StatDefinition record, string externalId)
    {
        var source = Assert.Single(record.Sources);
        Assert.Equal("repoe", source.SourceId);
        Assert.Equal(externalId, source.ExternalId);
        Assert.Null(source.ExternalUri);
    }

    private static void AssertHasDiagnostic(
        ImportResult<StatDefinition> result,
        string code,
        ImportDiagnosticSeverity? severity = null)
    {
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == code &&
            (!severity.HasValue || diagnostic.Severity == severity.Value));
    }
}
