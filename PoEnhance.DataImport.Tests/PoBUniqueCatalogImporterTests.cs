using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class PoBUniqueCatalogImporterTests
{
    [Fact]
    public void Import_ExactUniqueGenerationAndRange_WinsBeforeConflictingNormalizedSignature()
    {
        var result = ImportSingle(
            """
                Test Diamond
                Diamond Ring
                Implicits: 0
                (80-120)% increased Global Critical Strike Chance
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.critical", "unique_critical", 80, 120, "unique"),
                Modifier("ordinary.critical", "ordinary_critical", 10, 14, "prefix"),
            ],
            translations:
            [
                Translation("unique-critical", "unique_critical",
                    "{0}% increased Global Critical Strike Chance", "#"),
                Translation("ordinary-critical", "ordinary_critical",
                    "{0}% increased Global Critical Strike Chance", "#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.critical"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["unique_critical"], block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_MechanicallyEquivalentExactUniqueSources_PreserveEverySourceId()
    {
        var result = ImportSingle(
            """
                Test Diamond
                Diamond Ring
                Implicits: 0
                (80-120)% increased Global Critical Strike Chance
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.critical.one", "unique_critical", 80, 120, "unique"),
                Modifier("unique.critical.two", "unique_critical", 80, 120, "unique"),
            ],
            translations:
            [
                Translation("unique-critical", "unique_critical",
                    "{0}% increased Global Critical Strike Chance", "#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(
            UniqueModifierMechanicalMappingStatus.EquivalentSourceSet,
            block.MechanicalMapping.Status);
        Assert.Equal(
            ["unique.critical.one", "unique.critical.two"],
            block.MechanicalMapping.ModifierIds);
        Assert.Equal(["unique_critical"], block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_MechanicallyDifferentExactUniqueSources_RemainAmbiguous()
    {
        var result = ImportSingle(
            """
                Test Diamond
                Diamond Ring
                Implicits: 0
                (80-120)% increased Global Critical Strike Chance
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.critical.one", "first_critical", 80, 120, "unique"),
                Modifier("unique.critical.two", "second_critical", 80, 120, "unique"),
            ],
            translations:
            [
                Translation("first-critical", "first_critical",
                    "{0}% increased Global Critical Strike Chance", "#"),
                Translation("second-critical", "second_critical",
                    "{0}% increased Global Critical Strike Chance", "#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Empty(block.MechanicalMapping.StatIds);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.MechanicalMapping.DiagnosticCode);
    }

    [Fact]
    public void Import_ExactUniqueEvidence_UsesCompatibleBaseDomainWhenAvailable()
    {
        var itemCandidate = Modifier("unique.item", "item_critical", 80, 120, "unique");
        var monsterCandidate = Modifier("unique.monster", "monster_critical", 80, 120, "unique") with
        {
            Domain = "monster",
        };
        var result = ImportSingle(
            """
                Test Diamond
                Diamond Ring
                Implicits: 0
                (80-120)% increased Global Critical Strike Chance
                """,
            generated: false,
            modifiers: [itemCandidate, monsterCandidate],
            translations:
            [
                Translation("item-critical", "item_critical",
                    "{0}% increased Global Critical Strike Chance", "#"),
                Translation("monster-critical", "monster_critical",
                    "{0}% increased Global Critical Strike Chance", "#"),
            ],
            baseItems:
            [
                new ItemBaseRecord { Name = "Diamond Ring", Domain = "item" },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.item"], block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_GeneratedDisplayIndexedMechanic_UsesEvaluatedConcreteTextAndUniqueStatVector()
    {
        var dynamicModifiers = new ModifierDefinition[]
        {
            new()
            {
                Id = "unique.random-skill",
                GroupId = "RandomSkill",
                GenerationType = ModifierGenerationType.Implicit,
                SourceGenerationType = "unique",
                Domain = "item",
                Stats =
                [
                    new ModifierStat
                    {
                        Index = 0,
                        StatId = "random_skill_level",
                        MinValue = 3,
                        MaxValue = 3,
                    },
                    new ModifierStat
                    {
                        Index = 1,
                        StatId = "random_skill_index",
                        MinValue = 1,
                        MaxValue = 287,
                    },
                ],
            },
        };
        var dynamicTranslations = new StatTranslationDefinition[]
        {
            new()
            {
                Id = "random-skill",
                StatIds = ["random_skill_level", "random_skill_index"],
                Variants =
                [
                    new StatTranslationVariant
                    {
                        Conditions =
                        [
                            new StatTranslationCondition { Index = 0, MinValue = 1 },
                            new StatTranslationCondition { Index = 1 },
                        ],
                        ValueFormats = ["#", "#"],
                        IndexHandlers =
                        [
                            new StatTranslationIndexHandler { Index = 0 },
                            new StatTranslationIndexHandler
                            {
                                Index = 1,
                                Handlers = ["display_indexable_skill"],
                            },
                        ],
                        FormatLines = ["+{0} to Level of all {1} Gems"],
                    },
                ],
            },
        };
        var result = ImportSingle(
            """
                Replica Test Flight
                Onyx Amulet
                Implicits: 0
                +3 to Level of all Absolution Gems
                """,
            generated: true,
            modifiers: dynamicModifiers,
            translations: dynamicTranslations);

        var catalog = Assert.IsType<UniqueItemCatalog>(result.Catalog);
        var source = Assert.Single(catalog.SourceObservations);
        Assert.True(source.IsGenerated);
        var block = Assert.Single(Assert.Single(Assert.Single(catalog.Items).Versions).ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.random-skill"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["random_skill_level", "random_skill_index"], block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_StaticExactUniqueRendering_PrecedesCompatibleDynamicDisplayPattern()
    {
        var fixedCandidate = Modifier(
            "unique.fixed-physical",
            "physical_spell_level",
            3,
            3,
            "unique");
        var dynamicCandidate = new ModifierDefinition
        {
            Id = "unique.random-skill",
            GroupId = "RandomSkill",
            GenerationType = ModifierGenerationType.Implicit,
            SourceGenerationType = "unique",
            Domain = "item",
            Stats =
            [
                new ModifierStat { Index = 0, StatId = "random_skill_level", MinValue = 3, MaxValue = 3 },
                new ModifierStat { Index = 1, StatId = "random_skill_index", MinValue = 1, MaxValue = 287 },
            ],
        };
        var result = ImportSingle(
            """
                Test Dagger
                Ezomyte Dagger
                Implicits: 0
                +3 to Level of all Physical Spell Skill Gems
                """,
            generated: false,
            modifiers: [fixedCandidate, dynamicCandidate],
            translations:
            [
                Translation(
                    "physical-spell-level",
                    "physical_spell_level",
                    "{0} to Level of all Physical Spell Skill Gems",
                    "+#"),
                new StatTranslationDefinition
                {
                    Id = "random-skill",
                    StatIds = ["random_skill_level", "random_skill_index"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions =
                            [
                                new StatTranslationCondition { Index = 0, MinValue = 1 },
                                new StatTranslationCondition { Index = 1 },
                            ],
                            ValueFormats = ["#", "#"],
                            IndexHandlers =
                            [
                                new StatTranslationIndexHandler { Index = 0 },
                                new StatTranslationIndexHandler
                                {
                                    Index = 1,
                                    Handlers = ["display_indexable_skill"],
                                },
                            ],
                            FormatLines = ["+{0} to Level of all {1} Gems"],
                        },
                    ],
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.fixed-physical"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["physical_spell_level"], block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_GeneratedOptionDirective_PrefersMatchingDynamicMechanicOverCoincidentalStaticText()
    {
        var fixedCandidate = Modifier(
            "unique.fixed-absolution",
            "fixed_absolution_level",
            3,
            3,
            "unique");
        var dynamicCandidate = new ModifierDefinition
        {
            Id = "unique.random-skill",
            GroupId = "RandomSkill",
            GenerationType = ModifierGenerationType.Implicit,
            SourceGenerationType = "unique",
            Domain = "item",
            Stats =
            [
                new ModifierStat { Index = 0, StatId = "random_skill_level", MinValue = 3, MaxValue = 3 },
                new ModifierStat { Index = 1, StatId = "random_skill_index", MinValue = 1, MaxValue = 287 },
            ],
        };
        var result = ImportSingle(
            """
                Replica Test Flight
                Onyx Amulet
                Variant: Current
                Variant: Absolution
                Implicits: 0
                {variant:2}+3 to Level of all Absolution Gems
                """,
            generated: true,
            modifiers: [fixedCandidate, dynamicCandidate],
            translations:
            [
                Translation(
                    "fixed-absolution",
                    "fixed_absolution_level",
                    "{0} to Level of all Absolution Gems",
                    "+#"),
                DynamicSkillTranslation(),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.random-skill"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["random_skill_level", "random_skill_index"], block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_UnprovenGeneratedMechanic_RemainsUnsupportedWithSpecificReason()
    {
        var result = ImportSingle(
            """
                Generated Test
                Test Base
                Implicits: 0
                This mechanic has no canonical observation
                """,
            generated: true,
            modifiers: [],
            translations: []);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Unsupported, block.MechanicalMapping.Status);
        Assert.Equal("UNIQUE_GENERATED_MECHANICS_NOT_FOUND", block.MechanicalMapping.DiagnosticCode);
        Assert.Contains("evaluated generated PoB", block.MechanicalMapping.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_EvaluatedVariantsAndGeneratedReplica_RetainsProvenanceAndMechanics()
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-pob-uniques-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                entries = new object[]
                {
                    new
                    {
                        uniqueType = "helmet",
                        sourcePath = "Data/Uniques/helmet.lua",
                        generated = false,
                        raw = """
                            Test Crown
                            {variant:1}Iron Hat
                            {variant:2}Leather Cap
                            Variant: Pre 3.29.0
                            Variant: Current
                            Implicits: 0
                            {variant:1}+(20-30) to maximum Life
                            {variant:2}+(40-50) to maximum Life
                            Cannot be Stunned
                            """,
                    },
                    new
                    {
                        uniqueType = "generated",
                        sourcePath = "Data/Uniques/Special/Generated.lua",
                        generated = true,
                        raw = """
                            Replica Test Crown
                            Iron Hat
                            Implicits: 0
                            +(10-20) to maximum Life
                            """,
                    },
                },
            }));

            var modifiers = new[]
            {
                Modifier("unique.life", "maximum_life", 10, 50),
                Modifier("unique.stun", "cannot_be_stunned", 1, 1),
            };
            var translations = new[]
            {
                Translation("life", "maximum_life", "+{0} to maximum Life", "+#"),
                Translation("stun", "cannot_be_stunned", "Cannot be Stunned"),
            };

            var result = new PoBUniqueCatalogImporter().Import(
                path,
                "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                "v2.67.2",
                "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                modifiers,
                translations);

            Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                diagnostic.Severity == ImportDiagnosticSeverity.Error);
            Assert.Equal(2, result.RecordsImported);
            var catalog = Assert.IsType<UniqueItemCatalog>(result.Catalog);
            var ordinary = Assert.Single(catalog.Items, item => item.CanonicalName == "Test Crown");
            Assert.Equal(UniqueItemKind.Ordinary, ordinary.Kind);
            Assert.Equal(["Iron Hat", "Leather Cap"], ordinary.BaseTypeEvidence);
            Assert.Collection(
                ordinary.Versions.OrderBy(version => version.Role),
                current =>
                {
                    Assert.Equal(UniqueItemVersionRole.Current, current.Role);
                    Assert.Equal("Leather Cap", current.BaseType);
                },
                historical =>
                {
                    Assert.Equal(UniqueItemVersionRole.Historical, historical.Role);
                    Assert.Equal("Iron Hat", historical.BaseType);
                });
            Assert.All(ordinary.Versions.SelectMany(version => version.ModifierBlocks), block =>
                Assert.NotEqual(UniqueModifierMechanicalMappingStatus.Unknown, block.MechanicalMapping.Status));

            var replica = Assert.Single(catalog.Items, item => item.CanonicalName == "Replica Test Crown");
            Assert.Equal(UniqueItemKind.Replica, replica.Kind);
            var replicaSource = Assert.Single(catalog.SourceObservations, source =>
                replica.SourceObservationIds.Contains(source.Id!));
            Assert.True(replicaSource.IsGenerated);
            Assert.Equal("Data/Uniques/Special/Generated.lua", replicaSource.SourcePath);
            Assert.Equal(64, replicaSource.RawEntrySha256!.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static PoBUniqueCatalogImportResult ImportSingle(
        string raw,
        bool generated,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations,
        IReadOnlyList<ItemBaseRecord>? baseItems = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-pob-uniques-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                entries = new[]
                {
                    new
                    {
                        uniqueType = generated ? "generated" : "ring",
                        sourcePath = generated
                            ? "Data/Uniques/Special/Generated.lua"
                            : "Data/Uniques/ring.lua",
                        generated,
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
                baseItems);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ModifierDefinition Modifier(
        string id,
        string statId,
        decimal min,
        decimal max,
        string sourceGenerationType = "prefix") => new()
    {
        Id = id,
        GroupId = id,
        Name = id,
        GenerationType = sourceGenerationType == "unique"
            ? ModifierGenerationType.Implicit
            : ModifierGenerationType.Prefix,
        SourceGenerationType = sourceGenerationType,
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
                ValueFormats = valueFormats,
                IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
            },
        ],
    };

    private static StatTranslationDefinition DynamicSkillTranslation() => new()
    {
        Id = "random-skill",
        StatIds = ["random_skill_level", "random_skill_index"],
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions =
                [
                    new StatTranslationCondition { Index = 0, MinValue = 1 },
                    new StatTranslationCondition { Index = 1 },
                ],
                ValueFormats = ["#", "#"],
                IndexHandlers =
                [
                    new StatTranslationIndexHandler { Index = 0 },
                    new StatTranslationIndexHandler
                    {
                        Index = 1,
                        Handlers = ["display_indexable_skill"],
                    },
                ],
                FormatLines = ["+{0} to Level of all {1} Gems"],
            },
        ],
    };
}
