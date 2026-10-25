using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

/// <summary>
/// SameDisplay ExactConflict → singular PoB Export typed owner collapse.
/// Fixtures use authentic ModifierId / StatId shapes from pinned PoB Export + RePoE;
/// synthetic negatives stay clearly marked.
/// </summary>
public sealed class SameDisplayExportOwnerCollapseTests
{
    [Fact]
    public void Import_OskarmAssassinsMark_CollapsesToExportOwner()
    {
        // Authentic Export owner CurseOnHitCriticalWeaknessUnique__1 vs New/Divergent orphans.
        // Pinned RePoE trade_stats for both StatIds share the same Trade filter set.
        const string line =
            "Trigger Level 10 Assassin's Mark when you Hit a Rare or Unique Enemy and have no Mark";
        var exportRoot = CreateExportTree("""
            return {
            [[
            Oskarm
            Nubuck Gloves
            Variant: Current
            Implicits: 0
            CurseOnHitCriticalWeaknessUnique__1
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            """
                Oskarm
                Nubuck Gloves
                Variant: Current
                Implicits: 0
                Trigger Level 10 Assassin's Mark when you Hit a Rare or Unique Enemy and have no Mark
                """,
            modifiers:
            [
                UniqueModifier(
                    "CurseOnHitCriticalWeaknessUnique__1",
                    "curse_on_hit_level_assassins_mark"),
                UniqueModifier(
                    "CurseOnHitCriticalWeaknessUniqueNewUnique__1",
                    "local_display_trigger_level_X_assassins_mark_when_you_hit_rare_or_unique_enemy"),
                UniqueModifier(
                    "DivergentCurseOnHitCriticalWeaknessUniqueNewUnique__1",
                    "local_display_trigger_level_X_assassins_mark_when_you_hit_rare_or_unique_enemy"),
            ],
            translations:
            [
                Translation("owner", "curse_on_hit_level_assassins_mark", line),
                Translation(
                    "orphan_new",
                    "local_display_trigger_level_X_assassins_mark_when_you_hit_rare_or_unique_enemy",
                    line),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["CurseOnHitCriticalWeaknessUnique__1"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["curse_on_hit_level_assassins_mark"], block.MechanicalMapping.StatIds);
        Assert.Null(block.MechanicalMapping.ConflictEvidence);
        Assert.Contains(
            PoBUniqueCatalogImporter.CurrentSameDisplayExportOwnerCollapseReason,
            block.MechanicalMapping.Provenance!.ResolutionReasons);
    }

    [Fact]
    public void Import_RecklessDefenceScaleHandler_CollapsesToExportOwner()
    {
        // Authentic Export owner ChanceToBeCritJewelUnique__1 (20_times_value) vs Updated orphan.
        // Both StatIds publish identical official Trade filter IDs in pinned RePoE trade_stats.
        var exportRoot = CreateExportTree("""
            return {
            [[
            Reckless Defence
            Cobalt Jewel
            Variant: Current
            Implicits: 0
            ChanceToBeCritJewelUnique__1
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            """
                Reckless Defence
                Cobalt Jewel
                Variant: Current
                Implicits: 0
                Hits have (140-200)% increased Critical Strike Chance against you
                """,
            modifiers:
            [
                UniqueModifier(
                    "ChanceToBeCritJewelUnique__1",
                    "enemy_critical_strike_chance_+%_against_self_20_times_value",
                    min: 7,
                    max: 10),
                UniqueModifier(
                    "ChanceToBeCritJewelUpdatedUnique__1",
                    "base_enemy_critical_strike_chance_+%_against_self",
                    min: 140,
                    max: 200),
            ],
            translations:
            [
                Translation(
                    "scale",
                    "enemy_critical_strike_chance_+%_against_self_20_times_value",
                    "Hits have {0}% increased Critical Strike Chance against you",
                    "#",
                    "times_twenty"),
                Translation(
                    "updated",
                    "base_enemy_critical_strike_chance_+%_against_self",
                    "Hits have {0}% increased Critical Strike Chance against you",
                    "#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["ChanceToBeCritJewelUnique__1"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["enemy_critical_strike_chance_+%_against_self_20_times_value"],
            block.MechanicalMapping.StatIds);
        Assert.Contains(
            PoBUniqueCatalogImporter.CurrentSameDisplayExportOwnerCollapseReason,
            block.MechanicalMapping.Provenance!.ResolutionReasons);
    }

    [Fact]
    public void Import_PoachersAimPierce_CollapsesToExportOwner()
    {
        // Authentic Export owner AdditionalPierceUniqueJewel__1 vs PierceChance orphan.
        // Both StatIds share Trade filter explicit.stat_2067062068 in pinned RePoE trade_stats.
        // Isolated fixture: Poacher is the sole typed Export owner (no cross-Unique share).
        const string line = "Projectiles Pierce an additional Target";
        var exportRoot = CreateExportTree("""
            return {
            [[
            Poacher's Aim
            Viridian Jewel
            Variant: Current
            Implicits: 0
            AdditionalPierceUniqueJewel__1
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            """
                Poacher's Aim
                Viridian Jewel
                Variant: Current
                Implicits: 0
                Projectiles Pierce an additional Target
                """,
            modifiers:
            [
                UniqueModifier(
                    "AdditionalPierceUniqueJewel__1",
                    "projectile_base_number_of_targets_to_pierce"),
                UniqueModifier(
                    "PierceChanceUniqueJewel41",
                    "projectiles_pierce_1_additional_target_per_15_stat_value",
                    min: 15,
                    max: 15),
            ],
            translations:
            [
                Translation("pierce", "projectile_base_number_of_targets_to_pierce", line),
                Translation(
                    "per15",
                    "projectiles_pierce_1_additional_target_per_15_stat_value",
                    line),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["AdditionalPierceUniqueJewel__1"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["projectile_base_number_of_targets_to_pierce"],
            block.MechanicalMapping.StatIds);
        Assert.Contains(
            PoBUniqueCatalogImporter.CurrentSameDisplayExportOwnerCollapseReason,
            block.MechanicalMapping.Provenance!.ResolutionReasons);
    }

    [Fact]
    public void Import_PoachersAimPierce_CrossUniqueOwner_RemainsFailClosed()
    {
        // Negative (pinned-shape): AdditionalPierceUniqueJewel__1 is also typed on
        // The Devouring Diadem variant 28 (+2 Proj Gems + Pierce). Softening cross-Unique for
        // "global vs variant-scoped" is NOT safe: pinned Export has 373 mixed shared ModIds.
        // Official Trade (live 2026-10-25 and frozen Aug-19 fixture) has no exact label
        // "Projectiles Pierce an additional Target" — only "Pierce # additional Targets"
        // (explicit.stat_2067062068). Exact-text Trade identity therefore cannot authorize Exact.
        // Keep Ambiguous fail-closed; Standard ownership of a real jewel does not change provenance.
        const string line = "Projectiles Pierce an additional Target";
        var exportRoot = CreateExportTree("""
            return {
            [[
            Poacher's Aim
            Viridian Jewel
            Source: No longer obtainable
            Implicits: 0
            ProjectileDamageJewelUniqueJewel41
            AdditionalPierceUniqueJewel__1
            ]],[[
            The Devouring Diadem
            Necromancer Circlet
            Variant: Other Craft
            Variant: Pierce Craft
            Implicits: 0
            {variant:1}IncreasedManaUnique__16
            {variant:2}AdditionalPierceUniqueJewel__1
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            """
                Poacher's Aim
                Viridian Jewel
                Source: No longer obtainable
                Implicits: 0
                Projectiles Pierce an additional Target
                10% increased Projectile Damage
                """,
            modifiers:
            [
                UniqueModifier(
                    "AdditionalPierceUniqueJewel__1",
                    "projectile_base_number_of_targets_to_pierce"),
                UniqueModifier(
                    "PierceChanceUniqueJewel41",
                    "projectiles_pierce_1_additional_target_per_15_stat_value",
                    min: 15,
                    max: 15),
                UniqueModifier("ProjectileDamageJewelUniqueJewel41", "projectile_damage_+%", min: 10, max: 10),
                UniqueModifier("IncreasedManaUnique__16", "base_maximum_mana", min: 1, max: 1),
            ],
            translations:
            [
                Translation("pierce", "projectile_base_number_of_targets_to_pierce", line),
                Translation(
                    "per15",
                    "projectiles_pierce_1_additional_target_per_15_stat_value",
                    line),
                Translation(
                    "proj",
                    "projectile_damage_+%",
                    "{0}% increased Projectile Damage",
                    "#"),
                Translation("mana", "base_maximum_mana", "{0} to maximum Mana", "+#"),
            ]);

        var poacher = Assert.Single(
            result.Catalog!.Items,
            item => string.Equals(item.CanonicalName, "Poacher's Aim", StringComparison.Ordinal));
        var pierce = Assert.Single(
            Assert.Single(poacher.Versions).ModifierBlocks,
            block => block.Lines.Any(l =>
                l.Contains("Pierce an additional Target", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, pierce.MechanicalMapping.Status);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", pierce.MechanicalMapping.DiagnosticCode);
        Assert.Equal(
            UniqueMechanicalConflictKind.SameDisplayTextDifferentStatIds,
            pierce.MechanicalMapping.ConflictEvidence!.Kind);
        Assert.Equal(
            ["AdditionalPierceUniqueJewel__1", "PierceChanceUniqueJewel41"],
            pierce.MechanicalMapping.ModifierIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.CurrentSameDisplayExportOwnerCollapseReason,
            pierce.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_BitterbindResidualText_RemainsFailClosed()
    {
        // Negative: PoB Data/Export model Catarina craft outcomes as Unique variant lines.
        // Variant 19 is residual display text (no typed ModId; Current spectre lines lack
        // {crafted} even though in-game they appear as Explicit/Prefix). Competing Unique-
        // generation vectors differ (multi-minion BodyInt9/Sceptre5 vs spectre-only Talisman)
        // so status stays Ambiguous — English identity alone must not collapse to Exact, and
        // the line must not be silently demoted to Unsupported to clear the scanner.
        var exportRoot = CreateExportTree("""
            return {
            [[
            Bitterbind Point
            Titanium Spirit Shield
            Variant: Maximum Number of Spectres (Current)
            Implicits: 0
            {variant:1}+1 to maximum number of Spectres
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            """
                Bitterbind Point
                Titanium Spirit Shield
                Variant: Maximum Number of Spectres (Current)
                Selected Variant: 1
                Implicits: 0
                +1 to maximum number of Spectres
                """,
            modifiers:
            [
                new ModifierDefinition
                {
                    Id = "MaximumMinionCountUniqueBodyInt9",
                    GroupId = "MaximumMinionCountUniqueBodyInt9",
                    Name = "MaximumMinionCountUniqueBodyInt9",
                    GenerationType = ModifierGenerationType.Implicit,
                    SourceGenerationType = "unique",
                    Domain = "item",
                    Stats =
                    [
                        new ModifierStat { Index = 0, StatId = "base_number_of_zombies_allowed", MinValue = 0, MaxValue = 0 },
                        new ModifierStat { Index = 1, StatId = "base_number_of_skeletons_allowed", MinValue = 0, MaxValue = 0 },
                        new ModifierStat { Index = 2, StatId = "base_number_of_spectres_allowed", MinValue = 1, MaxValue = 1 },
                    ],
                },
                new ModifierDefinition
                {
                    Id = "MaximumMinionCountUniqueSceptre5",
                    GroupId = "MaximumMinionCountUniqueSceptre5",
                    Name = "MaximumMinionCountUniqueSceptre5",
                    GenerationType = ModifierGenerationType.Implicit,
                    SourceGenerationType = "unique",
                    Domain = "item",
                    Stats =
                    [
                        new ModifierStat { Index = 0, StatId = "base_number_of_zombies_allowed", MinValue = 0, MaxValue = 0 },
                        new ModifierStat { Index = 1, StatId = "base_number_of_skeletons_allowed", MinValue = 0, MaxValue = 0 },
                        new ModifierStat { Index = 2, StatId = "base_number_of_spectres_allowed", MinValue = 1, MaxValue = 1 },
                    ],
                },
                UniqueModifier(
                    "TalismanEnchantMaximumSpectreCount",
                    "base_number_of_spectres_allowed"),
            ],
            translations:
            [
                Translation(
                    "spectres",
                    "base_number_of_spectres_allowed",
                    "{0} to maximum number of Spectres",
                    "+#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.MechanicalMapping.DiagnosticCode);
        Assert.Equal(
            UniqueMechanicalConflictKind.SameDisplayTextDifferentStatIds,
            block.MechanicalMapping.ConflictEvidence!.Kind);
        Assert.Equal(
            [
                "MaximumMinionCountUniqueBodyInt9",
                "MaximumMinionCountUniqueSceptre5",
                "TalismanEnchantMaximumSpectreCount",
            ],
            block.MechanicalMapping.ModifierIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.CurrentSameDisplayExportOwnerCollapseReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_BitterbindResidual_DoesNotExactClaimUnveiledCatarinaPrefix()
    {
        // Negative: JunMasterVeiledMaximumSpectreCount (domain=unveiled, Catarina's) shares the
        // spectre display text and matches in-game Explicit/Prefix searchability
        // (explicit.stat_125218179), but Fixed Unique blocks must not Exact-claim unveiled rows
        // from English alone. Residual Export still leaves Unique-generation ExactConflict.
        const string format = "{0} to maximum number of Spectres";
        var exportRoot = CreateExportTree("""
            return {
            [[
            Bitterbind Point
            Titanium Spirit Shield
            Variant: Maximum Number of Spectres (Current)
            Implicits: 0
            {variant:1}+1 to maximum number of Spectres
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            """
                Bitterbind Point
                Titanium Spirit Shield
                Variant: Maximum Number of Spectres (Current)
                Selected Variant: 1
                Implicits: 0
                +1 to maximum number of Spectres
                """,
            modifiers:
            [
                new ModifierDefinition
                {
                    Id = "MaximumMinionCountUniqueBodyInt9",
                    GroupId = "MaximumMinionCount",
                    Name = "MaximumMinionCountUniqueBodyInt9",
                    GenerationType = ModifierGenerationType.Implicit,
                    SourceGenerationType = "unique",
                    Domain = "item",
                    Stats =
                    [
                        new ModifierStat { Index = 0, StatId = "base_number_of_zombies_allowed", MinValue = 0, MaxValue = 0 },
                        new ModifierStat { Index = 1, StatId = "base_number_of_skeletons_allowed", MinValue = 0, MaxValue = 0 },
                        new ModifierStat { Index = 2, StatId = "base_number_of_spectres_allowed", MinValue = 1, MaxValue = 1 },
                    ],
                },
                new ModifierDefinition
                {
                    Id = "MaximumMinionCountUniqueSceptre5",
                    GroupId = "MaximumMinionCount",
                    Name = "MaximumMinionCountUniqueSceptre5",
                    GenerationType = ModifierGenerationType.Implicit,
                    SourceGenerationType = "unique",
                    Domain = "item",
                    Stats =
                    [
                        new ModifierStat { Index = 0, StatId = "base_number_of_zombies_allowed", MinValue = 0, MaxValue = 0 },
                        new ModifierStat { Index = 1, StatId = "base_number_of_skeletons_allowed", MinValue = 0, MaxValue = 0 },
                        new ModifierStat { Index = 2, StatId = "base_number_of_spectres_allowed", MinValue = 1, MaxValue = 1 },
                    ],
                },
                UniqueModifier("TalismanEnchantMaximumSpectreCount", "base_number_of_spectres_allowed"),
                new ModifierDefinition
                {
                    Id = "JunMasterVeiledMaximumSpectreCount",
                    GroupId = "MaximumSpectreCount",
                    Name = "Catarina's",
                    GenerationType = ModifierGenerationType.Prefix,
                    SourceGenerationType = "prefix",
                    Domain = "unveiled",
                    Stats =
                    [
                        new ModifierStat
                        {
                            Index = 0,
                            StatId = "base_number_of_spectres_allowed",
                            MinValue = 1,
                            MaxValue = 1,
                        },
                    ],
                },
            ],
            translations:
            [
                Translation("spectres", "base_number_of_spectres_allowed", format, "+#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.MechanicalMapping.DiagnosticCode);
        Assert.DoesNotContain(
            "JunMasterVeiledMaximumSpectreCount",
            block.MechanicalMapping.ModifierIds);
        Assert.Equal(
            [
                "MaximumMinionCountUniqueBodyInt9",
                "MaximumMinionCountUniqueSceptre5",
                "TalismanEnchantMaximumSpectreCount",
            ],
            block.MechanicalMapping.ModifierIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.CurrentSameDisplayExportOwnerCollapseReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.GeneratedOptionDomainExactReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_PassageChromeBlock_DefersToPassageRule()
    {
        // Negative: Passage chrome present → SameDisplay Export-owner collapse must not fire.
        const string allocateLine =
            "Passive Skills in Radius can be Allocated without being connected to your tree";
        var exportRoot = CreateExportTree("""
            return {
            [[
            Thread of Hope
            Crimson Jewel
            Variant: Current
            Implicits: 0
            JewelUniqueAllocateDisconnectedPassives
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            """
                Thread of Hope
                Crimson Jewel
                Variant: Current
                Implicits: 0
                Passive Skills in Radius can be Allocated without being connected to your tree
                Passage
                """,
            modifiers:
            [
                new ModifierDefinition
                {
                    Id = "JewelUniqueAllocateDisconnectedPassives",
                    GroupId = "JewelUniqueAllocateDisconnectedPassives",
                    Name = "JewelUniqueAllocateDisconnectedPassives",
                    GenerationType = ModifierGenerationType.Implicit,
                    SourceGenerationType = "unique",
                    Domain = "item",
                    Stats =
                    [
                        new ModifierStat { Index = 0, StatId = "alloc", MinValue = 1, MaxValue = 1 },
                        new ModifierStat { Index = 1, StatId = "radius", MinValue = 800, MaxValue = 800 },
                    ],
                },
                UniqueModifier("AllocateDisconnectedPassivesDonutUnique__1", "other"),
            ],
            translations:
            [
                new StatTranslationDefinition
                {
                    Id = "passage-tid",
                    StatIds = ["alloc"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0 }],
                            FormatLines = [allocateLine, "Passage"],
                            ValueFormats = ["#"],
                            IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
                        },
                    ],
                },
                new StatTranslationDefinition
                {
                    Id = "passage-tid-b",
                    StatIds = ["other"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0 }],
                            FormatLines = [allocateLine, "Passage"],
                            ValueFormats = ["#"],
                            IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
                        },
                    ],
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.CurrentSameDisplayExportOwnerCollapseReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_SyntheticMultipleOwnedCandidates_RemainsFailClosed()
    {
        // Synthetic negative: two Export-typed owners among SameDisplay candidates.
        const string line = "Projectiles Pierce an additional Target";
        var exportRoot = CreateExportTree("""
            return {
            [[
            Synthetic Pierce Jewel
            Viridian Jewel
            Variant: Current
            Implicits: 0
            AdditionalPierceUniqueJewel__1
            PierceChanceUniqueJewel41
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            """
                Synthetic Pierce Jewel
                Viridian Jewel
                Variant: Current
                Implicits: 0
                Projectiles Pierce an additional Target
                """,
            modifiers:
            [
                UniqueModifier(
                    "AdditionalPierceUniqueJewel__1",
                    "projectile_base_number_of_targets_to_pierce"),
                UniqueModifier(
                    "PierceChanceUniqueJewel41",
                    "projectiles_pierce_1_additional_target_per_15_stat_value",
                    min: 15,
                    max: 15),
            ],
            translations:
            [
                Translation("a", "projectile_base_number_of_targets_to_pierce", line),
                Translation(
                    "b",
                    "projectiles_pierce_1_additional_target_per_15_stat_value",
                    line),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.CurrentSameDisplayExportOwnerCollapseReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    private static string CreateExportTree(string luaContents)
    {
        var root = Path.Combine(Path.GetTempPath(), $"poenhance-export-sde-{Guid.NewGuid():N}");
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
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-pob-sde-{Guid.NewGuid():N}.json");
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
                pobSourceRoot.Contains("poenhance-export-sde-", StringComparison.Ordinal) &&
                Directory.Exists(pobSourceRoot))
            {
                Directory.Delete(pobSourceRoot, recursive: true);
            }
        }
    }

    private static ModifierDefinition UniqueModifier(
        string id,
        string statId,
        int min = 1,
        int max = 1) => new()
    {
        Id = id,
        GroupId = id,
        Name = id,
        GenerationType = ModifierGenerationType.Implicit,
        SourceGenerationType = "unique",
        Domain = "item",
        Stats = [new ModifierStat { Index = 0, StatId = statId, MinValue = min, MaxValue = max }],
    };

    private static StatTranslationDefinition Translation(
        string id,
        string statId,
        string format,
        string valueFormat = "#",
        string? handler = null) => new()
    {
        Id = id,
        StatIds = [statId],
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions = [new StatTranslationCondition { Index = 0 }],
                FormatLines = [format],
                ValueFormats = [valueFormat],
                IndexHandlers =
                [
                    new StatTranslationIndexHandler
                    {
                        Index = 0,
                        Handlers = handler is null ? [] : [handler],
                    },
                ],
            },
        ],
    };
}
