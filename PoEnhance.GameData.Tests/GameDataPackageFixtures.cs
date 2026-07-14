using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

internal static class GameDataPackageFixtures
{
    public static GameDataPackage CreateDevelopmentPackage()
    {
        return new GameDataPackage
        {
            Manifest = GameDataPackageManifestFixtures.CreateDevelopmentManifest(),
            ItemBases =
            [
                new ItemBaseRecord
                {
                    Id = "item-base.gold-ring",
                    Name = "Gold Ring",
                    ItemClass = "Rings",
                    RequiredLevel = 20,
                    Domain = "item",
                    Tags = ["jewellery", "ring"],
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "repoe",
                            ExternalId = "Metadata/Items/Rings/Ring5",
                            ExternalUri = "https://github.com/repoe-fork/repoe",
                        },
                        new GameDataSourceReference
                        {
                            SourceId = "poedb",
                            ExternalId = "Gold Ring",
                            ExternalUri = "https://poedb.tw/us/Gold_Ring",
                        },
                    ],
                },
                new ItemBaseRecord
                {
                    Id = "item-base.granite-flask",
                    Name = "Granite Flask",
                    ItemClass = "Utility Flasks",
                    RequiredLevel = 27,
                    Domain = "flask",
                    Tags = ["flask", "utility-flask"],
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "repoe",
                            ExternalId = "Metadata/Items/Flasks/FlaskUtility5",
                            ExternalUri = "https://github.com/repoe-fork/repoe",
                        },
                        new GameDataSourceReference
                        {
                            SourceId = "poedb",
                            ExternalId = "Granite Flask",
                            ExternalUri = "https://poedb.tw/us/Granite_Flask",
                        },
                    ],
                },
            ],
            Modifiers =
            [
                new ModifierDefinition
                {
                    Id = "mod.implicit.gold-ring.item-rarity",
                    GroupId = "mod-group.gold-ring.item-rarity",
                    Name = "Gold Ring Implicit",
                    GenerationType = ModifierGenerationType.Implicit,
                    RequiredLevel = 20,
                    Domain = "item",
                    Tags = ["jewellery", "ring", "item-rarity"],
                    Stats =
                    [
                        new ModifierStat
                        {
                            Index = 0,
                            StatId = "base_item_found_rarity_+%",
                            MinValue = 6m,
                            MaxValue = 15m,
                        },
                    ],
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "repoe",
                            ExternalId = "GoldRingImplicit",
                        },
                    ],
                },
                new ModifierDefinition
                {
                    Id = "mod.prefix.maximum-life.t5",
                    GroupId = "mod-group.maximum-life",
                    Name = "Hale",
                    GenerationType = ModifierGenerationType.Prefix,
                    Tier = 5,
                    RequiredLevel = 44,
                    Domain = "item",
                    Tags = ["life"],
                    Stats =
                    [
                        new ModifierStat
                        {
                            Index = 0,
                            StatId = "base_maximum_life",
                            MinValue = 50m,
                            MaxValue = 59m,
                        },
                    ],
                    SpawnWeights =
                    [
                        new ModifierSpawnWeight
                        {
                            Tag = "ring",
                            Weight = 1000,
                        },
                    ],
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "repoe",
                            ExternalId = "HaleLife",
                        },
                    ],
                },
                new ModifierDefinition
                {
                    Id = "mod.suffix.fire-resistance.t4",
                    GroupId = "mod-group.fire-resistance",
                    Name = "of the Furnace",
                    GenerationType = ModifierGenerationType.Suffix,
                    Tier = 4,
                    RequiredLevel = 30,
                    Domain = "item",
                    Tags = ["elemental", "fire", "resistance"],
                    Stats =
                    [
                        new ModifierStat
                        {
                            Index = 0,
                            StatId = "base_fire_damage_resistance_%",
                            MinValue = 24m,
                            MaxValue = 29m,
                        },
                    ],
                    SpawnWeights =
                    [
                        new ModifierSpawnWeight
                        {
                            Tag = "ring",
                            Weight = 800,
                        },
                    ],
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "repoe",
                            ExternalId = "FurnaceResistance",
                        },
                        new GameDataSourceReference
                        {
                            SourceId = "poedb",
                            ExternalId = "of the Furnace",
                            ExternalUri = "https://poedb.tw/us/Modifiers",
                        },
                    ],
                },
                new ModifierDefinition
                {
                    Id = "mod.prefix.armour-requirements.hybrid.t3",
                    GroupId = "mod-group.armour-requirements.hybrid",
                    Name = "Athlete's",
                    GenerationType = ModifierGenerationType.Prefix,
                    Tier = 3,
                    RequiredLevel = 60,
                    Domain = "item",
                    Tags = ["defences", "hybrid"],
                    Stats =
                    [
                        new ModifierStat
                        {
                            Index = 0,
                            StatId = "local_armour_+%",
                            MinValue = 80.5m,
                            MaxValue = 100.5m,
                        },
                        new ModifierStat
                        {
                            Index = 1,
                            StatId = "local_attribute_requirements_+%",
                            MinValue = -18.5m,
                            MaxValue = -15.25m,
                        },
                    ],
                    SpawnWeights =
                    [
                        new ModifierSpawnWeight
                        {
                            Tag = "body_armour",
                            Weight = 200,
                        },
                    ],
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "repoe",
                            ExternalId = "AthletesHybrid",
                        },
                    ],
                },
            ],
            Stats =
            [
                new StatDefinition
                {
                    Id = "base_item_found_rarity_+%",
                    IsLocal = false,
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "repoe",
                            ExternalId = "base_item_found_rarity_+%",
                        },
                    ],
                },
                new StatDefinition
                {
                    Id = "base_maximum_life",
                    IsLocal = false,
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "repoe",
                            ExternalId = "base_maximum_life",
                        },
                    ],
                },
                new StatDefinition
                {
                    Id = "base_fire_damage_resistance_%",
                    IsLocal = false,
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "repoe",
                            ExternalId = "base_fire_damage_resistance_%",
                        },
                    ],
                },
                new StatDefinition
                {
                    Id = "local_armour_+%",
                    IsLocal = true,
                    MainHandAliasId = "main_hand_local_armour_+%",
                    OffHandAliasId = "off_hand_local_armour_+%",
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "repoe",
                            ExternalId = "local_armour_+%",
                        },
                    ],
                },
                new StatDefinition
                {
                    Id = "main_hand_local_armour_+%",
                    IsLocal = false,
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "repoe",
                            ExternalId = "main_hand_local_armour_+%",
                        },
                    ],
                },
                new StatDefinition
                {
                    Id = "off_hand_local_armour_+%",
                    IsLocal = false,
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "repoe",
                            ExternalId = "off_hand_local_armour_+%",
                        },
                    ],
                },
                new StatDefinition
                {
                    Id = "local_attribute_requirements_+%",
                    IsLocal = true,
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "repoe",
                            ExternalId = "local_attribute_requirements_+%",
                        },
                    ],
                },
            ],
            StatTranslations =
            [
                new StatTranslationDefinition
                {
                    Id = "translation.base-maximum-life",
                    StatIds = ["base_maximum_life"],
                    Language = "English",
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions =
                            [
                                new StatTranslationCondition
                                {
                                    Index = 0,
                                    MinValue = 1m,
                                },
                            ],
                            ValueFormats = ["#"],
                            IndexHandlers =
                            [
                                new StatTranslationIndexHandler
                                {
                                    Index = 0,
                                    Handlers = [],
                                },
                            ],
                            FormatLines = ["+{0} to maximum Life"],
                        },
                    ],
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "repoe",
                            ExternalId = "translation.base-maximum-life",
                        },
                    ],
                },
            ],
        };
    }
}
