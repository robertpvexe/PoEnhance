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
    public void Import_PartialTranslationVector_DefaultsAbsentStatToZeroAndRetainsProof()
    {
        var modifier = Modifier(
            "unique.returning-projectiles",
            "projectiles_return",
            1,
            1,
            "unique");
        var translation = new StatTranslationDefinition
        {
            Id = "returning-projectiles",
            StatIds = ["projectiles_return", "projectile_return_chance"],
            Variants =
            [
                new StatTranslationVariant
                {
                    Conditions =
                    [
                        new StatTranslationCondition
                        {
                            Index = 0,
                            MinValue = 0,
                            MaxValue = 0,
                            IsNegated = true,
                        },
                        new StatTranslationCondition { Index = 1, MinValue = 0, MaxValue = 0 },
                    ],
                    ValueFormats = ["ignore", "ignore"],
                    IndexHandlers =
                    [
                        new StatTranslationIndexHandler { Index = 0 },
                        new StatTranslationIndexHandler { Index = 1 },
                    ],
                    FormatLines =
                    [
                        "Projectiles Return to you",
                        "Return",
                    ],
                },
            ],
        };

        var result = ImportSingle(
            """
                Test Return
                Topaz Ring
                Implicits: 0
                Projectiles Return to you
                Return
                """,
            generated: false,
            modifiers: [modifier],
            translations: [translation]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(2, block.Lines.Count);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["projectiles_return"], block.MechanicalMapping.StatIds);
        var provenance = Assert.IsType<UniqueModifierMechanicalProvenance>(
            block.MechanicalMapping.Provenance);
        Assert.Equal(["implicit-zero-stat-composition"], provenance.ResolutionReasons);
        Assert.True(provenance.UsedComposition);
        Assert.Equal("copiedInstance", provenance.ValueAuthority);
        var evidence = Assert.Single(provenance.Translations);
        Assert.Equal("returning-projectiles", evidence.TranslationId);
        Assert.Equal(["projectile_return_chance"], evidence.DefaultedStatIds);
        Assert.Equal(2, evidence.Conditions.Count);
    }

    [Fact]
    public void Import_CompleteMultilineModifier_GroupsSourceLinesIndependentOfRenderingOrder()
    {
        var modifier = Modifier(
            "unique.minion-count",
            ("zombies", 1m, 1m),
            ("skeletons", 1m, 1m),
            ("spectres", 1m, 1m));
        var result = ImportSingle(
            """
                Test Wand
                Calling Wand
                Implicits: 0
                +1 to maximum number of Raised Zombies
                +1 to maximum number of Spectres
                +1 to maximum number of Skeletons
                """,
            generated: false,
            modifiers: [modifier],
            translations:
            [
                TranslationWithDefaultedZero(
                    "zombies",
                    "zombies",
                    "quality_display_raise_zombie_is_gem",
                    "{0} to maximum number of Raised Zombies"),
                TranslationWithDefaultedZero(
                    "skeletons",
                    "skeletons",
                    "quality_display_summon_skeleton_is_gem",
                    "{0} to maximum number of Skeletons"),
                Translation("spectres", "spectres", "{0} to maximum number of Spectres", "+#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(3, block.Lines.Count);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.minion-count"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["zombies", "skeletons", "spectres"], block.MechanicalMapping.StatIds);
        var provenance = Assert.IsType<UniqueModifierMechanicalProvenance>(
            block.MechanicalMapping.Provenance);
        Assert.True(provenance.UsedComposition);
        Assert.Equal(3, provenance.Translations.Count);
        Assert.Equal("copiedInstance", provenance.ValueAuthority);
        Assert.Contains("implicit-zero-stat-composition", provenance.ResolutionReasons);
        Assert.Contains("order-independent-complete-multiline", provenance.ResolutionReasons);
    }

    [Fact]
    public void Import_CompleteMultilineLiteralAndNumericModifier_GroupsReversedSourceOrder()
    {
        var modifier = Modifier(
            "unique.life-reservation",
            ("life_reserved", 30m, 30m),
            ("cannot_use_ci", 1m, 1m));
        var result = ImportSingle(
            """
                Test Wand
                Calling Wand
                Implicits: 0
                Cannot be used with Chaos Inoculation
                Reserves 30% of Life
                """,
            generated: false,
            modifiers: [modifier],
            translations:
            [
                Translation("reservation", "life_reserved", "Reserves {0}% of Life", "#"),
                Translation("cannot-use-ci", "cannot_use_ci", "Cannot be used with Chaos Inoculation", "ignore"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(2, block.Lines.Count);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.life-reservation"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["life_reserved", "cannot_use_ci"], block.MechanicalMapping.StatIds);
        var provenance = Assert.IsType<UniqueModifierMechanicalProvenance>(
            block.MechanicalMapping.Provenance);
        Assert.Equal(2, provenance.Translations.Count);
        Assert.Contains("order-independent-complete-multiline", provenance.ResolutionReasons);
    }

    [Fact]
    public void Import_PartialMultilineModifier_OmittingNonzeroStatDoesNotResolveCompleteVector()
    {
        var modifier = Modifier(
            "unique.minion-count",
            ("zombies", 1m, 1m),
            ("skeletons", 1m, 1m),
            ("spectres", 1m, 1m));
        var result = ImportSingle(
            """
                Test Wand
                Calling Wand
                Implicits: 0
                +1 to maximum number of Raised Zombies
                +1 to maximum number of Spectres
                """,
            generated: false,
            modifiers: [modifier],
            translations:
            [
                Translation("zombies", "zombies", "{0} to maximum number of Raised Zombies", "+#"),
                Translation("skeletons", "skeletons", "{0} to maximum number of Skeletons", "+#"),
                Translation("spectres", "spectres", "{0} to maximum number of Spectres", "+#"),
            ]);

        var blocks = Assert.Single(Assert.Single(result.Catalog!.Items).Versions).ModifierBlocks;
        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, block =>
        {
            Assert.Equal(UniqueModifierMechanicalMappingStatus.Unsupported,
                block.MechanicalMapping.Status);
            Assert.Empty(block.MechanicalMapping.StatIds);
            Assert.Null(block.MechanicalMapping.Provenance);
        });
    }

    [Fact]
    public void Import_CompetingCompleteMultilineVectors_GroupAtomicallyAndFailClosed()
    {
        var current = Modifier(
            "unique.minion-count.current",
            ("zombies", 1m, 1m),
            ("skeletons", 1m, 1m),
            ("spectres", 1m, 1m));
        var legacy = Modifier(
            "unique.minion-count.legacy",
            ("zombies", 1m, 1m),
            ("legacy_skeletons", 1m, 1m),
            ("spectres", 1m, 1m));
        var result = ImportSingle(
            """
                Test Wand
                Calling Wand
                Implicits: 0
                +1 to maximum number of Raised Zombies
                +1 to maximum number of Spectres
                +1 to maximum number of Skeletons
                """,
            generated: false,
            modifiers: [current, legacy],
            translations:
            [
                Translation("zombies", "zombies", "{0} to maximum number of Raised Zombies", "+#"),
                Translation("skeletons", "skeletons", "{0} to maximum number of Skeletons", "+#"),
                Translation("legacy-skeletons", "legacy_skeletons", "{0} to maximum number of Skeletons", "+#"),
                Translation("spectres", "spectres", "{0} to maximum number of Spectres", "+#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(3, block.Lines.Count);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous,
            block.MechanicalMapping.Status);
        Assert.Equal(
            ["unique.minion-count.current", "unique.minion-count.legacy"],
            block.MechanicalMapping.ModifierIds);
        Assert.Empty(block.MechanicalMapping.StatIds);
        Assert.Contains("conflicting RePoE mechanical stat vectors",
            block.MechanicalMapping.Diagnostic);
        Assert.Null(block.MechanicalMapping.Provenance);
    }

    [Fact]
    public void Import_PartialTranslationVector_DefaultZeroOutsideConditionRemainsUnsupported()
    {
        var modifier = Modifier("unique.test", "present_stat", 1, 1, "unique");
        var translation = new StatTranslationDefinition
        {
            Id = "requires-nonzero-missing-stat",
            StatIds = ["present_stat", "missing_stat"],
            Variants =
            [
                new StatTranslationVariant
                {
                    Conditions =
                    [
                        new StatTranslationCondition { Index = 0, MinValue = 1 },
                        new StatTranslationCondition { Index = 1, MinValue = 1 },
                    ],
                    ValueFormats = ["ignore", "ignore"],
                    IndexHandlers =
                    [
                        new StatTranslationIndexHandler { Index = 0 },
                        new StatTranslationIndexHandler { Index = 1 },
                    ],
                    FormatLines = ["Requires both stats"],
                },
            ],
        };

        var result = ImportSingle(
            """
                Test Missing
                Topaz Ring
                Implicits: 0
                Requires both stats
                """,
            generated: false,
            modifiers: [modifier],
            translations: [translation]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Unsupported, block.MechanicalMapping.Status);
        Assert.Null(block.MechanicalMapping.Provenance);
    }

    [Fact]
    public void Import_PartialTranslationVector_DoesNotOverrideExistingStrictResolution()
    {
        var exact = Modifier("unique.exact", "exact_stat", 1, 1, "unique");
        var partial = Modifier("unique.partial", "partial_stat", 1, 1, "unique");
        var partialTranslation = new StatTranslationDefinition
        {
            Id = "partial-fallback",
            StatIds = ["partial_stat", "missing_stat"],
            Variants =
            [
                new StatTranslationVariant
                {
                    Conditions =
                    [
                        new StatTranslationCondition { Index = 0, MinValue = 1, MaxValue = 1 },
                        new StatTranslationCondition { Index = 1, MinValue = 0, MaxValue = 0 },
                    ],
                    ValueFormats = ["ignore", "ignore"],
                    IndexHandlers =
                    [
                        new StatTranslationIndexHandler { Index = 0 },
                        new StatTranslationIndexHandler { Index = 1 },
                    ],
                    FormatLines = ["Existing Resolution"],
                },
            ],
        };

        var result = ImportSingle(
            """
                Test Existing
                Topaz Ring
                Implicits: 0
                Existing Resolution
                """,
            generated: false,
            modifiers: [exact, partial],
            translations:
            [
                Translation("exact", "exact_stat", "Existing Resolution", "ignore"),
                partialTranslation,
            ]);

        var mapping = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks).MechanicalMapping;
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, mapping.Status);
        Assert.Equal(["unique.exact"], mapping.ModifierIds);
        Assert.Null(mapping.Provenance);
    }

    [Fact]
    public void Import_ReviewedLocalPropertyCapabilityEliminatesImpossibleNonWeaponCandidate()
    {
        var global = Modifier("unique.global", "attack_speed", 10, 10, "unique");
        var local = Modifier("unique.local", "local_attack_speed", 10, 10, "unique");
        var result = ImportSingle(
            """
                Test Speed
                Topaz Ring
                Implicits: 0
                10% increased Attack Speed
                """,
            generated: false,
            modifiers: [global, local],
            translations:
            [
                Translation("global", "attack_speed", "{0}% increased Attack Speed", "#"),
                Translation("local", "local_attack_speed", "{0}% increased Attack Speed", "#"),
            ],
            baseItems: [new ItemBaseRecord { Name = "Topaz Ring", Domain = "item" }],
            itemPropertySemantics:
            [
                new ItemPropertySemanticDescriptor
                {
                    Id = "weapon.attack-speed.local",
                    OrderedStatIds = ["local_attack_speed"],
                    Applicability = ItemPropertyApplicability.UnconditionalDisplayedLocal,
                    Contributions =
                    [
                        new ItemPropertyContribution
                        {
                            Targets = [ItemPropertyTarget.AttacksPerSecond],
                            Operation = ItemPropertyOperation.IncreasedPercent,
                        },
                    ],
                },
            ]);

        var mapping = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks).MechanicalMapping;
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, mapping.Status);
        Assert.Equal(["unique.global"], mapping.ModifierIds);
        Assert.Contains("base-item-property-capability", mapping.Provenance!.ResolutionReasons);
    }

    [Fact]
    public void Import_MetadataBeforeBaseAndItemStateLines_DoNotBecomeModifierBlocks()
    {
        var result = ImportSingle(
            """
                Test Crown
                Shaper Item
                League: Test League
                Source: Test Source
                Iron Hat
                Requires Level: 20
                Implicits: 0
                +(10-20) to maximum Life
                {variant:1}Corrupted
                """,
            generated: false,
            modifiers: [Modifier("unique.life", "maximum_life", 10, 20, "unique")],
            translations: [Translation("life", "maximum_life", "{0} to maximum Life", "+#")]);

        var version = Assert.Single(Assert.Single(result.Catalog!.Items).Versions);
        Assert.Equal("Iron Hat", version.BaseType);
        var block = Assert.Single(version.ModifierBlocks);
        Assert.Equal(["+(10-20) to maximum Life"], block.Lines);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Single(block.SourceObservationIds);
    }

    [Fact]
    public void Import_NonContiguousTranslationVectorsAndIgnoredStats_KeepAuthenticBlockAtomic()
    {
        var modifier = new ModifierDefinition
        {
            Id = "unique.timeless",
            GroupId = "Timeless",
            GenerationType = ModifierGenerationType.Implicit,
            SourceGenerationType = "unique",
            Domain = "misc",
            Stats =
            [
                new ModifierStat { Index = 0, StatId = "version", MinValue = 2, MaxValue = 2 },
                new ModifierStat { Index = 1, StatId = "seed", MinValue = 10000, MaxValue = 18000 },
                new ModifierStat { Index = 2, StatId = "keystone", MinValue = 1, MaxValue = 3 },
                new ModifierStat { Index = 3, StatId = "radius", MinValue = 1500, MaxValue = 1500 },
                new ModifierStat { Index = 4, StatId = "historic", MinValue = 1, MaxValue = 1 },
                new ModifierStat { Index = 5, StatId = "revision", MinValue = 1, MaxValue = 1 },
            ],
        };
        var result = ImportSingle(
            """
                Test Pride
                Timeless Jewel
                Radius: Large
                Implicits: 0
                Commanded leadership over (10000-18000) warriors under Akoya
                Passives in radius are Conquered by the Karui
                Historic
                """,
            generated: false,
            modifiers: [modifier],
            translations:
            [
                new StatTranslationDefinition
                {
                    Id = "timeless-seed",
                    StatIds = ["version", "seed", "keystone", "revision"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions =
                            [
                                new StatTranslationCondition { Index = 0, MinValue = 2, MaxValue = 2 },
                                new StatTranslationCondition { Index = 1 },
                                new StatTranslationCondition { Index = 2, MinValue = 3, MaxValue = 3 },
                                new StatTranslationCondition { Index = 3 },
                            ],
                            ValueFormats = ["ignore", "#", "ignore", "ignore"],
                            IndexHandlers =
                            [
                                new StatTranslationIndexHandler { Index = 0 },
                                new StatTranslationIndexHandler { Index = 1 },
                                new StatTranslationIndexHandler { Index = 2 },
                                new StatTranslationIndexHandler { Index = 3 },
                            ],
                            FormatLines =
                            [
                                "Commanded leadership over {1} warriors under Akoya",
                                "Passives in radius are Conquered by the Karui",
                            ],
                        },
                    ],
                },
                new StatTranslationDefinition
                {
                    Id = "timeless-historic",
                    StatIds = ["historic"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0 }],
                            ValueFormats = ["ignore"],
                            IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
                            FormatLines = ["Historic"],
                        },
                    ],
                },
            ],
            baseItems:
            [
                new ItemBaseRecord { Name = "Timeless Jewel", Domain = "misc" },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(3, block.Lines.Count);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.timeless"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["version", "seed", "keystone", "radius", "historic", "revision"],
            block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_ExactNumericValueEvidence_PrecedesEquivalentLiteralIgnoredRendering()
    {
        var literal = Modifier(
            "unique.literal-bleed",
            "fixed_bleed_chance",
            1,
            1,
            "unique");
        var numeric = Modifier(
            "unique.numeric-bleed",
            "bleed_chance_percent",
            50,
            50,
            "unique");
        var result = ImportSingle(
            """
                Test Axe
                Headsman Axe
                Implicits: 0
                50% chance to cause Bleeding on Hit
                """,
            generated: false,
            modifiers: [literal, numeric],
            translations:
            [
                new StatTranslationDefinition
                {
                    Id = "literal-bleed",
                    StatIds = ["fixed_bleed_chance"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0 }],
                            ValueFormats = ["ignore"],
                            IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
                            FormatLines = ["50% chance to cause Bleeding on Hit"],
                        },
                    ],
                },
                Translation(
                    "numeric-bleed",
                    "bleed_chance_percent",
                    "{0}% chance to cause Bleeding on Hit",
                    "#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["unique.numeric-bleed"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["bleed_chance_percent"], block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_CompositeCurrentAndPreLabels_SelectOnlyCurrentVariants()
    {
        var result = ImportSingle(
            """
                Test Fostering
                Test Armour
                Variant: Rhoa Pre 3.26
                Variant: Snake Pre 3.26
                Variant: Rhoa Current
                Variant: Snake Current
                Implicits: 0
                {variant:1}10% increased Rhoa Damage
                {variant:2}10% increased Snake Damage
                {variant:3}20% increased Rhoa Damage
                {variant:4}20% increased Snake Damage
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.rhoa-current", "rhoa_damage", 20, 20, "unique"),
                Modifier("unique.snake-current", "snake_damage", 20, 20, "unique"),
                Modifier("unique.rhoa-old", "rhoa_damage", 10, 10, "unique"),
                Modifier("unique.snake-old", "snake_damage", 10, 10, "unique"),
            ],
            translations:
            [
                Translation("rhoa", "rhoa_damage", "{0}% increased Rhoa Damage", "#"),
                Translation("snake", "snake_damage", "{0}% increased Snake Damage", "#"),
            ]);

        var versions = Assert.Single(result.Catalog!.Items).Versions;
        Assert.Equal(2, versions.Count(version => version.Role == UniqueItemVersionRole.Current));
        Assert.Equal(2, versions.Count(version => version.Role == UniqueItemVersionRole.Historical));
        Assert.All(versions, version => Assert.Single(version.ModifierBlocks));
        Assert.Equal(
            ["20% increased Rhoa Damage", "20% increased Snake Damage"],
            versions.Where(version => version.Role == UniqueItemVersionRole.Current)
                .SelectMany(version => version.ModifierBlocks)
                .SelectMany(block => block.Lines)
                .OrderBy(line => line, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void Import_ExplicitCurrentSibling_ClassifiesBareAndPunctuatedPatchLabelsAsHistorical()
    {
        var result = ImportSingle(
            """
                Test History
                Test Armour
                Variant: 3.19.0
                Variant: Pre.3.20.0
                Variant: Current
                Implicits: 0
                {variant:1}10% increased Armour
                {variant:2}20% increased Armour
                {variant:3}30% increased Armour
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.history-one", "armour", 10, 10, "unique"),
                Modifier("unique.history-two", "armour", 20, 20, "unique"),
                Modifier("unique.current", "armour", 30, 30, "unique"),
            ],
            translations:
            [
                Translation("armour", "armour", "{0}% increased Armour", "#"),
            ]);

        var versions = Assert.Single(result.Catalog!.Items).Versions;
        Assert.Equal(2, versions.Count(version => version.Role == UniqueItemVersionRole.Historical));
        Assert.Single(versions, version => version.Role == UniqueItemVersionRole.Current);
        Assert.All(versions.Where(version => version.Role == UniqueItemVersionRole.Historical), version =>
            Assert.Contains("histor", version.RoleDecisionReason, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Import_CurrentRePoeBaseDiacriticAlias_CanonicalizesBaseAndPreservesSourceText()
    {
        var result = ImportSingle(
            """
                Test Staff
                Maelstrom Staff
                Variant: Current
                Implicits: 0
                """,
            generated: false,
            modifiers: [],
            translations: [],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Weapons/TwoHandWeapons/Staves/Staff17",
                    Name = "Maelström Staff",
                    ItemClass = "Warstaff",
                },
            ]);

        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
        var identity = Assert.Single(result.Catalog!.Items);
        Assert.Equal("ordinary|test staff", identity.CanonicalIdentityKey);
        var version = Assert.Single(identity.Versions);
        Assert.Equal("Maelström Staff", version.BaseType);
        Assert.Equal("Maelstrom Staff", version.SourceBaseType);
        Assert.Equal(UniqueSourceIdentityNormalizer.CanonicalRule, version.BaseTypeNormalizationRule);
        Assert.Equal(["Metadata/Items/Weapons/TwoHandWeapons/Staves/Staff17"], version.RePoeBaseItemIds);
    }

    [Fact]
    public void Import_CurrentRePoeBaseCanonicalCollision_FailsClosed()
    {
        var result = ImportSingle(
            """
                Test Staff
                Ä Base
                Variant: Current
                Implicits: 0
                """,
            generated: false,
            modifiers: [],
            translations: [],
            baseItems:
            [
                new ItemBaseRecord { Id = "base-a", Name = "A Base", ItemClass = "Staff" },
                new ItemBaseRecord { Id = "base-accent", Name = "Á Base", ItemClass = "Staff" },
            ]);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RePoeImportDiagnosticCodes.PoBUniqueBaseNormalizationCollision &&
            diagnostic.Severity == ImportDiagnosticSeverity.Error);
        var version = Assert.Single(Assert.Single(result.Catalog!.Items).Versions);
        Assert.Equal("Ä Base", version.BaseType);
        Assert.Empty(version.RePoeBaseItemIds);
    }

    [Fact]
    public void Import_DistinctSourceNamesWithSameCanonicalKey_FailsClosedWithoutMerging()
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-pob-collision-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                entries = new[]
                {
                    new { sourcePath = "one.lua", generated = false, raw = "Tést Item\nTest Base" },
                    new { sourcePath = "two.lua", generated = false, raw = "Test Item\nTest Base" },
                },
            }));

            var result = new PoBUniqueCatalogImporter().Import(
                path,
                "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                "v2.67.2",
                "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                [],
                []);

            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code == RePoeImportDiagnosticCodes.PoBUniqueIdentityNormalizationCollision &&
                diagnostic.Severity == ImportDiagnosticSeverity.Error);
            Assert.Equal(2, result.Catalog!.Items.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_UnlabelledNonGeneratedAlternatives_AreDistinctCurrentVersions()
    {
        var result = ImportSingle(
            """
                Test Voices
                Large Cluster Jewel
                Variant: Adds 1 Small Passive Skill
                Variant: Adds 3 Small Passive Skills
                Implicits: 0
                {variant:1}Adds 1 Small Passive Skill which grants nothing
                {variant:2}Adds 3 Small Passive Skills which grant nothing
                """,
            generated: false,
            modifiers:
            [
                Modifier("unique.one", "small_passives", 1, 1, "unique"),
                Modifier("unique.three", "small_passives", 3, 3, "unique"),
            ],
            translations:
            [
                Translation("passives", "small_passives",
                    "Adds {0} Small Passive Skills which grants nothing", "#"),
            ]);

        var versions = Assert.Single(result.Catalog!.Items).Versions;
        Assert.Equal(2, versions.Count);
        Assert.All(versions, version =>
        {
            Assert.Equal(UniqueItemVersionRole.Current, version.Role);
            Assert.Single(version.ModifierBlocks);
        });
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
    public void Import_GeneratedOptions_RetainExplicitCurrentAndHistoricalObservations()
    {
        var result = ImportSingle(
            """
                Replica Test Flight
                Onyx Amulet
                Variant: Pre 3.23.0
                Variant: Current
                Variant: Absolution
                Implicits: 0
                {variant:1}10% increased Reservation Efficiency of Skills
                {variant:2}5% increased Reservation Efficiency of Skills
                {variant:3}+3 to Level of all Absolution Gems
                """,
            generated: true,
            modifiers:
            [
                Modifier("unique.old-reservation", "reservation", 10, 10, "unique"),
                Modifier("unique.current-reservation", "reservation", 5, 5, "unique"),
                new ModifierDefinition
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
                },
            ],
            translations:
            [
                Translation(
                    "reservation",
                    "reservation",
                    "{0}% increased Reservation Efficiency of Skills",
                    "#"),
                DynamicSkillTranslation(),
            ]);

        var versions = Assert.Single(result.Catalog!.Items).Versions;
        Assert.Equal(2, versions.Count);
        Assert.Contains(versions, version => version.Role == UniqueItemVersionRole.Current);
        Assert.Contains(versions, version => version.Role == UniqueItemVersionRole.Historical);
        Assert.All(versions, version => Assert.Contains(version.ModifierBlocks, block =>
            block.Lines.Contains("+3 to Level of all Absolution Gems")));
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
        IReadOnlyList<ItemBaseRecord>? baseItems = null,
        IReadOnlyList<ItemPropertySemanticDescriptor>? itemPropertySemantics = null)
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
                baseItems,
                itemPropertySemantics);
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

    private static ModifierDefinition Modifier(
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

    private static StatTranslationDefinition TranslationWithDefaultedZero(
        string id,
        string statId,
        string defaultedStatId,
        string format) => new()
    {
        Id = id,
        StatIds = [statId, defaultedStatId],
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions =
                [
                    new StatTranslationCondition { Index = 0 },
                    new StatTranslationCondition { Index = 1, MinValue = 0, MaxValue = 0 },
                ],
                FormatLines = [format],
                ValueFormats = ["+#", "ignore"],
                IndexHandlers =
                [
                    new StatTranslationIndexHandler { Index = 0 },
                    new StatTranslationIndexHandler { Index = 1 },
                ],
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
