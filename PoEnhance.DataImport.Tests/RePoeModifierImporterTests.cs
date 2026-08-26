using System.Diagnostics;
using System.Text;
using System.Text.Json;
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
        Assert.Equal("prefix", life.SourceGenerationType);
        Assert.False(life.IsEssenceOnly);
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
        Assert.Equal("unique", implicitModifier.SourceGenerationType);
        Assert.Equal("base_item_found_rarity_+%", Assert.Single(implicitModifier.Stats).StatId);
    }

    [Fact]
    public void Import_UniqueSourceText_PreservesExactTransientCompositionEvidence()
    {
        var result = ImportJson("""
            {
              "CompoundUnique": {
                "domain": "item",
                "generation_type": "unique",
                "groups": ["CompoundUnique"],
                "text": "+(30-50) to maximum Energy Shield\n(10-15)% increased Stun and Block Recovery",
                "stats": [
                  { "id": "local_energy_shield", "min": 30, "max": 50 },
                  { "id": "base_stun_recovery_+%", "min": 10, "max": 15 }
                ]
              }
            }
            """);

        Assert.Equal(
            "+(30-50) to maximum Energy Shield\n(10-15)% increased Stun and Block Recovery",
            Assert.Single(result.ImportedRecords).SourceText);
    }

    [Theory]
    [InlineData("exarch_implicit")]
    [InlineData("searing_exarch_implicit")]
    [InlineData("eater_implicit")]
    [InlineData("eater_of_worlds_implicit")]
    public void Import_EldritchGeneration_MapsToImplicit(string generationType)
    {
        var result = ImportJson($$"""
            {
              "EldritchImplicit": {
                "domain": "item",
                "generation_type": "{{generationType}}",
                "groups": ["EldritchImplicit"],
                "stats": [
                  {
                    "id": "test_stat",
                    "min": 1,
                    "max": 2
                  }
                ]
              }
            }
            """);

        var modifier = Assert.Single(result.ImportedRecords);
        Assert.Equal(ModifierGenerationType.Implicit, modifier.GenerationType);
        Assert.Equal(generationType, modifier.SourceGenerationType);
    }

    [Fact]
    public void Import_CorruptedGeneration_PreservesProvenanceOrderAndPotentialAvailability()
    {
        var result = ImportJson(ModifierJson(
            "CorruptedImplicit",
            "corrupted",
            """
                "spawn_weights": [
                  { "tag": "graft", "weight": 80 },
                  { "tag": "default", "weight": 0 }
                ],
            """));

        var modifier = Assert.Single(result.ImportedRecords);
        Assert.Equal(ModifierGenerationType.Corrupted, modifier.GenerationType);
        Assert.Equal("corrupted", modifier.SourceGenerationType);
        Assert.Equal(ModifierSourceAvailability.PotentiallyEligible, modifier.SourceAvailability);
        Assert.Equal(["graft", "default"], modifier.SpawnWeights.Select(weight => weight.Tag));
        Assert.Equal([80, 0], modifier.SpawnWeights.Select(weight => weight.Weight));
        AssertRePoeSource(modifier, "CorruptedImplicit");
    }

    [Fact]
    public void Import_AllZeroSpawnWeights_ProducesDisabledAvailability()
    {
        var result = ImportJson(ModifierJson(
            "DisabledMod",
            "prefix",
            """
                "spawn_weights": [
                  { "tag": "ring", "weight": 0 },
                  { "tag": "default", "weight": 0 }
                ],
            """));

        var modifier = Assert.Single(result.ImportedRecords);
        Assert.Equal(ModifierSourceAvailability.Disabled, modifier.SourceAvailability);
    }

    [Fact]
    public void Import_MissingSpawnWeights_ProducesUnknownAvailability()
    {
        var result = ImportJson(ModifierJson("UnknownAvailabilityMod", "suffix"));

        var modifier = Assert.Single(result.ImportedRecords);
        Assert.Equal(ModifierSourceAvailability.Unknown, modifier.SourceAvailability);
        Assert.Empty(modifier.SpawnWeights);
        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => diagnostic.Code == RePoeImportDiagnosticCodes.ModifierRecordAvailabilityUnknown);
    }

    [Fact]
    public void Import_MalformedSpawnWeights_PreservesUsableOrderButFailsClosedToUnknown()
    {
        var result = ImportJson(ModifierJson(
            "MalformedWeightsMod",
            "prefix",
            """
                "spawn_weights": [
                  { "tag": "ring", "weight": 0 },
                  { "tag": 42, "weight": 0 },
                  { "tag": "default", "weight": 0 }
                ],
            """));

        var modifier = Assert.Single(result.ImportedRecords);
        Assert.Equal(ModifierSourceAvailability.Unknown, modifier.SourceAvailability);
        Assert.Equal(["ring", "default"], modifier.SpawnWeights.Select(weight => weight.Tag));
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.ModifierRecordInvalidSpawnWeight);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.ModifierRecordAvailabilityUnknown);
    }

    [Theory]
    [InlineData("prefix", ModifierGenerationType.Prefix)]
    [InlineData("suffix", ModifierGenerationType.Suffix)]
    [InlineData("enchantment", ModifierGenerationType.Enchantment)]
    [InlineData("unique", ModifierGenerationType.Implicit)]
    [InlineData("exarch_implicit", ModifierGenerationType.Implicit)]
    [InlineData("searing_exarch_implicit", ModifierGenerationType.Implicit)]
    [InlineData("eater_implicit", ModifierGenerationType.Implicit)]
    [InlineData("eater_of_worlds_implicit", ModifierGenerationType.Implicit)]
    [InlineData("fractured", ModifierGenerationType.Unknown)]
    public void Import_NonCorruptedGenerationMappings_RemainUnchanged(
        string sourceGenerationType,
        ModifierGenerationType expectedGenerationType)
    {
        var result = ImportJson(ModifierJson("MappedMod", sourceGenerationType));

        var modifier = Assert.Single(result.ImportedRecords);
        Assert.Equal(expectedGenerationType, modifier.GenerationType);
        Assert.Equal(sourceGenerationType, modifier.SourceGenerationType);
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

    [Fact]
    public void Import_AuditedCurrentSource_ProducesExpectedCorruptedEvidence()
    {
        var auditRepository = Path.Combine(
            Path.GetTempPath(),
            "PoEnhance-CurrentLeague-DataSourceAudit",
            "external",
            "repoe-latest",
            "RePoE");
        var modsPath = Path.Combine(auditRepository, "data", "mods.json");
        if (!File.Exists(modsPath))
        {
            return;
        }

        Assert.Equal(
            "34a9bd548eba7c3b62ab1d1f19a99ae8b12f1564",
            RunGit(auditRepository, "rev-parse HEAD"));

        var result = _importer.Import(modsPath);
        using var rawDocument = JsonDocument.Parse(File.ReadAllBytes(modsPath));
        var rawCorruptedRecords = rawDocument.RootElement
            .EnumerateObject()
            .Where(property =>
                property.Value.TryGetProperty("generation_type", out var generationType) &&
                generationType.ValueKind == JsonValueKind.String &&
                generationType.GetString() == "corrupted")
            .ToArray();
        var importedCorrupted = result.ImportedRecords
            .Where(modifier => modifier.SourceGenerationType == "corrupted")
            .ToArray();
        var importedById = importedCorrupted.ToDictionary(modifier => modifier.Id!, StringComparer.Ordinal);

        Assert.Equal(521, rawCorruptedRecords.Length);
        Assert.Equal(521, importedCorrupted.Length);
        Assert.Equal(521, importedCorrupted.Count(modifier =>
            modifier.GenerationType == ModifierGenerationType.Corrupted));
        Assert.DoesNotContain(
            importedCorrupted,
            modifier => modifier.GenerationType == ModifierGenerationType.Unknown);
        Assert.Equal(348, importedCorrupted.Count(modifier =>
            modifier.SourceAvailability == ModifierSourceAvailability.PotentiallyEligible));
        Assert.Equal(173, importedCorrupted.Count(modifier =>
            modifier.SourceAvailability == ModifierSourceAvailability.Disabled));
        Assert.DoesNotContain(
            importedCorrupted,
            modifier => modifier.SourceAvailability == ModifierSourceAvailability.Unknown);

        foreach (var rawRecord in rawCorruptedRecords)
        {
            var imported = importedById[rawRecord.Name];
            AssertRePoeSource(imported, rawRecord.Name);
            var rawWeights = rawRecord.Value.GetProperty("spawn_weights")
                .EnumerateArray()
                .Select(weight => (
                    Tag: weight.GetProperty("tag").GetString(),
                    Weight: weight.GetProperty("weight").GetInt32()))
                .ToArray();
            Assert.Equal(
                rawWeights,
                imported.SpawnWeights.Select(weight => (weight.Tag, weight.Weight)).ToArray());
        }
    }

    private ImportResult<ModifierDefinition> ImportJson(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return _importer.Import(stream);
    }

    private static string ModifierJson(
        string id,
        string generationType,
        string additionalProperties = "")
    {
        return $$"""
            {
              "{{id}}": {
                "domain": "item",
                "generation_type": "{{generationType}}",
                "groups": ["TestGroup"],
            {{additionalProperties}}
                "stats": [
                  {
                    "id": "test_stat",
                    "min": 1,
                    "max": 2
                  }
                ]
              }
            }
            """;
    }

    private static string RunGit(string workingDirectory, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"-C \"{workingDirectory}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        Assert.NotNull(process);

        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {arguments} failed: {error}");
        return output;
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
