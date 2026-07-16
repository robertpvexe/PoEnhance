using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

public sealed class GameDataPackageJsonTests
{
    [Fact]
    public void Serialize_DevelopmentPackage_UsesReadablePackageShape()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();

        var json = GameDataPackageJson.Serialize(package);

        Assert.Contains("\"manifest\": {", json);
        Assert.Contains("\"itemBases\": [", json);
        Assert.Contains("\"modifiers\": [", json);
        Assert.Contains("\"stats\": [", json);
        Assert.Contains("\"statTranslations\": [", json);
        Assert.Contains("\"generationType\": \"prefix\"", json);
        Assert.Contains("\n", json);
    }

    [Fact]
    public void Deserialize_SerializedPackage_RoundTripsCompletePackage()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();

        var json = GameDataPackageJson.Serialize(package);
        var roundTrippedPackage = GameDataPackageJson.Deserialize(json);

        Assert.NotNull(roundTrippedPackage);
        Assert.Equal(package.Manifest.DataVersion, roundTrippedPackage.Manifest.DataVersion);
        Assert.Equal(package.Manifest.CreatedAtUtc, roundTrippedPackage.Manifest.CreatedAtUtc);
        Assert.Equal(package.ItemBases.Count, roundTrippedPackage.ItemBases.Count);
        Assert.Equal(package.Modifiers.Count, roundTrippedPackage.Modifiers.Count);
        Assert.Equal(package.Stats.Count, roundTrippedPackage.Stats.Count);
        Assert.Equal(package.StatTranslations.Count, roundTrippedPackage.StatTranslations.Count);
        Assert.True(GameDataPackageValidator.Validate(roundTrippedPackage).IsValid);
    }

    [Fact]
    public void Deserialize_SerializedPackage_PreservesSourceReferencesExactly()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();

        var json = GameDataPackageJson.Serialize(package);
        var roundTrippedPackage = GameDataPackageJson.Deserialize(json);

        Assert.NotNull(roundTrippedPackage);
        var goldRing = roundTrippedPackage.ItemBases.Single(itemBase => itemBase.Id == "item-base.gold-ring");
        Assert.Collection(
            goldRing.Sources,
            repoe =>
            {
                Assert.Equal("repoe", repoe.SourceId);
                Assert.Equal("Metadata/Items/Rings/Ring5", repoe.ExternalId);
                Assert.Equal("https://github.com/repoe-fork/repoe", repoe.ExternalUri);
            },
            poedb =>
            {
                Assert.Equal("poedb", poedb.SourceId);
                Assert.Equal("Gold Ring", poedb.ExternalId);
                Assert.Equal("https://poedb.tw/us/Gold_Ring", poedb.ExternalUri);
            });

        var suffix = roundTrippedPackage.Modifiers.Single(modifier => modifier.Id == "mod.suffix.fire-resistance.t4");
        var poedbSource = suffix.Sources.Single(source => source.SourceId == "poedb");
        Assert.Equal("of the Furnace", poedbSource.ExternalId);
        Assert.Equal("https://poedb.tw/us/Modifiers", poedbSource.ExternalUri);
    }

    [Fact]
    public void Deserialize_SerializedPackage_PreservesHybridStatOrder()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();

        var json = GameDataPackageJson.Serialize(package);
        var roundTrippedPackage = GameDataPackageJson.Deserialize(json);

        Assert.NotNull(roundTrippedPackage);
        var hybrid = roundTrippedPackage.Modifiers.Single(
            modifier => modifier.Id == "mod.prefix.armour-requirements.hybrid.t3");

        Assert.Collection(
            hybrid.Stats,
            first =>
            {
                Assert.Equal(0, first.Index);
                Assert.Equal("local_armour_+%", first.StatId);
            },
            second =>
            {
                Assert.Equal(1, second.Index);
                Assert.Equal("local_attribute_requirements_+%", second.StatId);
            });
    }

    [Fact]
    public void Deserialize_SerializedPackage_PreservesStatsAndTranslations()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();

        var json = GameDataPackageJson.Serialize(package);
        var roundTrippedPackage = GameDataPackageJson.Deserialize(json);

        Assert.NotNull(roundTrippedPackage);
        var localArmour = roundTrippedPackage.Stats.Single(stat => stat.Id == "local_armour_+%");
        Assert.True(localArmour.IsLocal);
        Assert.Equal("main_hand_local_armour_+%", localArmour.MainHandAliasId);
        Assert.Equal("off_hand_local_armour_+%", localArmour.OffHandAliasId);

        var translation = Assert.Single(roundTrippedPackage.StatTranslations);
        Assert.Equal("English", translation.Language);
        Assert.Equal(["base_maximum_life"], translation.StatIds);
        Assert.Equal(["+{0} to maximum Life"], Assert.Single(translation.Variants).FormatLines);
        Assert.Equal(1m, Assert.Single(translation.Variants[0].Conditions).MinValue);
    }


    [Fact]
    public void Serialize_Enums_AreStableHumanReadableStrings()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();

        var json = GameDataPackageJson.Serialize(package);

        Assert.Contains("\"generationType\": \"implicit\"", json);
        Assert.Contains("\"generationType\": \"prefix\"", json);
        Assert.Contains("\"generationType\": \"suffix\"", json);
        Assert.DoesNotContain("\"generationType\": 1", json);
    }

    [Fact]
    public void Deserialize_SerializedPackage_PreservesFractionalAndNegativeStatsExactly()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();

        var json = GameDataPackageJson.Serialize(package);
        var roundTrippedPackage = GameDataPackageJson.Deserialize(json);

        Assert.NotNull(roundTrippedPackage);
        var hybrid = roundTrippedPackage.Modifiers.Single(
            modifier => modifier.Id == "mod.prefix.armour-requirements.hybrid.t3");

        Assert.Equal(80.5m, hybrid.Stats[0].MinValue);
        Assert.Equal(100.5m, hybrid.Stats[0].MaxValue);
        Assert.Equal(-18.5m, hybrid.Stats[1].MinValue);
        Assert.Equal(-15.25m, hybrid.Stats[1].MaxValue);
    }

    [Fact]
    public void Deserialize_MissingPackageCollections_UsesEmptyCollections()
    {
        const string json = """
            {
              "manifest": {
                "schemaVersion": 1,
                "dataVersion": "dev-2026-01-15",
                "createdAtUtc": "2026-01-15T12:00:00+00:00",
                "league": "Mercenaries",
                "patch": "3.26.0",
                "sources": [
                  {
                    "sourceId": "repoe",
                    "retrievedAtUtc": "2026-01-15T12:05:00+00:00",
                    "sourceVersion": "c50acab2ed660a70511e7f91ee09db4e632089e4",
                    "sourceUri": "https://github.com/repoe-fork/repoe",
                    "sourceBranch": "master"
                  }
                ]
              }
            }
            """;

        var package = GameDataPackageJson.Deserialize(json);

        Assert.NotNull(package);
        Assert.NotNull(package.ItemBases);
        Assert.NotNull(package.Modifiers);
        Assert.NotNull(package.Stats);
        Assert.NotNull(package.StatTranslations);
        Assert.NotNull(package.ItemPropertySemantics);
        Assert.Empty(package.ItemBases);
        Assert.Empty(package.Modifiers);
        Assert.Empty(package.Stats);
        Assert.Empty(package.StatTranslations);
        Assert.Empty(package.ItemPropertySemantics);
    }
}
