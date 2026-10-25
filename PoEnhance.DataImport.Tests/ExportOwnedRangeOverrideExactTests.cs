using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

/// <summary>
/// Export typed <c>ModId[min,max]</c> instance roll overrides → Exact mechanical mapping.
/// Fixtures use authentic ModifierId / StatId shapes from pinned PoB Export + RePoE;
/// synthetic negatives stay clearly marked.
/// </summary>
public sealed class ExportOwnedRangeOverrideExactTests
{
    private const string CritLine25_35 = "(25-35)% increased Global Critical Strike Chance";
    private const string CritLine40_60 = "(40-60)% increased Global Critical Strike Chance";
    private const string CritStatId = "critical_strike_chance_+%";
    private const string OwnedModId = "CriticalStrikeChanceUniqueGlovesStr3";

    [Fact]
    public void Parser_TypedOwner_CapturesTrailingRangeOverrides()
    {
        var index = PoBExportUniqueOwnershipParser.LoadFromPoBSourceRoot(CreateExportTree("""
            return {
            [[
            Replica Atziri's Acuity
            Vaal Gauntlets
            Variant: Current
            Implicits: 0
            CriticalStrikeChanceUniqueGlovesStr3[25,35]
            ]],
            }
            """));

        Assert.True(index.TryGetEntry("Replica Atziri's Acuity", out var entry));
        var line = Assert.Single(entry.EffectLines, effect => effect.IsTypedModifier);
        Assert.Equal(OwnedModId, line.ModifierId);
        Assert.Equal(
            [new PoBExportValueRangeOverride(25m, 35m)],
            line.ValueRangeOverrides.ToArray());

        Assert.True(PoBExportUniqueOwnershipParser.TryResolveVariantScope(
            entry, "Current", sourceVariantIndex: null, out var scope, out _));
        var overrideOwners = PoBExportUniqueOwnershipParser.GetTypedOwnerLinesWithRangeOverrides(
            entry,
            scope);
        Assert.Equal(OwnedModId, Assert.Single(overrideOwners).ModifierId);
    }

    [Fact]
    public void Parser_BareOwner_HasEmptyRangeOverrides()
    {
        var index = PoBExportUniqueOwnershipParser.LoadFromPoBSourceRoot(CreateExportTree("""
            return {
            [[
            Atziri's Acuity
            Vaal Gauntlets
            Variant: Current
            Implicits: 0
            CriticalStrikeChanceUniqueGlovesStr3
            ]],
            }
            """));

        Assert.True(index.TryGetEntry("Atziri's Acuity", out var entry));
        var line = Assert.Single(entry.EffectLines, effect => effect.IsTypedModifier);
        Assert.Empty(line.ValueRangeOverrides);
    }

    [Fact]
    public void Import_CurrentStaleExportOverride_25_35_ReconcilesToRePoEBase_40_60()
    {
        // Live/current Replica Crit is 40-60 (RePoE + PoEDB 3.16). Pinned PoB Export/Data still
        // carry legacy [25,35]/(25-35). Current must not Exact-package the stale override roll.
        var exportRoot = CreateExportTree("""
            return {
            [[
            Replica Atziri's Acuity
            Vaal Gauntlets
            Variant: Current
            Implicits: 0
            CriticalStrikeChanceUniqueGlovesStr3[25,35]
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            $"""
                Replica Atziri's Acuity
                Vaal Gauntlets
                Variant: Current
                Implicits: 0
                {CritLine25_35}
                """,
            modifiers:
            [
                UniqueModifier(OwnedModId, CritStatId, min: 40, max: 60),
                UniqueModifier(
                    "CriticalStrikeChanceUniqueAmulet17",
                    "critical_strike_chance_+%_from_amulet_fork",
                    min: 20,
                    max: 30),
                UniqueModifier(
                    "CriticalStrikeChanceUniqueWand3",
                    "critical_strike_chance_+%_from_wand_fork",
                    min: 10,
                    max: 20),
            ],
            translations:
            [
                Translation("crit", CritStatId, "{0}% increased Global Critical Strike Chance"),
                Translation(
                    "amulet_fork",
                    "critical_strike_chance_+%_from_amulet_fork",
                    "{0}% increased Global Critical Strike Chance"),
                Translation(
                    "wand_fork",
                    "critical_strike_chance_+%_from_wand_fork",
                    "{0}% increased Global Critical Strike Chance"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal([OwnedModId], block.MechanicalMapping.ModifierIds);
        Assert.Equal([CritStatId], block.MechanicalMapping.StatIds);
        Assert.Equal([CritLine40_60], block.Lines.ToArray());
        Assert.Null(block.MechanicalMapping.ConflictEvidence);
        Assert.Contains(
            PoBUniqueCatalogImporter.ExportOwnedCurrentRePoEBaseReconcileExactReason,
            block.MechanicalMapping.Provenance!.ResolutionReasons);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.ExportOwnedRangeOverrideExactReason,
            block.MechanicalMapping.Provenance.ResolutionReasons);
    }

    [Fact]
    public void Import_HistoricalExportOverride_25_35_KeepsOverrideExactWithoutRewriting()
    {
        // Historical Pre 3.16 roll may keep Export [25,35] Exact against RePoE base 40-60.
        var exportRoot = CreateExportTree("""
            return {
            [[
            Atziri's Acuity
            Vaal Gauntlets
            Variant: Pre 3.16.0
            Variant: Current
            Implicits: 0
            {variant:1}CriticalStrikeChanceUniqueGlovesStr3[25,35]
            {variant:2}CriticalStrikeChanceUniqueGlovesStr3
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            $"""
                Atziri's Acuity
                Vaal Gauntlets
                Variant: Pre 3.16.0
                Implicits: 0
                {CritLine25_35}
                """,
            modifiers:
            [
                UniqueModifier(OwnedModId, CritStatId, min: 40, max: 60),
                UniqueModifier(
                    "CriticalStrikeChanceUniqueAmulet17",
                    "critical_strike_chance_+%_from_amulet_fork",
                    min: 10,
                    max: 20),
            ],
            translations:
            [
                Translation("crit", CritStatId, "{0}% increased Global Critical Strike Chance"),
                Translation(
                    "amulet_fork",
                    "critical_strike_chance_+%_from_amulet_fork",
                    "{0}% increased Global Critical Strike Chance"),
            ]);

        var histBlock = result.Catalog!.Items.Single().Versions
            .SelectMany(version => version.ModifierBlocks)
            .Single(block => block.Lines.Any(line =>
                line.Contains("25-35", StringComparison.Ordinal)));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, histBlock.MechanicalMapping.Status);
        Assert.Equal([OwnedModId], histBlock.MechanicalMapping.ModifierIds);
        Assert.Equal([CritLine25_35], histBlock.Lines.ToArray());
        Assert.Contains(
            PoBUniqueCatalogImporter.ExportOwnedRangeOverrideExactReason,
            histBlock.MechanicalMapping.Provenance!.ResolutionReasons);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.ExportOwnedCurrentRePoEBaseReconcileExactReason,
            histBlock.MechanicalMapping.Provenance.ResolutionReasons);
    }

    [Fact]
    public void Import_BareExportOwner_40_60_RemainsExactWithoutOverridePath()
    {
        // Same ModId RePoE base roll (40-60) with bare Export owner — normal Exact index, no override.
        var exportRoot = CreateExportTree("""
            return {
            [[
            Atziri's Acuity
            Vaal Gauntlets
            Variant: Current
            Implicits: 0
            CriticalStrikeChanceUniqueGlovesStr3
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            $"""
                Atziri's Acuity
                Vaal Gauntlets
                Variant: Current
                Implicits: 0
                {CritLine40_60}
                """,
            modifiers:
            [
                UniqueModifier(OwnedModId, CritStatId, min: 40, max: 60),
                UniqueModifier("CriticalStrikeChanceUniqueAmulet17", CritStatId, min: 20, max: 30),
            ],
            translations:
            [
                Translation("crit", CritStatId, "{0}% increased Global Critical Strike Chance"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal([OwnedModId], block.MechanicalMapping.ModifierIds);
        Assert.Equal([CritStatId], block.MechanicalMapping.StatIds);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.ExportOwnedRangeOverrideExactReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_ExportRangeOverride_DoesNotApplyToUnrelatedBareDisplay()
    {
        // SYNTHETIC negative: Export override [25,35] must not claim a (40-60) Data line.
        var exportRoot = CreateExportTree("""
            return {
            [[
            Synthetic Crit Override Mismatch
            Vaal Gauntlets
            Variant: Current
            Implicits: 0
            CriticalStrikeChanceUniqueGlovesStr3[25,35]
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            $"""
                Synthetic Crit Override Mismatch
                Vaal Gauntlets
                Variant: Current
                Implicits: 0
                {CritLine40_60}
                """,
            modifiers:
            [
                UniqueModifier(OwnedModId, CritStatId, min: 40, max: 60),
                UniqueModifier("CriticalStrikeChanceUniqueAmulet17", CritStatId, min: 40, max: 60),
            ],
            translations:
            [
                Translation("crit", CritStatId, "{0}% increased Global Critical Strike Chance"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        // Base-roll Exact evidence for (40-60) may be Ambiguous (two ModIds) or Exact; never
        // claim the Export [25,35] override path for a non-matching display.
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.ExportOwnedRangeOverrideExactReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_NoExportOwnership_25_35_DoesNotClaimOverrideExact()
    {
        // SYNTHETIC negative: display 25-35 without Export ownership must not become override Exact.
        // Same StatId competitors yield EquivalentSourceSet (soft conflict), not Ambiguous.
        var result = ImportWithExport(
            pobSourceRoot: null,
            $"""
                Synthetic Crit No Export Owner
                Vaal Gauntlets
                Variant: Current
                Implicits: 0
                {CritLine25_35}
                """,
            modifiers:
            [
                UniqueModifier(OwnedModId, CritStatId, min: 40, max: 60),
                UniqueModifier("CriticalStrikeChanceUniqueAmulet17", CritStatId, min: 20, max: 30),
            ],
            translations:
            [
                Translation("crit", CritStatId, "{0}% increased Global Critical Strike Chance"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.NotEqual(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.ExportOwnedRangeOverrideExactReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_CompetingOverrideOwners_DoNotClaimSingularExact()
    {
        // SYNTHETIC negative: two Export owners with identical [25,35] overrides matching display.
        var exportRoot = CreateExportTree("""
            return {
            [[
            Synthetic Competing Crit Overrides
            Vaal Gauntlets
            Variant: Current
            Implicits: 0
            CriticalStrikeChanceUniqueGlovesStr3[25,35]
            CriticalStrikeChanceUniqueAmulet17[25,35]
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            $"""
                Synthetic Competing Crit Overrides
                Vaal Gauntlets
                Variant: Current
                Implicits: 0
                {CritLine25_35}
                """,
            modifiers:
            [
                UniqueModifier(OwnedModId, CritStatId, min: 40, max: 60),
                UniqueModifier("CriticalStrikeChanceUniqueAmulet17", CritStatId, min: 20, max: 30),
            ],
            translations:
            [
                Translation("crit", CritStatId, "{0}% increased Global Critical Strike Chance"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.NotEqual(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.ExportOwnedRangeOverrideExactReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_VariantScopedOverride_DoesNotLeakToBareVariant()
    {
        // Authentic Atziri shape: variant 2 has [25,35], variant 3 is bare 40-60.
        var exportRoot = CreateExportTree("""
            return {
            [[
            Atziri's Acuity
            Vaal Gauntlets
            Variant: Pre 3.1.0
            Variant: Current
            Implicits: 0
            {variant:1}CriticalStrikeChanceUniqueGlovesStr3[25,35]
            {variant:2}CriticalStrikeChanceUniqueGlovesStr3
            ]],
            }
            """);

        var current = ImportWithExport(
            exportRoot,
            $"""
                Atziri's Acuity
                Vaal Gauntlets
                Variant: Current
                Implicits: 0
                {CritLine40_60}
                """,
            modifiers:
            [
                UniqueModifier(OwnedModId, CritStatId, min: 40, max: 60),
            ],
            translations:
            [
                Translation("crit", CritStatId, "{0}% increased Global Critical Strike Chance"),
            ],
            deleteExportRoot: false);

        var currentBlock = Assert.Single(Assert.Single(Assert.Single(current.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, currentBlock.MechanicalMapping.Status);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.ExportOwnedRangeOverrideExactReason,
            currentBlock.MechanicalMapping.Provenance?.ResolutionReasons ?? []);

        var historical = ImportWithExport(
            exportRoot,
            $"""
                Atziri's Acuity
                Vaal Gauntlets
                Variant: Pre 3.1.0
                Implicits: 0
                {CritLine25_35}
                """,
            modifiers:
            [
                UniqueModifier(OwnedModId, CritStatId, min: 40, max: 60),
                UniqueModifier(
                    "CriticalStrikeChanceUniqueAmulet17",
                    "critical_strike_chance_+%_from_amulet_fork",
                    min: 10,
                    max: 20),
            ],
            translations:
            [
                Translation("crit", CritStatId, "{0}% increased Global Critical Strike Chance"),
                Translation(
                    "amulet_fork",
                    "critical_strike_chance_+%_from_amulet_fork",
                    "{0}% increased Global Critical Strike Chance"),
            ]);

        // Historical role may not use Current-only filters; override Exact is role-agnostic.
        var histVersions = historical.Catalog!.Items.Single().Versions;
        var histBlock = histVersions
            .SelectMany(version => version.ModifierBlocks)
            .Single(block => block.Lines.Any(line =>
                line.Contains("25-35", StringComparison.Ordinal)));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, histBlock.MechanicalMapping.Status);
        Assert.Equal([OwnedModId], histBlock.MechanicalMapping.ModifierIds);
        Assert.Contains(
            PoBUniqueCatalogImporter.ExportOwnedRangeOverrideExactReason,
            histBlock.MechanicalMapping.Provenance!.ResolutionReasons);
    }

    private static string CreateExportTree(string luaContents)
    {
        var root = Path.Combine(Path.GetTempPath(), $"poenhance-export-ror-{Guid.NewGuid():N}");
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
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-pob-ror-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                entries = new[]
                {
                    new
                    {
                        uniqueType = "armour",
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
                pobSourceRoot.Contains("poenhance-export-ror-", StringComparison.Ordinal) &&
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
