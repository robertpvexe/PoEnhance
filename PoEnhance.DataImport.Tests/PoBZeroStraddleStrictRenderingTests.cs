using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

/// <summary>
/// U.2.4 — PoB authors zero-straddling Unique rolls under reduced/less templates while keeping
/// the full signed interval in the display range. Constrained Exact rendering alone cannot match.
/// </summary>
public sealed class PoBZeroStraddleStrictRenderingTests
{
    [Fact]
    public void Import_ZeroStraddleReducedAttackSpeed_ResolvesExactLocalOwner()
    {
        // Shape mirrors Echoforge / LocalIncreasedAttackSpeedUnique__38 vs a foreign global fork.
        const string line = "(-16-16)% reduced Attack Speed";
        var exportRoot = CreateExportTree("""
            return {
            [[
            Test Echo Sword
            Infernal Sword
            Implicits: 0
            LocalIncreasedAttackSpeedUnique__38
            ]],[[
            Other Sword
            Infernal Sword
            Implicits: 0
            IncreasedAttackSpeedUnique__9
            ]],
            }
            """);
        try
        {
            var result = Import(
                """
                    Test Echo Sword
                    Infernal Sword
                    Implicits: 0
                    (-16-16)% reduced Attack Speed
                    """,
                modifiers:
                [
                    UniqueModifier(
                        "LocalIncreasedAttackSpeedUnique__38",
                        "local_attack_speed_+%",
                        -16,
                        16),
                    UniqueModifier(
                        "IncreasedAttackSpeedUnique__9",
                        "attack_speed_+%",
                        -16,
                        16),
                ],
                translations:
                [
                    AttackSpeedTranslation("local-attack-speed", "local_attack_speed_+%"),
                    AttackSpeedTranslation("global-attack-speed", "attack_speed_+%"),
                ],
                pobSourceRoot: exportRoot);

            var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
                .ModifierBlocks);
            Assert.Equal(line, Assert.Single(block.Lines));
            Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
            Assert.Null(block.MechanicalMapping.ConflictEvidence);
            Assert.Equal(
                ["LocalIncreasedAttackSpeedUnique__38"],
                block.MechanicalMapping.ModifierIds);
            Assert.Equal(["local_attack_speed_+%"], block.MechanicalMapping.StatIds);
            Assert.Contains(
                PoBUniqueCatalogImporter.ExportUniqueOwnershipFilterReason,
                block.MechanicalMapping.Provenance!.ResolutionReasons);
        }
        finally
        {
            if (Directory.Exists(exportRoot))
            {
                Directory.Delete(exportRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Import_ZeroStraddleReducedReservationEfficiency_ResolvesExact()
    {
        // Shape mirrors Ventor's Gamble Current / ManaReservationEfficiencyUnique__1.
        const string line = "(-15-15)% reduced Mana Reservation Efficiency of Skills";
        var result = Import(
            """
                Test Gamble Ring
                Gold Ring
                Implicits: 0
                (-15-15)% reduced Mana Reservation Efficiency of Skills
                """,
            modifiers:
            [
                UniqueModifier(
                    "ManaReservationEfficiencyUnique__1",
                    "base_mana_reservation_efficiency_+%",
                    -15,
                    15),
                UniqueModifier(
                    "ManaReservationEfficiencyUnique__2",
                    "base_mana_reservation_efficiency_+%",
                    10,
                    20),
            ],
            translations:
            [
                ReservationEfficiencyTranslation("reservation-efficiency"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(line, Assert.Single(block.Lines));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["ManaReservationEfficiencyUnique__1"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["base_mana_reservation_efficiency_+%"],
            block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_ZeroStraddle_DoesNotSelectLegacyInverseEncoding()
    {
        // Ventor's Current shape: modern +% efficiency vs legacy inverse encoding share display
        // after straddle, but only the modern -15..15 vector Exact-matches the PoB line.
        const string line = "(-15-15)% reduced Mana Reservation Efficiency of Skills";
        var result = Import(
            """
                Test Gamble Ring
                Gold Ring
                Variant: Current
                Implicits: 0
                (-15-15)% reduced Mana Reservation Efficiency of Skills
                """,
            modifiers:
            [
                UniqueModifier(
                    "ManaReservationEfficiencyUnique__1",
                    "base_mana_reservation_efficiency_+%",
                    -15,
                    15),
                UniqueModifier(
                    "ReducedManaReservationsCostUniqueHelmetDex5",
                    "mana_reservation_efficiency_-2%_per_1",
                    -15,
                    15),
            ],
            translations:
            [
                ReservationEfficiencyTranslation("reservation-efficiency"),
                new StatTranslationDefinition
                {
                    Id = "reservation-legacy-inverse",
                    StatIds = ["mana_reservation_efficiency_-2%_per_1"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions =
                            [
                                new StatTranslationCondition { Index = 0, MaxValue = -1 },
                            ],
                            FormatLines = ["{0}% reduced Mana Reservation Efficiency of Skills"],
                            ValueFormats = ["#"],
                            IndexHandlers =
                            [
                                new StatTranslationIndexHandler
                                {
                                    Index = 0,
                                    Handlers = ["negate_and_double"],
                                },
                            ],
                        },
                    ],
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(line, Assert.Single(block.Lines));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["ManaReservationEfficiencyUnique__1"], block.MechanicalMapping.ModifierIds);
        Assert.DoesNotContain(
            "mana_reservation_efficiency_-2%_per_1",
            block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void PinnedModTextMap_ZeroStraddleKeys_AreSingularOwners()
    {
        var pobRoot = Path.Combine(Path.GetTempPath(), "PoEnhance-PoB-v2.67.2-b32759a");
        var path = Path.Combine(pobRoot, "src", "Export", "Uniques", "ModTextMap.lua");
        Assert.True(File.Exists(path), $"Pinned ModTextMap missing at {path}");

        var index = PoBModTextMapParser.LoadFromFile(path);
        Assert.True(index.TryGetModifierIds(
            "(-16-16)% reduced Attack Speed",
            out var attackSpeedIds));
        Assert.Equal(["LocalIncreasedAttackSpeedUnique__38"], attackSpeedIds);
        Assert.True(index.TryGetModifierIds(
            "(-15-15)% reduced Mana Reservation Efficiency of Skills",
            out var reservationIds));
        Assert.Equal(["ManaReservationEfficiencyUnique__1"], reservationIds);
    }

    [Fact]
    public void Import_ConstrainedHalfRangeReduced_StillMatchesWithoutStraddleKey()
    {
        // Non-straddling negative rolls must keep the ordinary constrained Exact path.
        const string line = "(10-16)% reduced Attack Speed";
        var result = Import(
            """
                Test Half Sword
                Infernal Sword
                Implicits: 0
                (10-16)% reduced Attack Speed
                """,
            modifiers:
            [
                UniqueModifier("LocalReducedAttackSpeedUnique__1", "local_attack_speed_+%", -16, -10),
            ],
            translations:
            [
                AttackSpeedTranslation("local-attack-speed", "local_attack_speed_+%"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(line, Assert.Single(block.Lines));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["LocalReducedAttackSpeedUnique__1"], block.MechanicalMapping.ModifierIds);
    }

    private static StatTranslationDefinition AttackSpeedTranslation(string id, string statId) =>
        new()
        {
            Id = id,
            StatIds = [statId],
            Variants =
            [
                new StatTranslationVariant
                {
                    Conditions =
                    [
                        new StatTranslationCondition { Index = 0, MinValue = 1 },
                    ],
                    FormatLines = ["{0}% increased Attack Speed"],
                    ValueFormats = ["#"],
                    IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
                },
                new StatTranslationVariant
                {
                    Conditions =
                    [
                        new StatTranslationCondition { Index = 0, MaxValue = -1 },
                    ],
                    FormatLines = ["{0}% reduced Attack Speed"],
                    ValueFormats = ["#"],
                    IndexHandlers =
                    [
                        new StatTranslationIndexHandler
                        {
                            Index = 0,
                            Handlers = ["negate"],
                        },
                    ],
                },
            ],
        };

    private static StatTranslationDefinition ReservationEfficiencyTranslation(string id) =>
        new()
        {
            Id = id,
            StatIds = ["base_mana_reservation_efficiency_+%"],
            Variants =
            [
                new StatTranslationVariant
                {
                    Conditions =
                    [
                        new StatTranslationCondition { Index = 0, MinValue = 1 },
                    ],
                    FormatLines = ["{0}% increased Mana Reservation Efficiency of Skills"],
                    ValueFormats = ["#"],
                    IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
                },
                new StatTranslationVariant
                {
                    Conditions =
                    [
                        new StatTranslationCondition { Index = 0, MaxValue = -1 },
                    ],
                    FormatLines = ["{0}% reduced Mana Reservation Efficiency of Skills"],
                    ValueFormats = ["#"],
                    IndexHandlers =
                    [
                        new StatTranslationIndexHandler
                        {
                            Index = 0,
                            Handlers = ["negate"],
                        },
                    ],
                },
            ],
        };

    private static ModifierDefinition UniqueModifier(
        string id,
        string statId,
        decimal min,
        decimal max) => new()
    {
        Id = id,
        GroupId = id,
        Name = id,
        GenerationType = ModifierGenerationType.Implicit,
        SourceGenerationType = "unique",
        Domain = "item",
        Stats =
        [
            new ModifierStat
            {
                Index = 0,
                StatId = statId,
                MinValue = min,
                MaxValue = max,
            },
        ],
    };

    private static string CreateExportTree(string luaContents)
    {
        var root = Path.Combine(Path.GetTempPath(), $"poenhance-straddle-export-{Guid.NewGuid():N}");
        var uniques = Path.Combine(root, "src", "Export", "Uniques");
        Directory.CreateDirectory(uniques);
        File.WriteAllText(Path.Combine(uniques, "test.lua"), luaContents);
        return root;
    }

    private static PoBUniqueCatalogImportResult Import(
        string raw,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations,
        string? pobSourceRoot = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-straddle-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                entries = new[]
                {
                    new
                    {
                        uniqueType = "test",
                        sourcePath = "Data/Uniques/test.lua",
                        generated = false,
                        raw,
                    },
                },
            }));
            return new PoBUniqueCatalogImporter().Import(
                path,
                "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                "v2.67.2",
                "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                modifiers,
                translations,
                pobSourceRootPath: pobSourceRoot);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
