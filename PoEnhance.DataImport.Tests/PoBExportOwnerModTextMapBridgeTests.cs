using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class PoBExportOwnerModTextMapBridgeTests
{
    [Fact]
    public void Parser_PinnedModTextMap_LoadsKeysAndMultiModifierEntries()
    {
        var pobRoot = Path.Combine(Path.GetTempPath(), "PoEnhance-PoB-v2.67.2-b32759a");
        var path = Path.Combine(pobRoot, "src", "Export", "Uniques", "ModTextMap.lua");
        Assert.True(File.Exists(path), $"Pinned ModTextMap missing at {path}");

        var index = PoBModTextMapParser.LoadFromFile(path);
        Assert.True(index.EntryCount > 1000);
        Assert.True(index.MultiModifierEntryCount > 0);
        Assert.True(index.TryGetModifierIds(
            "Gain 3 Rage on Melee Weapon Hit",
            out var rageIds));
        Assert.Equal(["TinctureRageOnHitImplicit1"], rageIds);
        Assert.True(index.TryGetModifierIds(
            "Melee Weapon Attacks have Culling Strike",
            out var cullingIds));
        Assert.Equal(["TinctureCullingStrikeUnique__1"], cullingIds);
        Assert.True(index.TryGetModifierIds(
            "(1-5)% increased Rarity of Items found per Mana Burn, up to a maximum of 100%",
            out var rarityIds));
        Assert.Equal(["TinctureRarityPerToxicityUnique__1"], rarityIds);
    }

    [Fact]
    public void Import_UnsupportedBlock_ExportOwnerIntersectModTextMap_BecomesExact()
    {
        var root = CreateBridgeTree(
            """
            return {
            [[
            Test Bridge Unique
            Iron Ring
            Implicits: 0
            BridgeOwnedMod__1
            ]],
            }
            """,
            """
            return {
            	["gain 3 rage on melee weapon hit"] = { "BridgeOwnedMod__1", },
            }
            """);

        var result = ImportBridge(
            root,
            """
                Test Bridge Unique
                Iron Ring
                Implicits: 0
                Gain 3 Rage on Melee Weapon Hit
                """,
            [OwnedModifier("BridgeOwnedMod__1", "bridge_stat_a")]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["BridgeOwnedMod__1"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["bridge_stat_a"], block.MechanicalMapping.StatIds);
        Assert.Contains(
            PoBUniqueCatalogImporter.ExportOwnerModTextMapPositiveExactReason,
            block.MechanicalMapping.Provenance!.ResolutionReasons);
        Assert.Null(block.MechanicalMapping.DiagnosticCode);
    }

    [Fact]
    public void Import_ModTextMapMultiIds_WithoutSingleOwnedSurvivor_RemainsUnsupported()
    {
        var root = CreateBridgeTree(
            """
            return {
            [[
            Test Bridge Unique
            Iron Ring
            Implicits: 0
            BridgeOwnedMod__1
            BridgeOwnedMod__2
            ]],
            }
            """,
            """
            return {
            	["ambiguous line"] = { "BridgeOwnedMod__1", "BridgeOwnedMod__2", },
            }
            """);

        var result = ImportBridge(
            root,
            """
                Test Bridge Unique
                Iron Ring
                Implicits: 0
                ambiguous line
                """,
            [
                OwnedModifier("BridgeOwnedMod__1", "bridge_stat_a"),
                OwnedModifier("BridgeOwnedMod__2", "bridge_stat_b"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(
            UniqueModifierMechanicalMappingStatus.Unsupported,
            block.MechanicalMapping.Status);
        Assert.Equal("UNIQUE_MECHANICS_NOT_FOUND", block.MechanicalMapping.DiagnosticCode);
    }

    [Fact]
    public void Import_ForeignModTextMapOnly_RemainsUnsupported()
    {
        var root = CreateBridgeTree(
            """
            return {
            [[
            Test Bridge Unique
            Iron Ring
            Implicits: 0
            BridgeOwnedMod__1
            ]],
            }
            """,
            """
            return {
            	["foreign only line"] = { "ForeignMod__1", },
            }
            """);

        var result = ImportBridge(
            root,
            """
                Test Bridge Unique
                Iron Ring
                Implicits: 0
                foreign only line
                """,
            [
                OwnedModifier("BridgeOwnedMod__1", "bridge_stat_a"),
                OwnedModifier("ForeignMod__1", "foreign_stat"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(
            UniqueModifierMechanicalMappingStatus.Unsupported,
            block.MechanicalMapping.Status);
    }

    [Fact]
    public void Import_MissingStatIds_RemainsUnsupported()
    {
        var root = CreateBridgeTree(
            """
            return {
            [[
            Test Bridge Unique
            Iron Ring
            Implicits: 0
            BridgeOwnedMod__1
            ]],
            }
            """,
            """
            return {
            	["no stats line"] = { "BridgeOwnedMod__1", },
            }
            """);

        var emptyStats = OwnedModifier("BridgeOwnedMod__1", "bridge_stat_a") with
        {
            Stats = [],
        };
        var result = ImportBridge(
            root,
            """
                Test Bridge Unique
                Iron Ring
                Implicits: 0
                no stats line
                """,
            [emptyStats]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(
            UniqueModifierMechanicalMappingStatus.Unsupported,
            block.MechanicalMapping.Status);
    }

    [Fact]
    public void Import_DisabledModifier_RemainsUnsupported()
    {
        var root = CreateBridgeTree(
            """
            return {
            [[
            Test Bridge Unique
            Iron Ring
            Implicits: 0
            BridgeOwnedMod__1
            ]],
            }
            """,
            """
            return {
            	["disabled line"] = { "BridgeOwnedMod__1", },
            }
            """);

        var disabled = OwnedModifier("BridgeOwnedMod__1", "bridge_stat_a") with
        {
            SourceAvailability = ModifierSourceAvailability.Disabled,
        };
        var result = ImportBridge(
            root,
            """
                Test Bridge Unique
                Iron Ring
                Implicits: 0
                disabled line
                """,
            [disabled]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(
            UniqueModifierMechanicalMappingStatus.Unsupported,
            block.MechanicalMapping.Status);
    }

    [Fact]
    public void Import_VariantScope_DoesNotLeakHistoricalOwnerOntoCurrent()
    {
        var root = CreateBridgeTree(
            """
            return {
            [[
            Variant Bridge Unique
            Iron Ring
            Variant: Pre 3.0.0
            Variant: Current
            Implicits: 0
            {variant:1}HistoricalOwnedMod__1
            {variant:2}CurrentOwnedMod__1
            ]],
            }
            """,
            """
            return {
            	["shared display line"] = { "HistoricalOwnedMod__1", "CurrentOwnedMod__1", },
            }
            """);

        var result = ImportBridge(
            root,
            """
                Variant Bridge Unique
                Iron Ring
                Variant: Current
                Implicits: 0
                shared display line
                """,
            [
                OwnedModifier("HistoricalOwnedMod__1", "historical_stat"),
                OwnedModifier("CurrentOwnedMod__1", "current_stat"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["CurrentOwnedMod__1"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["current_stat"], block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_ReplicaOwnership_DoesNotLeakFromOriginal()
    {
        var root = CreateBridgeTree(
            """
            return {
            [[
            Bridge Original
            Iron Ring
            Implicits: 0
            OriginalOwnedMod__1
            ]],[[
            Replica Bridge Original
            Iron Ring
            Implicits: 0
            ReplicaOwnedMod__1
            ]],
            }
            """,
            """
            return {
            	["shared replica line"] = { "OriginalOwnedMod__1", "ReplicaOwnedMod__1", },
            }
            """);

        var result = ImportBridge(
            root,
            """
                Replica Bridge Original
                Iron Ring
                Implicits: 0
                shared replica line
                """,
            [
                OwnedModifier("OriginalOwnedMod__1", "original_stat"),
                OwnedModifier("ReplicaOwnedMod__1", "replica_stat"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["ReplicaOwnedMod__1"], block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_BattleWithinLike_RageCullingRarity_BecomeExact_AndSourceTextAndRemains()
    {
        const string manaFirst = "Does not inflict Mana Burn over time";
        const string manaSecond = "Inflicts Mana Burn on you when you Hit an Enemy with a Melee Weapon";
        var root = CreateBridgeTree(
            """
            return {
            [[
            The Battle Within
            Oakbranch Tincture
            Implicits: 1
            TinctureRageOnHitImplicit1
            TinctureToxicityOnHitUnique__1
            TinctureRarityPerToxicityUnique__1
            TinctureCullingStrikeUnique__1
            ]],
            }
            """,
            """
            return {
            	["gain 3 rage on melee weapon hit"] = { "TinctureRageOnHitImplicit1", },
            	["melee weapon attacks have culling strike"] = { "TinctureCullingStrikeUnique__1", },
            	["(1-5)% increased rarity of items found per mana burn, up to a maximum of 100%"] = { "TinctureRarityPerToxicityUnique__1", },
            	["does not inflict mana burn over time"] = { "TinctureToxicityOnHitUnique__1", },
            }
            """);

        var toxicity = OwnedModifier(
                "TinctureToxicityOnHitUnique__1",
                ("local_cannot_generate_toxicity_stacks_over_time", 1m, 1m),
                ("toxicity_stacks_gained_on_hit_with_tinctured_weapons", 1m, 1m)) with
        {
            SourceText = $"{manaFirst}\n{manaSecond}",
            Domain = "tincture",
        };
        var result = ImportBridge(
            root,
            $"""
                The Battle Within
                Oakbranch Tincture
                Implicits: 1
                Gain 3 Rage on Melee Weapon Hit
                {manaFirst}
                {manaSecond}
                Melee Weapon Attacks have Culling Strike
                (1-5)% increased Rarity of Items found per Mana Burn, up to a maximum of 100%
                """,
            [
                OwnedModifier("TinctureRageOnHitImplicit1", "gain_x_rage_on_hit_with_tinctured_weapons") with
                {
                    Domain = "tincture",
                },
                toxicity,
                OwnedModifier(
                    "TinctureRarityPerToxicityUnique__1",
                    "item_rarity_+%_per_toxicity_up_to_100%_from_tincture",
                    1,
                    5) with
                {
                    Domain = "tincture",
                },
                OwnedModifier("TinctureCullingStrikeUnique__1", "culling_strike_with_tinctured_weapons") with
                {
                    Domain = "tincture",
                },
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Tinctures/Tincture9",
                    Name = "Oakbranch Tincture",
                    ItemClass = "Tincture",
                    Domain = "tincture",
                },
            ]);

        var version = Assert.Single(Assert.Single(result.Catalog!.Items).Versions);
        Assert.Equal(4, version.ModifierBlocks.Count);

        var rage = Assert.Single(
            version.ModifierBlocks,
            block => block.Kind == UniqueModifierBlockKind.Implicit);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, rage.MechanicalMapping.Status);
        Assert.Equal(["TinctureRageOnHitImplicit1"], rage.MechanicalMapping.ModifierIds);
        Assert.Equal(["gain_x_rage_on_hit_with_tinctured_weapons"], rage.MechanicalMapping.StatIds);
        Assert.Contains(
            PoBUniqueCatalogImporter.ExportOwnerModTextMapPositiveExactReason,
            rage.MechanicalMapping.Provenance!.ResolutionReasons);

        var mana = Assert.Single(
            version.ModifierBlocks,
            block => block.Lines.Count == 2);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, mana.MechanicalMapping.Status);
        Assert.Equal(["TinctureToxicityOnHitUnique__1"], mana.MechanicalMapping.ModifierIds);
        Assert.Contains(
            "repoe-modifier-source-text",
            mana.MechanicalMapping.Provenance!.ResolutionReasons);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.ExportOwnerModTextMapPositiveExactReason,
            mana.MechanicalMapping.Provenance.ResolutionReasons);
        Assert.Equal(2, Assert.IsType<UniqueModifierComposition>(mana.Composition).Components.Count);

        var culling = Assert.Single(
            version.ModifierBlocks,
            block => block.Lines is ["Melee Weapon Attacks have Culling Strike"]);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, culling.MechanicalMapping.Status);
        Assert.Equal(["TinctureCullingStrikeUnique__1"], culling.MechanicalMapping.ModifierIds);
        Assert.Equal(["culling_strike_with_tinctured_weapons"], culling.MechanicalMapping.StatIds);

        var rarity = Assert.Single(
            version.ModifierBlocks,
            block => block.Lines[0].Contains("Rarity of Items found per Mana Burn", StringComparison.Ordinal));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, rarity.MechanicalMapping.Status);
        Assert.Equal(["TinctureRarityPerToxicityUnique__1"], rarity.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["item_rarity_+%_per_toxicity_up_to_100%_from_tincture"],
            rarity.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_SourceTextExact_IsNotRewrittenByBridge()
    {
        const string first = "Does not inflict Mana Burn over time";
        const string second = "Inflicts Mana Burn on you when you Hit an Enemy with a Melee Weapon";
        var root = CreateBridgeTree(
            """
            return {
            [[
            The Battle Within
            Oakbranch Tincture
            Implicits: 0
            TinctureToxicityOnHitUnique__1
            ]],
            }
            """,
            """
            return {
            	["does not inflict mana burn over time"] = { "TinctureToxicityOnHitUnique__1", },
            }
            """);
        var toxicity = OwnedModifier(
                "TinctureToxicityOnHitUnique__1",
                ("local_cannot_generate_toxicity_stacks_over_time", 1m, 1m),
                ("toxicity_stacks_gained_on_hit_with_tinctured_weapons", 1m, 1m)) with
        {
            SourceText = $"{first}\n{second}",
            Domain = "tincture",
        };
        var result = ImportBridge(
            root,
            $"""
                The Battle Within
                Oakbranch Tincture
                Implicits: 0
                {first}
                {second}
                """,
            [toxicity],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Tinctures/Tincture9",
                    Name = "Oakbranch Tincture",
                    ItemClass = "Tincture",
                    Domain = "tincture",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Contains(
            "repoe-modifier-source-text",
            block.MechanicalMapping.Provenance!.ResolutionReasons);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.ExportOwnerModTextMapPositiveExactReason,
            block.MechanicalMapping.Provenance.ResolutionReasons);
    }

    private static string CreateBridgeTree(string exportLua, string modTextMapLua)
    {
        var root = Path.Combine(Path.GetTempPath(), $"poenhance-modtext-bridge-{Guid.NewGuid():N}");
        var uniques = Path.Combine(root, "src", "Export", "Uniques");
        Directory.CreateDirectory(uniques);
        File.WriteAllText(Path.Combine(uniques, "test.lua"), exportLua);
        File.WriteAllText(Path.Combine(uniques, "ModTextMap.lua"), modTextMapLua);
        return root;
    }

    private static PoBUniqueCatalogImportResult ImportBridge(
        string pobSourceRoot,
        string raw,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<ItemBaseRecord>? baseItems = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-modtext-bridge-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                entries = new[]
                {
                    new
                    {
                        uniqueType = "tincture",
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
                translations: [],
                baseItems: baseItems,
                pobSourceRootPath: pobSourceRoot);
        }
        finally
        {
            File.Delete(path);
            if (Directory.Exists(pobSourceRoot))
            {
                Directory.Delete(pobSourceRoot, recursive: true);
            }
        }
    }

    private static ModifierDefinition OwnedModifier(
        string id,
        string statId,
        decimal min = 1,
        decimal max = 1) => new()
    {
        Id = id,
        GroupId = id,
        Name = id,
        GenerationType = ModifierGenerationType.Implicit,
        SourceGenerationType = "unique",
        Domain = "item",
        Stats = [new ModifierStat { Index = 0, StatId = statId, MinValue = min, MaxValue = max }],
    };

    private static ModifierDefinition OwnedModifier(
        string id,
        params (string StatId, decimal Min, decimal Max)[] stats) => new()
    {
        Id = id,
        GroupId = id,
        Name = id,
        GenerationType = ModifierGenerationType.Implicit,
        SourceGenerationType = "unique",
        Domain = "item",
        Stats = stats.Select((stat, index) => new ModifierStat
        {
            Index = index,
            StatId = stat.StatId,
            MinValue = stat.Min,
            MaxValue = stat.Max,
        }).ToArray(),
    };
}
