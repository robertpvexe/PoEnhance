using System.Text.Json;
using System.Text.Json.Nodes;
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
        Assert.Null(package.ItemClasses);
        Assert.Null(package.Tags);
        Assert.Null(package.BaseModifierEvidence);
        Assert.Null(package.Manifest.ReviewedItemPropertySemantics);
        Assert.Null(package.Manifest.ItemPropertySemanticAugmentation);
    }

    [Fact]
    public void Serialize_CorruptedAvailabilityAndProvenance_RoundTripsExactly()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var corrupted = package.Modifiers[0] with
        {
            GenerationType = ModifierGenerationType.Corrupted,
            SourceGenerationType = "corrupted",
            SourceAvailability = ModifierSourceAvailability.PotentiallyEligible,
            SpawnWeights =
            [
                new ModifierSpawnWeight { Tag = "graft", Weight = 80 },
                new ModifierSpawnWeight { Tag = "default", Weight = 0 },
            ],
        };
        package = package with
        {
            Modifiers = [corrupted, .. package.Modifiers.Skip(1)],
        };

        var json = GameDataPackageJson.Serialize(package);
        var roundTripped = GameDataPackageJson.Deserialize(json);

        Assert.Contains("\"generationType\": \"corrupted\"", json, StringComparison.Ordinal);
        Assert.Contains("\"sourceAvailability\": \"potentiallyEligible\"", json, StringComparison.Ordinal);
        Assert.NotNull(roundTripped);
        var modifier = roundTripped.Modifiers[0];
        Assert.Equal(ModifierGenerationType.Corrupted, modifier.GenerationType);
        Assert.Equal("corrupted", modifier.SourceGenerationType);
        Assert.Equal(ModifierSourceAvailability.PotentiallyEligible, modifier.SourceAvailability);
        Assert.Equal(["graft", "default"], modifier.SpawnWeights.Select(weight => weight.Tag));
        Assert.Equal([80, 0], modifier.SpawnWeights.Select(weight => weight.Weight));
        Assert.Equal(corrupted.Sources, modifier.Sources);
        Assert.True(GameDataPackageValidator.Validate(roundTripped).IsValid);
    }

    [Fact]
    public void Deserialize_OldPackageMissingSourceAvailability_DefaultsToUnknown()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        var legacyCorrupted = package.Modifiers[0] with
        {
            GenerationType = ModifierGenerationType.Unknown,
            SourceGenerationType = "corrupted",
        };
        package = package with
        {
            Modifiers = [legacyCorrupted, .. package.Modifiers.Skip(1)],
        };
        var root = JsonNode.Parse(GameDataPackageJson.Serialize(package))!;
        foreach (var modifier in root["modifiers"]!.AsArray())
        {
            Assert.True(modifier!.AsObject().Remove("sourceAvailability"));
        }

        var deserialized = GameDataPackageJson.Deserialize(root.ToJsonString());

        Assert.NotNull(deserialized);
        Assert.All(
            deserialized.Modifiers,
            modifier => Assert.Equal(ModifierSourceAvailability.Unknown, modifier.SourceAvailability));
        Assert.Equal(ModifierGenerationType.Unknown, deserialized.Modifiers[0].GenerationType);
        Assert.Equal("corrupted", deserialized.Modifiers[0].SourceGenerationType);
        Assert.True(GameDataPackageValidator.Validate(deserialized).IsValid);
    }

    [Fact]
    public void Deserialize_InvalidSourceAvailabilityEnum_FailsVisibly()
    {
        var root = JsonNode.Parse(
            GameDataPackageJson.Serialize(GameDataPackageFixtures.CreateDevelopmentPackage()))!;
        root["modifiers"]![0]!["sourceAvailability"] = "notARealAvailability";

        Assert.Throws<JsonException>(() => GameDataPackageJson.Deserialize(root.ToJsonString()));
    }

    [Fact]
    public void Serialize_NewEligibilitySourceCatalogs_RoundTripExactly()
    {
        var package = AddEligibilitySources(GameDataPackageFixtures.CreateDevelopmentPackage());

        var json = GameDataPackageJson.Serialize(package);
        var roundTripped = GameDataPackageJson.Deserialize(json);

        Assert.Contains("\"semantics\": \"positiveAndContextualOnly\"", json, StringComparison.Ordinal);
        Assert.Contains("\"coverage\": \"partial\"", json, StringComparison.Ordinal);
        Assert.NotNull(roundTripped);
        Assert.Equal(package.ItemClasses!.Select(item => item.Id), roundTripped.ItemClasses!.Select(item => item.Id));
        Assert.Equal(package.ItemClasses!.SelectMany(item => item.InfluenceTagIds), roundTripped.ItemClasses!.SelectMany(item => item.InfluenceTagIds));
        Assert.Equal(package.Tags!.Select(tag => tag.Id), roundTripped.Tags!.Select(tag => tag.Id));
        Assert.Equal(package.BaseModifierEvidence!.Semantics, roundTripped.BaseModifierEvidence!.Semantics);
        Assert.Equal(package.BaseModifierEvidence.Coverage, roundTripped.BaseModifierEvidence.Coverage);
        Assert.Equal(
            package.BaseModifierEvidence.Groups.SelectMany(group => group.Modifiers).Select(modifier => modifier.ModifierId),
            roundTripped.BaseModifierEvidence.Groups.SelectMany(group => group.Modifiers).Select(modifier => modifier.ModifierId));
        Assert.True(GameDataPackageValidator.Validate(roundTripped).IsValid);
    }

    private static GameDataPackage AddEligibilitySources(GameDataPackage package)
    {
        var source = new GameDataSourceReference { SourceId = "repoe", ExternalId = "fixture" };
        var itemClasses = package.ItemBases
            .Select(itemBase => itemBase.ItemClass!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new ItemClassDefinition { Id = name, Name = name, Sources = [source with { ExternalId = name }] })
            .ToArray();
        var tags = package.ItemBases.SelectMany(itemBase => itemBase.Tags)
            .Concat(package.Modifiers.SelectMany(modifier => modifier.Tags))
            .Concat(package.Modifiers.SelectMany(modifier => modifier.SpawnWeights.Select(weight => weight.Tag!)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => new TagDefinition { Id = id, Sources = [source with { ExternalId = id }] })
            .ToArray();
        return package with
        {
            ItemClasses = itemClasses,
            Tags = tags,
            BaseModifierEvidence = new BaseModifierSourceEvidence
            {
                Semantics = BaseModifierEvidenceSemantics.PositiveAndContextualOnly,
                Coverage = BaseModifierEvidenceCoverage.Partial,
                SourceBaseEntriesRead = 1,
                BaseEntriesRepresented = 1,
                SourceRelationshipsRead = 1,
                RelationshipsRepresented = 1,
                Groups =
                [
                    new BaseModifierSourceEvidenceGroup
                    {
                        BaseItemIds = [package.ItemBases[0].Id!],
                        Modifiers =
                        [
                            new BaseModifierSourceEvidenceEntry
                            {
                                ModifierId = package.Modifiers[1].Id,
                                ReportedWeight = 1000,
                                SourceGenerationBucket = "prefix",
                            },
                        ],
                        Sources = [source with { ExternalId = "mods_by_base.json#/fixture" }],
                    },
                ],
                Sources = [source with { ExternalId = "mods_by_base.json" }],
            },
        };
    }
}
