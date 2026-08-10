using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

public sealed class StatTranslationHistoryJsonTests
{
    [Fact]
    public void PackageRoundTrip_PreservesVersionedTranslationObservationsAndSourceRoles()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage() with
        {
            StatTranslationHistory = History(),
        };

        var roundTripped = Assert.IsType<GameDataPackage>(
            GameDataPackageJson.Deserialize(GameDataPackageJson.Serialize(package)));

        var history = Assert.IsType<StatTranslationHistoryCatalog>(roundTripped.StatTranslationHistory);
        Assert.Equal(
            [StatTranslationSnapshotRole.CurrentCandidate, StatTranslationSnapshotRole.HistoricalObserved],
            history.SourceSnapshots.Select(source => source.Role));
        var historical = Assert.Single(history.Observations, observation =>
            observation.SourceSnapshotId == "historical");
        Assert.Equal("historical-observation", historical.Id);
        Assert.Equal("mechanical", historical.MechanicalSignature);
        Assert.Equal("rendering-old", historical.RenderingSignature);
        Assert.Equal("numeric", historical.NumericShapeSignature);
        Assert.Equal("Old {0} text", Assert.Single(historical.Translation!.Variants).FormatLines[0]);
        Assert.Equal(
            StatTranslationCompatibilityClassification.MechanicallyEquivalentRendering,
            Assert.Single(history.Changes).Classification);
    }

    [Fact]
    public void OldPackageWithoutTranslationHistory_LoadsAsUnavailableRatherThanNegativeEvidence()
    {
        var json = GameDataPackageJson.Serialize(GameDataPackageFixtures.CreateDevelopmentPackage());

        var package = Assert.IsType<GameDataPackage>(GameDataPackageJson.Deserialize(json));

        Assert.Null(package.StatTranslationHistory);
    }

    private static StatTranslationHistoryCatalog History() => new()
    {
        SourceSnapshots =
        [
            Source("current", StatTranslationSnapshotRole.CurrentCandidate, "a"),
            Source("historical", StatTranslationSnapshotRole.HistoricalObserved, "b"),
        ],
        Observations =
        [
            Observation("current-observation", "current", "Current {0} text", "rendering-current"),
            Observation("historical-observation", "historical", "Old {0} text", "rendering-old"),
        ],
        Changes =
        [
            new StatTranslationCompatibilityChange
            {
                Id = "change",
                CurrentObservationId = "current-observation",
                HistoricalObservationId = "historical-observation",
                Classification = StatTranslationCompatibilityClassification.MechanicallyEquivalentRendering,
                RuntimeRelevance = StatTranslationRuntimeRelevance.OrdinaryItemModifier,
            },
        ],
    };

    private static StatTranslationSourceSnapshot Source(
        string id,
        StatTranslationSnapshotRole role,
        string shaPrefix) => new()
    {
        Id = id,
        Role = role,
        ManifestSourceId = role == StatTranslationSnapshotRole.CurrentCandidate
            ? "repoe"
            : "repoe-historical-base-implicit",
        RepositoryUri = "https://github.com/repoe-fork/repoe",
        CommitSha = new string(shaPrefix[0], 40),
        DataVersion = role == StatTranslationSnapshotRole.CurrentCandidate ? "3.29.1.2.2" : "3.28.0.13",
        Files =
        [
            new StatTranslationSourceFile { LogicalRole = "stats", PackageInputLabel = $"{id}-stats.json" },
            new StatTranslationSourceFile { LogicalRole = "statTranslations", PackageInputLabel = $"{id}-translations.json" },
        ],
    };

    private static StatTranslationObservation Observation(
        string id,
        string snapshotId,
        string line,
        string renderingSignature) => new()
    {
        Id = id,
        SourceSnapshotId = snapshotId,
        StatIds = ["stat"],
        Translation = new StatTranslationDefinition
        {
            Id = "translation",
            StatIds = ["stat"],
            Language = "English",
            Variants =
            [
                new StatTranslationVariant
                {
                    Conditions = [new StatTranslationCondition { Index = 0 }],
                    ValueFormats = ["#"],
                    IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
                    FormatLines = [line],
                },
            ],
        },
        MechanicalSignature = "mechanical",
        RenderingSignature = renderingSignature,
        NumericShapeSignature = "numeric",
        ModifierUsageCount = 1,
    };
}
