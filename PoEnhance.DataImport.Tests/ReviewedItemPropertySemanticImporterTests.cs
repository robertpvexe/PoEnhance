using System.Text;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class ReviewedItemPropertySemanticImporterTests
{
    private static readonly IReadOnlyList<IReadOnlyList<string>> ExpectedVectors =
    [
        ["local_physical_damage_+%"],
        ["local_minimum_added_physical_damage", "local_maximum_added_physical_damage"],
        ["local_minimum_added_fire_damage", "local_maximum_added_fire_damage"],
        ["local_minimum_added_cold_damage", "local_maximum_added_cold_damage"],
        ["local_minimum_added_lightning_damage", "local_maximum_added_lightning_damage"],
        ["local_minimum_added_chaos_damage", "local_maximum_added_chaos_damage"],
    ];

    private readonly ReviewedItemPropertySemanticImporter _importer = new();

    [Fact]
    public void Import_TrackedReviewedFile_ImportsOnlySixExpectedDescriptors()
    {
        var result = _importer.Import(
            RePoeImportTestFixtures.ReviewedItemPropertySemanticsPath,
            CreateKnownStats());

        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(6, result.SourceRecordsRead);
        Assert.Equal(6, result.RecordsImported);
        Assert.Equal(0, result.RecordsSkipped);
        Assert.Equal(ExpectedVectors.Count, result.ImportedRecords.Count);
        Assert.Equal(ExpectedVectors, result.ImportedRecords.Select(descriptor => descriptor.OrderedStatIds));
        Assert.All(result.ImportedRecords, descriptor =>
        {
            Assert.Equal(ItemPropertyApplicability.UnconditionalDisplayedLocal, descriptor.Applicability);
            var evidence = Assert.Single(descriptor.Evidence);
            Assert.Equal(ItemPropertySemanticEvidenceMethod.ReviewedOverride, evidence.Method);
            Assert.Equal("weapon-dps-v1", evidence.ReviewVersion);
            Assert.Equal("poenhance.item-property-semantics", evidence.SourceId);
        });

        Assert.DoesNotContain(result.ImportedRecords, descriptor => descriptor.OrderedStatIds.SequenceEqual(
            ["spell_minimum_added_fire_damage", "spell_maximum_added_fire_damage"]));
        Assert.DoesNotContain(result.ImportedRecords, descriptor => descriptor.OrderedStatIds.SequenceEqual(
            ["attack_minimum_added_physical_damage", "attack_maximum_added_physical_damage"]));
        Assert.DoesNotContain(result.ImportedRecords, descriptor => descriptor.OrderedStatIds.SequenceEqual(
            ["local_minimum_added_fire_damage_vs_bleeding_enemies", "local_maximum_added_fire_damage_vs_bleeding_enemies"]));
    }

    [Fact]
    public void Import_MalformedJson_FailsWithoutImportingRecords()
    {
        var result = ImportJson("{");

        Assert.True(result.HasErrors);
        Assert.Empty(result.ImportedRecords);
        Assert.Equal(0, result.RecordsImported);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == ItemPropertySemanticImportDiagnosticCodes.JsonMalformed &&
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
    }

    [Fact]
    public void Import_InvalidDescriptor_FailsWholeInputWithoutSilentlySkippingRecord()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "reviewVersion": "weapon-dps-v1",
              "descriptors": [
                {
                  "id": "invalid.missing-contribution",
                  "orderedStatIds": ["local_physical_damage_+%"],
                  "contributions": [],
                  "applicability": "unconditionalDisplayedLocal",
                  "evidence": [
                    {
                      "method": "reviewedOverride",
                      "sourceId": "poenhance.item-property-semantics",
                      "reviewVersion": "weapon-dps-v1",
                      "reviewReference": "review:test"
                    }
                  ]
                }
              ]
            }
            """;

        var result = ImportJson(json, CreateKnownStats());

        Assert.True(result.HasErrors);
        Assert.Empty(result.ImportedRecords);
        Assert.Equal(1, result.SourceRecordsRead);
        Assert.Equal(0, result.RecordsImported);
        Assert.Equal(1, result.RecordsSkipped);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == ItemPropertySemanticImportDiagnosticCodes.ValidationFailed &&
            diagnostic.Message.Contains(
                GameDataValidationErrorCodes.ItemPropertySemanticContributionsRequired,
                StringComparison.Ordinal));
    }

    [Fact]
    public void Import_UnknownField_FailsClosed()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "reviewVersion": "weapon-dps-v1",
              "descriptors": [],
              "inferenceRules": ["forbidden"]
            }
            """;

        var result = ImportJson(json);

        Assert.True(result.HasErrors);
        Assert.Empty(result.ImportedRecords);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == ItemPropertySemanticImportDiagnosticCodes.JsonMalformed);
    }

    private ImportResult<ItemPropertySemanticDescriptor> ImportJson(
        string json,
        IReadOnlyCollection<StatDefinition>? knownStats = null)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return _importer.Import(stream, knownStats);
    }

    private static IReadOnlyCollection<StatDefinition> CreateKnownStats()
    {
        return ExpectedVectors
            .SelectMany(vector => vector)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(statId => new StatDefinition
            {
                Id = statId,
                IsLocal = true,
            })
            .ToArray();
    }
}
