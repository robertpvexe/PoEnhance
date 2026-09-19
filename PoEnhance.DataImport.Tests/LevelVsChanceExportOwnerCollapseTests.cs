using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class LevelVsChanceExportOwnerCollapseTests
{
    [Theory]
    [InlineData(
        "Asenath's Gentle Touch",
        "Silk Gloves",
        "Curse Enemies with Temporal Chains on Hit",
        "TemporalChainsOnHitUniqueGlovesInt3",
        "curse_on_hit_%_temporal_chains",
        "curse_on_hit_level_temporal_chains")]
    [InlineData(
        "Dreadarc",
        "Cleaver",
        "Curse Enemies with Flammability on Hit",
        "FlammabilityOnHitUniqueOneHandAxe7",
        "curse_on_hit_%_flammability",
        "curse_on_hit_level_flammability")]
    [InlineData(
        "Uul-Netol's Kiss",
        "Labrys",
        "Curse Enemies with Vulnerability on Hit",
        "CurseLevel10VulnerabilityOnHitUnique__1",
        "curse_on_hit_level_10_vulnerability_%",
        "curse_on_hit_%_vulnerability")]
    public void Import_CurrentLevelVsChance_CollapsesToSingularExportOwner(
        string uniqueName,
        string baseType,
        string line,
        string ownerModifierId,
        string ownerStatId,
        string competitorStatId)
    {
        var exportRoot = CreateExportTree(
            "return {\n[[\n" +
            uniqueName + "\n" +
            baseType + "\n" +
            "Variant: Current\n" +
            "Implicits: 0\n" +
            ownerModifierId + "\n" +
            "]],\n}\n");

        var result = ImportWithExport(
            exportRoot,
            uniqueName + "\n" +
            baseType + "\n" +
            "Variant: Current\n" +
            "Implicits: 0\n" +
            line,
            modifiers:
            [
                UniqueModifier(ownerModifierId, ownerStatId),
                UniqueModifier($"{ownerModifierId}_OrphanLevel", competitorStatId),
                UniqueModifier($"Synthesis{ownerModifierId}", competitorStatId),
                UniqueModifier($"Mutated{ownerModifierId}", ownerStatId),
            ],
            translations:
            [
                Translation("owner", ownerStatId, line),
                Translation("orphan", competitorStatId, line),
                Translation("synth", competitorStatId, line),
                Translation("mutated", ownerStatId, line),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal([ownerModifierId], block.MechanicalMapping.ModifierIds);
        Assert.Equal([ownerStatId], block.MechanicalMapping.StatIds);
        Assert.Null(block.MechanicalMapping.ConflictEvidence);
        Assert.Contains(
            PoBUniqueCatalogImporter.CurrentLevelVsChanceExportOwnerCollapseReason,
            block.MechanicalMapping.Provenance!.ResolutionReasons);
        Assert.Contains(block.SourceObservationIds, id => !string.IsNullOrWhiteSpace(id));
        Assert.False(string.IsNullOrWhiteSpace(block.Id));
    }

    [Fact]
    public void Import_LevelVsChance_ZeroExportOwners_RemainsFailClosed()
    {
        const string line = "Curse Enemies with Temporal Chains on Hit";
        var result = ImportWithExport(
            pobSourceRoot: null,
            raw: """
                Test Gloves
                Silk Gloves
                Variant: Current
                Implicits: 0
                Curse Enemies with Temporal Chains on Hit
                """,
            modifiers:
            [
                UniqueModifier("LevelCandidate", "curse_on_hit_level_temporal_chains"),
                UniqueModifier("ChanceCandidate", "curse_on_hit_%_temporal_chains"),
            ],
            translations:
            [
                Translation("level", "curse_on_hit_level_temporal_chains", line),
                Translation("chance", "curse_on_hit_%_temporal_chains", line),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal(
            UniqueMechanicalConflictKind.LevelVsChanceOnHit,
            block.MechanicalMapping.ConflictEvidence!.Kind);
        Assert.Null(block.MechanicalMapping.Provenance);
    }

    [Fact]
    public void Import_LevelVsChance_MultipleExportOwners_RemainsFailClosed()
    {
        const string line = "Curse Enemies with Temporal Chains on Hit";
        var exportRoot = CreateExportTree("""
            return {
            [[
            Test Gloves
            Silk Gloves
            Variant: Current
            Implicits: 0
            TemporalChainsOnHitUniqueGlovesInt3
            CurseOnHitTemporalChainsUnique__1
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            """
                Test Gloves
                Silk Gloves
                Variant: Current
                Implicits: 0
                Curse Enemies with Temporal Chains on Hit
                """,
            modifiers:
            [
                UniqueModifier("TemporalChainsOnHitUniqueGlovesInt3", "curse_on_hit_%_temporal_chains"),
                UniqueModifier("CurseOnHitTemporalChainsUnique__1", "curse_on_hit_level_temporal_chains"),
            ],
            translations:
            [
                Translation("chance", "curse_on_hit_%_temporal_chains", line),
                Translation("level", "curse_on_hit_level_temporal_chains", line),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal(
            UniqueMechanicalConflictKind.LevelVsChanceOnHit,
            block.MechanicalMapping.ConflictEvidence!.Kind);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.CurrentLevelVsChanceExportOwnerCollapseReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_LevelVsChance_HistoricalOnlyExportOwner_DoesNotAuthorizeCurrentExact()
    {
        const string line = "Curse Enemies with Temporal Chains on Hit";
        var exportRoot = CreateExportTree("""
            return {
            [[
            Test Gloves
            Silk Gloves
            Variant: Pre 3.0.0
            Variant: Current
            Implicits: 0
            {variant:1}TemporalChainsOnHitUniqueGlovesInt3
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            """
                Test Gloves
                Silk Gloves
                Variant: Current
                Implicits: 0
                Curse Enemies with Temporal Chains on Hit
                """,
            modifiers:
            [
                UniqueModifier("TemporalChainsOnHitUniqueGlovesInt3", "curse_on_hit_%_temporal_chains"),
                UniqueModifier("OrphanLevel", "curse_on_hit_level_temporal_chains"),
            ],
            translations:
            [
                Translation("chance", "curse_on_hit_%_temporal_chains", line),
                Translation("level", "curse_on_hit_level_temporal_chains", line),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal(
            UniqueMechanicalConflictKind.LevelVsChanceOnHit,
            block.MechanicalMapping.ConflictEvidence!.Kind);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.CurrentLevelVsChanceExportOwnerCollapseReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_LevelVsChance_OwnerWithoutStatIds_RemainsFailClosed()
    {
        const string line = "Curse Enemies with Temporal Chains on Hit";
        var exportRoot = CreateExportTree("""
            return {
            [[
            Test Gloves
            Silk Gloves
            Variant: Current
            Implicits: 0
            TemporalChainsOnHitUniqueGlovesInt3
            ]],
            }
            """);

        var ownerWithoutStats = UniqueModifier("TemporalChainsOnHitUniqueGlovesInt3", "unused_stat");
        ownerWithoutStats = ownerWithoutStats with { Stats = [] };

        var result = ImportWithExport(
            exportRoot,
            """
                Test Gloves
                Silk Gloves
                Variant: Current
                Implicits: 0
                Curse Enemies with Temporal Chains on Hit
                """,
            modifiers:
            [
                ownerWithoutStats,
                UniqueModifier("OrphanLevel", "curse_on_hit_level_temporal_chains"),
                UniqueModifier("OrphanChance", "curse_on_hit_%_temporal_chains"),
            ],
            translations:
            [
                Translation("level", "curse_on_hit_level_temporal_chains", line),
                Translation("chance", "curse_on_hit_%_temporal_chains", line),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.CurrentLevelVsChanceExportOwnerCollapseReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_OtherConflictKind_DoesNotApplyLevelVsChanceCollapse()
    {
        const string line = "Your Critical Strikes do not deal extra Damage";
        var exportRoot = CreateExportTree("""
            return {
            [[
            Kongor's Undying Rage
            Terror Maul
            Variant: Current
            Implicits: 0
            NoBonusesFromCriticalStrikes
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            """
                Kongor's Undying Rage
                Terror Maul
                Variant: Current
                Implicits: 0
                Your Critical Strikes do not deal extra Damage
                """,
            modifiers:
            [
                UniqueModifier("NoBonusesFromCriticalStrikes", "no_crit_bonus"),
                UniqueModifier("OrphanCritFork", "crit_orphan_stat"),
            ],
            translations:
            [
                Translation("a", "no_crit_bonus", line),
                Translation("b", "crit_orphan_stat", line),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal(
            UniqueMechanicalConflictKind.SameDisplayTextDifferentStatIds,
            block.MechanicalMapping.ConflictEvidence!.Kind);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.CurrentLevelVsChanceExportOwnerCollapseReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_LevelVsChance_ForeignOwnedCandidate_DoesNotBecomeExact()
    {
        const string line = "Curse Enemies with Temporal Chains on Hit";
        var exportRoot = CreateExportTree("""
            return {
            [[
            Test Gloves
            Silk Gloves
            Variant: Current
            Implicits: 0
            ]],[[
            Foreign Touch
            Silk Gloves
            Variant: Current
            Implicits: 0
            TemporalChainsOnHitUniqueGlovesInt3
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            """
                Test Gloves
                Silk Gloves
                Variant: Current
                Implicits: 0
                Curse Enemies with Temporal Chains on Hit
                """,
            modifiers:
            [
                UniqueModifier("TemporalChainsOnHitUniqueGlovesInt3", "curse_on_hit_%_temporal_chains"),
                UniqueModifier("OrphanLevel", "curse_on_hit_level_temporal_chains"),
            ],
            translations:
            [
                Translation("chance", "curse_on_hit_%_temporal_chains", line),
                Translation("level", "curse_on_hit_level_temporal_chains", line),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal(
            UniqueMechanicalConflictKind.LevelVsChanceOnHit,
            block.MechanicalMapping.ConflictEvidence!.Kind);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.CurrentLevelVsChanceExportOwnerCollapseReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    private static string CreateExportTree(string luaContents)
    {
        var root = Path.Combine(Path.GetTempPath(), $"poenhance-export-lvc-{Guid.NewGuid():N}");
        var uniques = Path.Combine(root, "src", "Export", "Uniques");
        Directory.CreateDirectory(uniques);
        File.WriteAllText(Path.Combine(uniques, "test.lua"), luaContents);
        return root;
    }

    private static PoBUniqueCatalogImportResult ImportWithExport(
        string? pobSourceRoot,
        string raw,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations)
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-pob-lvc-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                entries = new[]
                {
                    new
                    {
                        uniqueType = "weapon",
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
            File.Delete(path);
            if (pobSourceRoot is not null &&
                pobSourceRoot.Contains("poenhance-export-lvc-", StringComparison.Ordinal) &&
                Directory.Exists(pobSourceRoot))
            {
                Directory.Delete(pobSourceRoot, recursive: true);
            }
        }
    }

    private static ModifierDefinition UniqueModifier(string id, string statId) => new()
    {
        Id = id,
        GroupId = id,
        Name = id,
        GenerationType = ModifierGenerationType.Implicit,
        SourceGenerationType = "unique",
        Domain = "item",
        Stats = [new ModifierStat { Index = 0, StatId = statId, MinValue = 1, MaxValue = 1 }],
    };

    private static StatTranslationDefinition Translation(
        string id,
        string statId,
        string format) => new()
    {
        Id = id,
        StatIds = [statId],
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions = [new StatTranslationCondition { Index = 0 }],
                FormatLines = [format],
                ValueFormats = ["#"],
                IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
            },
        ],
    };
}
