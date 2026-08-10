using PoEnhance.Core.Items.GameData;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class ModifierHistoricalTranslationRecognitionTests
{
    private readonly ModifierTextSignatureMatcher matcher = new();

    [Fact]
    public void HistoricalExactRendering_ResolvesWhenCurrentRenderingCannot()
    {
        var catalog = Catalog(
            current: Translation("stat", "Current {0}% Damage"),
            historical: [Translation("stat", "Historical {0}% Damage")]);

        var result = matcher.Match(Modifier("stat"), catalog, ["Historical 20% Damage"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Match, result.Outcome);
        Assert.Equal(StatTranslationRecognitionRole.HistoricalExact, result.TranslationRecognition?.Role);
        Assert.Equal("historical", result.TranslationRecognition?.SourceSnapshotId);
        Assert.Equal("Current <number>% Damage", Assert.Single(result.TranslationRecognition!.CanonicalSignature.Lines));
    }

    [Fact]
    public void CurrentExactRendering_RemainsFirstClass()
    {
        var catalog = Catalog(
            current: Translation("stat", "Current {0}% Damage"),
            historical: [Translation("stat", "Historical {0}% Damage")]);

        var result = matcher.Match(Modifier("stat"), catalog, ["Current 20% Damage"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Match, result.Outcome);
        Assert.Equal(StatTranslationRecognitionRole.CurrentExact, result.TranslationRecognition?.Role);
    }

    [Fact]
    public void CrossStatHistoricalEvidence_IsNotReused()
    {
        var catalog = Catalog(
            current: Translation("stat", "Current {0}% Damage"),
            historical: [Translation("other_stat", "Historical {0}% Damage")]);

        var result = matcher.Match(Modifier("stat"), catalog, ["Historical 20% Damage"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.NoMatch, result.Outcome);
        Assert.Null(result.TranslationRecognition);
    }

    [Fact]
    public void AmbiguousHistoricalForms_FailClosed()
    {
        var historical = Translation("stat", "Historical {0}% Damage");
        var catalog = Catalog(
            current: Translation("stat", "Current {0}% Damage"),
            historical: [historical, historical with { Id = "duplicate" }]);

        var result = matcher.Match(Modifier("stat"), catalog, ["Historical 20% Damage"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Unknown, result.Outcome);
        Assert.Equal(ModifierTextSignatureMatchReasonCodes.HistoricalTranslationAmbiguous, result.ReasonCode);
        Assert.Null(result.TranslationRecognition);
    }

    [Fact]
    public void MechanicsChangedHistoricalForm_IsNotEligibleForFallback()
    {
        var current = Translation("stat", "Current {0}% Damage");
        var historical = Translation("stat", "Historical {0}% Damage", ["negate"]);
        var catalog = Catalog(
            current,
            [historical],
            StatTranslationCompatibilityClassification.MechanicsChanged);

        var result = matcher.Match(Modifier("stat"), catalog, ["Historical 20% Damage"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.NoMatch, result.Outcome);
        Assert.Null(result.TranslationRecognition);
    }

    private static GameDataCatalog Catalog(
        StatTranslationDefinition current,
        IReadOnlyList<StatTranslationDefinition> historical,
        StatTranslationCompatibilityClassification classification =
            StatTranslationCompatibilityClassification.MechanicallyEquivalentRendering)
    {
        var observations = new List<StatTranslationObservation>
        {
            Observation("current", "current", current),
        };
        var changes = new List<StatTranslationCompatibilityChange>();
        for (var index = 0; index < historical.Count; index++)
        {
            var id = $"historical-{index}";
            observations.Add(Observation(id, "historical", historical[index]));
            changes.Add(new StatTranslationCompatibilityChange
            {
                Id = $"change-{index}",
                CurrentObservationId = "current",
                HistoricalObservationId = id,
                Classification = classification,
                RuntimeRelevance = StatTranslationRuntimeRelevance.OrdinaryItemModifier,
            });
        }

        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 1,
                DataVersion = "test",
                CreatedAtUtc = DateTimeOffset.Parse("2026-08-10T00:00:00Z"),
                League = "test",
                Patch = "test",
                Sources =
                [
                    ManifestSource("repoe"),
                    ManifestSource("repoe-historical-base-implicit"),
                ],
            },
            ItemBases = [],
            Modifiers = [Modifier("stat")],
            Stats = [new StatDefinition { Id = "stat", Sources = [new GameDataSourceReference { SourceId = "repoe" }] }],
            StatTranslations = [current with { Sources = [new GameDataSourceReference { SourceId = "repoe" }] }],
            StatTranslationHistory = new StatTranslationHistoryCatalog
            {
                SourceSnapshots =
                [
                    Snapshot("current", StatTranslationSnapshotRole.CurrentCandidate, "repoe", 'a'),
                    Snapshot("historical", StatTranslationSnapshotRole.HistoricalObserved, "repoe-historical-base-implicit", 'b'),
                ],
                Observations = observations,
                Changes = changes,
            },
        });
    }

    private static GameDataPackageSource ManifestSource(string id) => new()
    {
        SourceId = id,
        SourceVersion = new string('a', 40),
        SourceUri = "https://github.com/repoe-fork/repoe",
    };

    private static StatTranslationSourceSnapshot Snapshot(
        string id,
        StatTranslationSnapshotRole role,
        string manifestSourceId,
        char sha) => new()
    {
        Id = id,
        Role = role,
        ManifestSourceId = manifestSourceId,
        RepositoryUri = "https://github.com/repoe-fork/repoe",
        CommitSha = new string(sha, 40),
        DataVersion = "test",
        Files =
        [
            new StatTranslationSourceFile { LogicalRole = "stats", PackageInputLabel = "stats.json" },
            new StatTranslationSourceFile { LogicalRole = "statTranslations", PackageInputLabel = "stat_translations.json" },
        ],
    };

    private static StatTranslationObservation Observation(
        string id,
        string snapshotId,
        StatTranslationDefinition translation) => new()
    {
        Id = id,
        SourceSnapshotId = snapshotId,
        StatIds = translation.StatIds,
        Translation = translation,
        MechanicalSignature = StatTranslationStructuralSemantics.MechanicalSignature(translation),
        RenderingSignature = StatTranslationStructuralSemantics.RenderingSignature(translation),
        NumericShapeSignature = StatTranslationStructuralSemantics.NumericShapeSignature(translation),
        ModifierUsageCount = 1,
    };

    private static ModifierDefinition Modifier(string statId) => new()
    {
        Id = "modifier",
        GroupId = "group",
        Name = "Test",
        GenerationType = ModifierGenerationType.Prefix,
        Domain = "item",
        Stats = [new ModifierStat { Index = 0, StatId = statId, MinValue = 1, MaxValue = 100 }],
        Sources = [new GameDataSourceReference { SourceId = "repoe" }],
    };

    private static StatTranslationDefinition Translation(
        string statId,
        string line,
        IReadOnlyList<string>? handlers = null) => new()
    {
        Id = $"translation-{statId}",
        StatIds = [statId],
        Language = "English",
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions = [new StatTranslationCondition { Index = 0 }],
                ValueFormats = ["#"],
                IndexHandlers = [new StatTranslationIndexHandler { Index = 0, Handlers = handlers ?? [] }],
                FormatLines = [line],
            },
        ],
    };
}
