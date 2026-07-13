using System.Text;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class RePoeStatTranslationsImporterTests
{
    private readonly RePoeStatsImporter _statsImporter = new();
    private readonly RePoeStatTranslationsImporter _translationsImporter = new();

    [Fact]
    public void Import_ReducedFixture_ImportsExpectedTranslationsDeterministically()
    {
        var stats = ImportReducedStats();

        var result = _translationsImporter.Import(RePoeImportTestFixtures.ReducedStatTranslationsPath, stats);

        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(6, result.SourceRecordsRead);
        Assert.Equal(6, result.RecordsImported);
        Assert.Equal(0, result.RecordsSkipped);
        Assert.Equal(
            result.ImportedRecords.OrderBy(translation => translation.Id, StringComparer.Ordinal).Select(translation => translation.Id),
            result.ImportedRecords.Select(translation => translation.Id));
    }

    [Fact]
    public void Import_SingleStatTranslation_PreservesLanguageConditionsAndSource()
    {
        var stats = ImportReducedStats();

        var result = _translationsImporter.Import(RePoeImportTestFixtures.ReducedStatTranslationsPath, stats);
        var translation = result.ImportedRecords.Single(record =>
            record.StatIds.SequenceEqual(["heist_coins_from_monsters_+%"]));

        Assert.Equal("English", translation.Language);
        Assert.StartsWith("repoe:stat-translation:", translation.Id, StringComparison.Ordinal);
        Assert.Collection(
            translation.Variants,
            positive =>
            {
                Assert.Equal(1m, Assert.Single(positive.Conditions).MinValue);
                Assert.Null(positive.Conditions[0].MaxValue);
                Assert.Equal(["#"], positive.ValueFormats);
                Assert.Empty(Assert.Single(positive.IndexHandlers).Handlers);
                Assert.Equal(["{0}% increased Rogue's Markers dropped by monsters"], positive.FormatLines);
            },
            negative =>
            {
                Assert.Null(Assert.Single(negative.Conditions).MinValue);
                Assert.Equal(-1m, negative.Conditions[0].MaxValue);
                Assert.Equal(["negate"], Assert.Single(negative.IndexHandlers).Handlers);
                Assert.Equal(["{0}% reduced Rogue's Markers dropped by monsters"], negative.FormatLines);
            });

        var source = Assert.Single(translation.Sources);
        Assert.Equal("repoe", source.SourceId);
        Assert.Equal(translation.Id, source.ExternalId);
    }

    [Fact]
    public void Import_MultiLineAndMultiStatTranslations_PreserveOrder()
    {
        var stats = ImportReducedStats();

        var result = _translationsImporter.Import(RePoeImportTestFixtures.ReducedStatTranslationsPath, stats);
        var multiLine = result.ImportedRecords.Single(record =>
            record.StatIds.SequenceEqual(["local_jewel_+%_effect_per_passive_between_jewel_and_class_start"]));
        var support = result.ImportedRecords.Single(record =>
            record.StatIds.SequenceEqual(["local_random_support_gem_level", "local_random_support_gem_index"]));

        Assert.Equal(
            [
                "This Jewel's Socket has {0}% increased effect per Allocated Passive Skill between",
                "it and your Class's starting location",
            ],
            Assert.Single(multiLine.Variants).FormatLines);

        var supportVariant = Assert.Single(support.Variants);
        Assert.Equal(["#", "#"], supportVariant.ValueFormats);
        Assert.Collection(
            supportVariant.IndexHandlers,
            first =>
            {
                Assert.Equal(0, first.Index);
                Assert.Empty(first.Handlers);
            },
            second =>
            {
                Assert.Equal(1, second.Index);
                Assert.Equal(["display_indexable_support"], second.Handlers);
            });
    }

    [Fact]
    public void Import_ExplicitNullConditionBoundsAndNegated_TreatsAsAbsent()
    {
        var stats = new[]
        {
            new StatDefinition { Id = "known_stat" },
        };
        var json = """
            [
              {
                "ids": ["known_stat"],
                "English": [
                  {
                    "condition": [{ "min": null, "max": null, "negated": null }],
                    "format": ["#"],
                    "index_handlers": [[]],
                    "string": "Known {0}"
                  }
                ],
                "hidden": false,
                "trade_stats": ["pseudo.pseudo_known_stat"],
                "reminder_text": null,
                "is_markup": true
              }
            ]
            """;

        var result = ImportJson(json, stats);
        var translation = Assert.Single(result.ImportedRecords);
        var variant = Assert.Single(translation.Variants);
        var condition = Assert.Single(variant.Conditions);

        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(1, result.SourceRecordsRead);
        Assert.Equal(1, result.RecordsImported);
        Assert.Equal(0, result.RecordsSkipped);
        Assert.Equal(["known_stat"], translation.StatIds);
        Assert.Equal("English", translation.Language);
        Assert.Null(condition.MinValue);
        Assert.Null(condition.MaxValue);
        Assert.Equal(["#"], variant.ValueFormats);
        Assert.Empty(Assert.Single(variant.IndexHandlers).Handlers);
        Assert.Equal(["Known {0}"], variant.FormatLines);
    }

    [Fact]
    public void Import_NonNullConditionBoundsAndNegated_PreservesNeutralFields()
    {
        var stats = new[]
        {
            new StatDefinition { Id = "known_stat" },
        };
        var json = """
            [
              {
                "ids": ["known_stat"],
                "English": [
                  {
                    "condition": [{ "min": -5, "max": 10, "negated": true }],
                    "format": ["negate"],
                    "index_handlers": [["divide_by_one_hundred"]],
                    "string": "Known {0}\nSecond line"
                  }
                ]
              }
            ]
            """;

        var result = ImportJson(json, stats);
        var variant = Assert.Single(Assert.Single(result.ImportedRecords).Variants);
        var condition = Assert.Single(variant.Conditions);
        var handler = Assert.Single(variant.IndexHandlers);

        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(-5m, condition.MinValue);
        Assert.Equal(10m, condition.MaxValue);
        Assert.Equal(["negate"], variant.ValueFormats);
        Assert.Equal(["divide_by_one_hundred"], handler.Handlers);
        Assert.Equal(["Known {0}", "Second line"], variant.FormatLines);
    }

    [Fact]
    public void Import_OldFixture_RemainsCompatibleAfterNullableConditionSupport()
    {
        var stats = ImportReducedStats();

        var result = _translationsImporter.Import(RePoeImportTestFixtures.ReducedStatTranslationsPath, stats);

        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(6, result.RecordsImported);
        Assert.Equal(0, result.RecordsSkipped);
    }

    [Fact]
    public void Import_MalformedRecords_SkipsInvalidRecordsWithDiagnostics()
    {
        var stats = new[]
        {
            new StatDefinition { Id = "known_stat" },
        };
        var json = """
            [
              {
                "ids": ["known_stat"],
                "English": [
                  {
                    "condition": [{}],
                    "format": ["#"],
                    "index_handlers": [[]],
                    "string": "Known {0}"
                  }
                ]
              },
              {
                "ids": ["unknown_stat"],
                "English": [
                  {
                    "condition": [{}],
                    "format": ["#"],
                    "index_handlers": [[]],
                    "string": "Unknown {0}"
                  }
                ]
              },
              {
                "ids": ["known_stat"],
                "English": [
                  {
                    "condition": [{ "min": 5, "max": 1 }],
                    "format": ["#"],
                    "index_handlers": [[]],
                    "string": "Bad range {0}"
                  }
                ]
              },
              {
                "ids": ["known_stat"],
                "English": [
                  {
                    "condition": [{}],
                    "format": ["#"],
                    "index_handlers": [[], []],
                    "string": "Bad handler {0}"
                  }
                ]
              }
            ]
            """;

        var result = ImportJson(json, stats);

        Assert.False(result.HasErrors);
        Assert.Equal(4, result.SourceRecordsRead);
        Assert.Equal(1, result.RecordsImported);
        Assert.Equal(3, result.RecordsSkipped);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.StatTranslationUnknownStatId);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.StatTranslationInvalidCondition);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.StatTranslationInvalidIndexHandler);
    }

    [Fact]
    public void Import_DerivedIds_AreStableAcrossRuns()
    {
        var stats = ImportReducedStats();

        var first = _translationsImporter.Import(RePoeImportTestFixtures.ReducedStatTranslationsPath, stats);
        var second = _translationsImporter.Import(RePoeImportTestFixtures.ReducedStatTranslationsPath, stats);

        Assert.Equal(
            first.ImportedRecords.Select(record => record.Id),
            second.ImportedRecords.Select(record => record.Id));
    }

    [Fact]
    public void Import_UnsupportedRootShape_ReturnsSchemaUnsupported()
    {
        var result = ImportJson("{}");

        Assert.True(result.HasErrors);
        AssertHasDiagnostic(result, RePoeImportDiagnosticCodes.SchemaUnsupported, ImportDiagnosticSeverity.Error);
    }

    private IReadOnlyList<StatDefinition> ImportReducedStats()
    {
        var result = _statsImporter.Import(RePoeImportTestFixtures.ReducedStatsPath);

        Assert.False(result.HasErrors);
        return result.ImportedRecords;
    }

    private ImportResult<StatTranslationDefinition> ImportJson(
        string json,
        IReadOnlyCollection<StatDefinition>? knownStats = null)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return _translationsImporter.Import(stream, knownStats);
    }

    private static void AssertHasDiagnostic(
        ImportResult<StatTranslationDefinition> result,
        string code,
        ImportDiagnosticSeverity? severity = null)
    {
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == code &&
            (!severity.HasValue || diagnostic.Severity == severity.Value));
    }
}
