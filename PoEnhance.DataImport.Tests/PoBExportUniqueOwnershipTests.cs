using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class PoBExportUniqueOwnershipTests
{
    [Fact]
    public void Parser_PinnedExportUniques_LoadsTypedOwnershipManyToMany()
    {
        var pobRoot = Path.Combine(
            Path.GetTempPath(),
            "PoEnhance-PoB-v2.67.2-b32759a");
        Assert.True(
            Directory.Exists(Path.Combine(pobRoot, "src", "Export", "Uniques")),
            $"Pinned PoB Export Uniques missing at {pobRoot}");

        var index = PoBExportUniqueOwnershipParser.LoadFromPoBSourceRoot(pobRoot);
        Assert.True(index.EntryCount > 1000);

        Assert.True(index.TryGetEntry("Kongor's Undying Rage", out var kongor));
        Assert.Contains(
            kongor.EffectLines,
            line => line.IsTypedModifier &&
                string.Equals(line.ModifierId, "NoBonusesFromCriticalStrikes", StringComparison.Ordinal));
        Assert.DoesNotContain(
            kongor.EffectLines,
            line => line.IsTypedModifier &&
                string.Equals(line.ModifierId, "CriticalMultiplierUniqueAmulet18", StringComparison.Ordinal));

        Assert.True(index.TryGetEntry("Replica Kongor's Undying Rage", out var replica));
        Assert.Contains(
            replica.EffectLines,
            line => line.IsTypedModifier &&
                string.Equals(line.ModifierId, "CriticalMultiplierUniqueAmulet18", StringComparison.Ordinal));

        Assert.True(index.TryGetEntry("Ungil's Harmony", out var ungil));
        Assert.Contains(
            ungil.EffectLines,
            line => line.IsTypedModifier &&
                string.Equals(line.ModifierId, "CriticalMultiplierUniqueAmulet18", StringComparison.Ordinal));

        Assert.True(index.IsOwnedByOtherUnique("CriticalMultiplierUniqueAmulet18", "Kongor's Undying Rage"));
        Assert.True(index.IsOwnedByOtherUnique("NoBonusesFromCriticalStrikes", "Ungil's Harmony"));
        // Shared across Kongor + Emberwake
        Assert.True(index.IsOwnedByOtherUnique("NoBonusesFromCriticalStrikes", "Kongor's Undying Rage"));
        // Shared across Ungil + Replica Kongor
        Assert.True(index.IsOwnedByOtherUnique("CriticalMultiplierUniqueAmulet18", "Ungil's Harmony"));
        Assert.True(index.IsOwnedByOtherUnique("CriticalMultiplierUniqueAmulet18", "Replica Kongor's Undying Rage"));
    }

    [Fact]
    public void Parser_VariantScope_DoesNotLeakAcrossVariants()
    {
        var index = PoBExportUniqueOwnershipParser.LoadFromPoBSourceRoot(CreateExportTree("""
            return {
            [[
            Emberwake
            Ruby Ring
            Variant: Pre 3.0.0
            Variant: Current
            Implicits: 0
            {variant:1}NoBonusesFromCriticalStrikes
            {variant:2}FireDamagePercentUniqueRing38
            ]],
            }
            """));

        Assert.True(index.TryGetEntry("Emberwake", out var entry));
        Assert.True(PoBExportUniqueOwnershipParser.TryResolveVariantScope(
            entry, "Pre 3.0.0", sourceVariantIndex: 1, out var historical, out _));
        Assert.Equal(
            ["NoBonusesFromCriticalStrikes"],
            PoBExportUniqueOwnershipParser.GetTypedOwnerModifierIds(entry, historical)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());

        Assert.True(PoBExportUniqueOwnershipParser.TryResolveVariantScope(
            entry, "Current", sourceVariantIndex: 2, out var current, out _));
        Assert.Equal(
            ["FireDamagePercentUniqueRing38"],
            PoBExportUniqueOwnershipParser.GetTypedOwnerModifierIds(entry, current)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());
        Assert.DoesNotContain(
            "NoBonusesFromCriticalStrikes",
            PoBExportUniqueOwnershipParser.GetTypedOwnerModifierIds(entry, current));
    }

    [Fact]
    public void Import_CrossItemSameDisplay_FiltersToOwnedModifier()
    {
        var exportRoot = CreateExportTree("""
            return {
            [[
            Kongor's Undying Rage
            Terror Maul
            Variant: Current
            Implicits: 0
            NoBonusesFromCriticalStrikes
            ]],[[
            Ungil's Harmony
            Turquoise Amulet
            Variant: Current
            Implicits: 0
            CriticalMultiplierUniqueAmulet18
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
                UniqueModifier("CriticalMultiplierUniqueAmulet18", "crit_multi_amulet"),
                UniqueModifier("CriticalMultiplierIs100UniqueRing38", "crit_is_100"),
            ],
            translations:
            [
                Translation("a", "no_crit_bonus", "Your Critical Strikes do not deal extra Damage"),
                Translation("b", "crit_multi_amulet", "Your Critical Strikes do not deal extra Damage"),
                Translation("c", "crit_is_100", "Your Critical Strikes do not deal extra Damage"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["NoBonusesFromCriticalStrikes"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["no_crit_bonus"], block.MechanicalMapping.StatIds);
        Assert.Contains(
            PoBUniqueCatalogImporter.ExportUniqueOwnershipFilterReason,
            block.MechanicalMapping.Provenance!.ResolutionReasons);
        Assert.Null(block.MechanicalMapping.ConflictEvidence);
    }

    [Fact]
    public void Import_ReplicaOwnership_DoesNotLeakFromOriginal()
    {
        var exportRoot = CreateExportTree("""
            return {
            [[
            Kongor's Undying Rage
            Terror Maul
            Variant: Current
            Implicits: 0
            NoBonusesFromCriticalStrikes
            ]],[[
            Replica Kongor's Undying Rage
            Terror Maul
            Implicits: 0
            CriticalMultiplierUniqueAmulet18
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            """
                Replica Kongor's Undying Rage
                Terror Maul
                Implicits: 0
                Your Critical Strikes do not deal extra Damage
                """,
            modifiers:
            [
                UniqueModifier("NoBonusesFromCriticalStrikes", "no_crit_bonus"),
                UniqueModifier("CriticalMultiplierUniqueAmulet18", "crit_multi_amulet"),
                UniqueModifier("CriticalMultiplierIs100UniqueRing38", "crit_is_100"),
            ],
            translations:
            [
                Translation("a", "no_crit_bonus", "Your Critical Strikes do not deal extra Damage"),
                Translation("b", "crit_multi_amulet", "Your Critical Strikes do not deal extra Damage"),
                Translation("c", "crit_is_100", "Your Critical Strikes do not deal extra Damage"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["CriticalMultiplierUniqueAmulet18"], block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_SharedModifier_RemainsEligibleForEveryOwner()
    {
        var exportRoot = CreateExportTree("""
            return {
            [[
            Kongor's Undying Rage
            Terror Maul
            Variant: Current
            Implicits: 0
            NoBonusesFromCriticalStrikes
            ]],[[
            Replica Kongor's Undying Rage
            Terror Maul
            Implicits: 0
            CriticalMultiplierUniqueAmulet18
            ]],[[
            Ungil's Harmony
            Turquoise Amulet
            Variant: Current
            Implicits: 0
            CriticalMultiplierUniqueAmulet18
            ]],
            }
            """);

        try
        {
            foreach (var name in new[] { "Replica Kongor's Undying Rage", "Ungil's Harmony" })
            {
                var raw = name == "Ungil's Harmony"
                    ? """
                        Ungil's Harmony
                        Turquoise Amulet
                        Variant: Current
                        Implicits: 0
                        Your Critical Strikes do not deal extra Damage
                        """
                    : """
                        Replica Kongor's Undying Rage
                        Terror Maul
                        Implicits: 0
                        Your Critical Strikes do not deal extra Damage
                        """;
                var result = ImportWithExport(
                    exportRoot,
                    raw,
                    modifiers:
                    [
                        UniqueModifier("NoBonusesFromCriticalStrikes", "no_crit_bonus"),
                        UniqueModifier("CriticalMultiplierUniqueAmulet18", "crit_multi_amulet"),
                    ],
                    translations:
                    [
                        Translation("a", "no_crit_bonus", "Your Critical Strikes do not deal extra Damage"),
                        Translation("b", "crit_multi_amulet", "Your Critical Strikes do not deal extra Damage"),
                    ],
                    deleteExportRoot: false);

                var item = Assert.Single(result.Catalog!.Items, candidate => candidate.CanonicalName == name);
                var block = Assert.Single(Assert.Single(item.Versions).ModifierBlocks);
                Assert.Equal(["CriticalMultiplierUniqueAmulet18"], block.MechanicalMapping.ModifierIds);
            }
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
    public void Import_SameItemOrphanFork_RemainsExactConflict()
    {
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
                UniqueModifier("CurseOnHitCriticalWeaknessUnique__1", "mark_a"),
                UniqueModifier("CurseOnHitCriticalWeaknessUniqueNewUnique__1", "mark_b"),
                UniqueModifier("DivergentCurseOnHitCriticalWeaknessUniqueNewUnique__1", "mark_c"),
            ],
            translations:
            [
                Translation("a", "mark_a",
                    "Trigger Level 10 Assassin's Mark when you Hit a Rare or Unique Enemy and have no Mark"),
                Translation("b", "mark_b",
                    "Trigger Level 10 Assassin's Mark when you Hit a Rare or Unique Enemy and have no Mark"),
                Translation("c", "mark_c",
                    "Trigger Level 10 Assassin's Mark when you Hit a Rare or Unique Enemy and have no Mark"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.MechanicalMapping.DiagnosticCode);
        Assert.Equal(3, block.MechanicalMapping.ConflictEvidence!.Candidates.Count);
        Assert.Null(block.MechanicalMapping.Provenance);
    }

    [Fact]
    public void Import_NoOwnedCandidate_DoesNotFilter()
    {
        var exportRoot = CreateExportTree("""
            return {
            [[
            Reckless Defence
            Cobalt Jewel
            Variant: Current
            Implicits: 0
            SpellBlockPercentageUnique__1
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
                UniqueModifier("ChanceToBeCritJewelUnique__1", "crit_a", 140, 200),
                UniqueModifier("ChanceToBeCritJewelUpdatedUnique__1", "crit_b", 140, 200),
            ],
            translations:
            [
                Translation("a", "crit_a",
                    "Hits have {0}% increased Critical Strike Chance against you", "#"),
                Translation("b", "crit_b",
                    "Hits have {0}% increased Critical Strike Chance against you", "#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.NotNull(block.MechanicalMapping.ConflictEvidence);
        Assert.Equal(2, block.MechanicalMapping.ConflictEvidence!.Candidates.Count);
    }

    [Fact]
    public void Import_ResidualTextExportLine_DoesNotFilter()
    {
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
                UniqueModifier("MaximumMinionCountUniqueBodyInt9", "spectre_a"),
                UniqueModifier("MaximumMinionCountUniqueSceptre5", "spectre_b"),
            ],
            translations:
            [
                Translation("a", "spectre_a", "{0} to maximum number of Spectres", "+#"),
                Translation("b", "spectre_b", "{0} to maximum number of Spectres", "+#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal(2, block.MechanicalMapping.ConflictEvidence!.Candidates.Count);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.ExportUniqueOwnershipFilterReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_AbsentOwnershipIndex_PreservesCandidates()
    {
        var result = ImportWithExport(
            pobSourceRoot: null,
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
                UniqueModifier("CriticalMultiplierUniqueAmulet18", "crit_multi_amulet"),
            ],
            translations:
            [
                Translation("a", "no_crit_bonus", "Your Critical Strikes do not deal extra Damage"),
                Translation("b", "crit_multi_amulet", "Your Critical Strikes do not deal extra Damage"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal(2, block.MechanicalMapping.ConflictEvidence!.Candidates.Count);
    }

    private static string CreateExportTree(string luaContents)
    {
        var root = Path.Combine(Path.GetTempPath(), $"poenhance-export-own-{Guid.NewGuid():N}");
        var uniques = Path.Combine(root, "src", "Export", "Uniques");
        Directory.CreateDirectory(uniques);
        File.WriteAllText(Path.Combine(uniques, "test.lua"), luaContents);
        return root;
    }

    private static PoBUniqueCatalogImportResult ImportWithExport(
        string? pobSourceRoot,
        string raw,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations,
        bool deleteExportRoot = true)
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-pob-own-{Guid.NewGuid():N}.json");
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
            if (deleteExportRoot &&
                pobSourceRoot is not null &&
                pobSourceRoot.Contains("poenhance-export-own-", StringComparison.Ordinal) &&
                Directory.Exists(pobSourceRoot))
            {
                Directory.Delete(pobSourceRoot, recursive: true);
            }
        }
    }

    private static ModifierDefinition UniqueModifier(
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

    private static StatTranslationDefinition Translation(
        string id,
        string statId,
        string format,
        params string[] valueFormats) => new()
    {
        Id = id,
        StatIds = [statId],
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions = [new StatTranslationCondition { Index = 0 }],
                FormatLines = [format],
                ValueFormats = valueFormats.Length == 0 ? ["#"] : valueFormats,
                IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
            },
        ],
    };
}
