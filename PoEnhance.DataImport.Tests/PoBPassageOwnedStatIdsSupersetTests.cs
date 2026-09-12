using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class PoBPassageOwnedStatIdsSupersetTests
{
    private const string AllocateLine =
        "Passive Skills in Radius can be Allocated without being connected to your tree";

    [Fact]
    public void PassageDisplayChrome_RequiresBlockAndSharedTranslationFormatLine()
    {
        Assert.True(PoBUniqueCatalogImporter.IsPassageDisplayChromeLine("Passage"));
        Assert.False(PoBUniqueCatalogImporter.IsPassageDisplayChromeLine("passage"));
        Assert.False(PoBUniqueCatalogImporter.IsPassageDisplayChromeLine("Only affects Passives in Small Ring"));

        var sharedEvidence = new PoBUniqueCatalogImporter.PassageTranslationFormatEvidence[]
        {
            new("shared-tid", [AllocateLine, "Passage"]),
        };
        Assert.True(PoBUniqueCatalogImporter.HasPassageDisplayChromeEvidence(
            [AllocateLine, "Passage"],
            [sharedEvidence, sharedEvidence],
            out var sharedIds));
        Assert.Equal(["shared-tid"], sharedIds);

        Assert.False(PoBUniqueCatalogImporter.HasPassageDisplayChromeEvidence(
            [AllocateLine],
            [sharedEvidence, sharedEvidence],
            out _));

        Assert.False(PoBUniqueCatalogImporter.HasPassageDisplayChromeEvidence(
            [AllocateLine, "Passage"],
            [
                [new("tid-a", [AllocateLine, "Passage"])],
                [new("tid-b", [AllocateLine, "Passage"])],
            ],
            out _));

        Assert.False(PoBUniqueCatalogImporter.HasPassageDisplayChromeEvidence(
            [AllocateLine, "Passage"],
            [
                [new("shared-tid", [AllocateLine, "Passage"])],
                [new("shared-tid", [AllocateLine])],
            ],
            out _));
    }

    [Fact]
    public void ProperStatIdsSubset_RequiresStrictProperSubset()
    {
        Assert.True(PoBUniqueCatalogImporter.IsProperStatIdsSubset(
            ["alloc"],
            ["alloc", "radius"]));
        Assert.False(PoBUniqueCatalogImporter.IsProperStatIdsSubset(
            ["alloc", "radius"],
            ["alloc", "radius"]));
        Assert.False(PoBUniqueCatalogImporter.IsProperStatIdsSubset(
            ["alloc", "other"],
            ["alloc", "radius"]));
        Assert.False(PoBUniqueCatalogImporter.IsProperStatIdsSubset(
            [],
            ["alloc", "radius"]));
        Assert.False(PoBUniqueCatalogImporter.IsProperStatIdsSubset(
            ["alloc"],
            []));
    }

    [Fact]
    public void Import_PassageOwnedSuperset_CollapsesToExactOwnedComposite()
    {
        var exportRoot = CreateExportTree("""
            return {
            [[
            Thread of Hope
            Crimson Jewel
            Variant: Small Ring
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
                Variant: Small Ring
                Implicits: 0
                Passive Skills in Radius can be Allocated without being connected to your tree
                Passage
                """,
            modifiers:
            [
                UniqueModifier(
                    "JewelUniqueAllocateDisconnectedPassives",
                    ("alloc", 1, 1),
                    ("radius", 800, 800)) with
                {
                    SourceText = $"{AllocateLine}\nPassage",
                },
                UniqueModifier(
                    "AllocateDisconnectedPassivesDonutUnique__1",
                    ("alloc", 1, 1)) with
                {
                    SourceText = $"{AllocateLine}\nPassage",
                },
            ],
            translations:
            [
                PassageTranslation("passage-tid", "alloc", AllocateLine),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(
            ["JewelUniqueAllocateDisconnectedPassives"],
            block.MechanicalMapping.ModifierIds);
        Assert.Equal(["alloc", "radius"], block.MechanicalMapping.StatIds);
        Assert.Contains(
            PoBUniqueCatalogImporter.PassageOwnedStatIdsSupersetCollapseReason,
            block.MechanicalMapping.Provenance!.ResolutionReasons);
        Assert.DoesNotContain(
            "source-block-composition",
            block.MechanicalMapping.Provenance.ResolutionReasons);
        Assert.Null(block.MechanicalMapping.ConflictEvidence);
        // Passage is display chrome — do not synthesize Composition that would bind it to radius.
        Assert.Null(block.Composition);
    }

    [Fact]
    public void Import_PassageOwnedSuperset_DoesNotMergeSeparateRingLine()
    {
        var exportRoot = CreateExportTree("""
            return {
            [[
            Thread of Hope
            Crimson Jewel
            Variant: Large Ring
            Implicits: 0
            JewelRingRadiusValuesUnique__1
            JewelUniqueAllocateDisconnectedPassives
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            """
                Thread of Hope
                Crimson Jewel
                Variant: Large Ring
                Implicits: 0
                Only affects Passives in Large Ring
                Passive Skills in Radius can be Allocated without being connected to your tree
                Passage
                """,
            modifiers:
            [
                UniqueModifier(
                    "JewelUniqueAllocateDisconnectedPassives",
                    ("alloc", 1, 1),
                    ("radius", 800, 800)),
                UniqueModifier(
                    "AllocateDisconnectedPassivesDonutUnique__1",
                    ("alloc", 1, 1)),
                UniqueModifier(
                    "JewelRingRadiusValuesUnique__1",
                    ("ring", 1, 4)),
            ],
            translations:
            [
                PassageTranslation("passage-tid", "alloc", AllocateLine),
                Translation("ring-tid", "ring", "Only affects Passives in Large Ring"),
            ]);

        var blocks = Assert.Single(Assert.Single(result.Catalog!.Items).Versions).ModifierBlocks;
        Assert.Equal(2, blocks.Count);

        var ring = Assert.Single(
            blocks,
            block => block.Lines.Count == 1 &&
                block.Lines[0] == "Only affects Passives in Large Ring");
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, ring.MechanicalMapping.Status);
        Assert.Equal(["JewelRingRadiusValuesUnique__1"], ring.MechanicalMapping.ModifierIds);
        Assert.Equal(["ring"], ring.MechanicalMapping.StatIds);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.PassageOwnedStatIdsSupersetCollapseReason,
            ring.MechanicalMapping.Provenance?.ResolutionReasons ?? []);

        var passage = Assert.Single(
            blocks,
            block => block.Lines.Any(line => line == "Passage"));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, passage.MechanicalMapping.Status);
        Assert.Equal(
            ["JewelUniqueAllocateDisconnectedPassives"],
            passage.MechanicalMapping.ModifierIds);
        Assert.Equal(["alloc", "radius"], passage.MechanicalMapping.StatIds);
        Assert.Contains(
            PoBUniqueCatalogImporter.PassageOwnedStatIdsSupersetCollapseReason,
            passage.MechanicalMapping.Provenance!.ResolutionReasons);
    }

    [Fact]
    public void Import_MultipleOwnedCandidates_RemainsExactConflict()
    {
        var exportRoot = CreateExportTree("""
            return {
            [[
            Thread of Hope
            Crimson Jewel
            Variant: Current
            Implicits: 0
            JewelUniqueAllocateDisconnectedPassives
            AllocateDisconnectedPassivesDonutUnique__1
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
                UniqueModifier(
                    "JewelUniqueAllocateDisconnectedPassives",
                    ("alloc", 1, 1),
                    ("radius", 800, 800)),
                UniqueModifier(
                    "AllocateDisconnectedPassivesDonutUnique__1",
                    ("alloc", 1, 1)),
            ],
            translations:
            [
                PassageTranslation("passage-tid", "alloc", AllocateLine),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal(2, block.MechanicalMapping.ConflictEvidence!.Candidates.Count);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.PassageOwnedStatIdsSupersetCollapseReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_EqualVectors_DoesNotUsePassageSupersetRule()
    {
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
                UniqueModifier(
                    "JewelUniqueAllocateDisconnectedPassives",
                    ("alloc", 1, 1),
                    ("radius", 800, 800)),
                UniqueModifier(
                    "AllocateDisconnectedPassivesDonutUnique__1",
                    ("alloc", 1, 1),
                    ("radius", 800, 800)),
            ],
            translations:
            [
                PassageTranslation("passage-tid", "alloc", AllocateLine),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        // Equal StatIds vectors collapse via existing EquivalentSourceSet, not Passage rule.
        Assert.Equal(
            UniqueModifierMechanicalMappingStatus.EquivalentSourceSet,
            block.MechanicalMapping.Status);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.PassageOwnedStatIdsSupersetCollapseReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_DisjointVectors_RemainExactConflict()
    {
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
                UniqueModifier(
                    "JewelUniqueAllocateDisconnectedPassives",
                    ("alloc", 1, 1),
                    ("radius", 800, 800)),
                UniqueModifier(
                    "AllocateDisconnectedPassivesDonutUnique__1",
                    ("other", 1, 1)),
            ],
            translations:
            [
                PassageTranslation("passage-tid", "alloc", AllocateLine),
                PassageTranslation("passage-tid-b", "other", AllocateLine),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.NotNull(block.MechanicalMapping.ConflictEvidence);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.PassageOwnedStatIdsSupersetCollapseReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_PartialPassageEvidence_RemainsExactConflict()
    {
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

        // Block has Passage, but translations do not — Passage chrome evidence incomplete.
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
                UniqueModifier(
                    "JewelUniqueAllocateDisconnectedPassives",
                    ("alloc", 1, 1),
                    ("radius", 800, 800)),
                UniqueModifier(
                    "AllocateDisconnectedPassivesDonutUnique__1",
                    ("alloc", 1, 1)),
            ],
            translations:
            [
                Translation("no-passage-tid", "alloc", AllocateLine),
            ]);

        var blocks = Assert.Single(Assert.Single(result.Catalog!.Items).Versions).ModifierBlocks;
        Assert.Equal(2, blocks.Count);

        // Without Passage FormatLines, the chrome line stays unmatched and the allocate line
        // remains a SameDisplay ExactConflict — never Passage-superset collapsed.
        var chrome = Assert.Single(blocks, block => block.Lines.SequenceEqual(["Passage"]));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Unsupported, chrome.MechanicalMapping.Status);

        var allocate = Assert.Single(
            blocks,
            block => block.Lines.SequenceEqual([AllocateLine]));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, allocate.MechanicalMapping.Status);
        Assert.Equal(2, allocate.MechanicalMapping.ConflictEvidence!.Candidates.Count);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.PassageOwnedStatIdsSupersetCollapseReason,
            allocate.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_NonPassageSubset_VisMortisShape_RemainsExactConflict()
    {
        var exportRoot = CreateExportTree("""
            return {
            [[
            Vis Mortis
            Necromancer Silks
            Variant: Pre 2.6.0
            Implicits: 0
            MaximumMinionCountUniqueBodyInt9
            ]],
            }
            """);

        var result = ImportWithExport(
            exportRoot,
            """
                Vis Mortis
                Necromancer Silks
                Variant: Pre 2.6.0
                Implicits: 0
                +1 to maximum number of Spectres
                """,
            modifiers:
            [
                UniqueModifier(
                    "MaximumMinionCountUniqueBodyInt9",
                    ("zombies", 1, 1),
                    ("skeletons", 1, 1),
                    ("spectres", 1, 1)),
                UniqueModifier(
                    "TalismanEnchantMaximumSpectreCount",
                    ("spectres", 1, 1)),
            ],
            translations:
            [
                Translation("tid", "spectres", "{0} to maximum number of Spectres", "+#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.True(block.MechanicalMapping.ConflictEvidence!.Candidates.Count >= 2);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.PassageOwnedStatIdsSupersetCollapseReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_BitterbindResidual_RemainsExactConflict()
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
                UniqueModifier(
                    "MaximumMinionCountUniqueBodyInt9",
                    ("zombies", 1, 1),
                    ("skeletons", 1, 1),
                    ("spectres", 1, 1)),
                UniqueModifier(
                    "TalismanEnchantMaximumSpectreCount",
                    ("spectres", 1, 1)),
            ],
            translations:
            [
                Translation("tid", "spectres", "{0} to maximum number of Spectres", "+#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.PassageOwnedStatIdsSupersetCollapseReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    [Fact]
    public void Import_RecklessScaleHandler_RemainsExactConflict()
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
                UniqueModifier("ChanceToBeCritJewelUnique__1", ("crit_a", 140, 200)),
                UniqueModifier("ChanceToBeCritJewelUpdatedUnique__1", ("crit_b", 140, 200)),
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
        Assert.DoesNotContain(
            PoBUniqueCatalogImporter.PassageOwnedStatIdsSupersetCollapseReason,
            block.MechanicalMapping.Provenance?.ResolutionReasons ?? []);
    }

    private static string CreateExportTree(string luaContents)
    {
        var root = Path.Combine(Path.GetTempPath(), $"poenhance-export-passage-{Guid.NewGuid():N}");
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
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-pob-passage-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                entries = new[]
                {
                    new
                    {
                        uniqueType = "jewel",
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
                pobSourceRoot.Contains("poenhance-export-passage-", StringComparison.Ordinal) &&
                Directory.Exists(pobSourceRoot))
            {
                Directory.Delete(pobSourceRoot, recursive: true);
            }
        }
    }

    private static ModifierDefinition UniqueModifier(
        string id,
        params (string StatId, decimal Min, decimal Max)[] stats) => new()
    {
        Id = id,
        GroupId = id,
        Name = id,
        GenerationType = ModifierGenerationType.Implicit,
        SourceGenerationType = "unique",
        Domain = "item",
        Stats = stats
            .Select((stat, index) => new ModifierStat
            {
                Index = index,
                StatId = stat.StatId,
                MinValue = stat.Min,
                MaxValue = stat.Max,
            })
            .ToArray(),
    };

    private static StatTranslationDefinition PassageTranslation(
        string id,
        string statId,
        string allocateLine) => new()
    {
        Id = id,
        StatIds = [statId],
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions = [new StatTranslationCondition { Index = 0 }],
                FormatLines = [allocateLine, "Passage"],
                ValueFormats = ["ignore"],
                IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
            },
        ],
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
