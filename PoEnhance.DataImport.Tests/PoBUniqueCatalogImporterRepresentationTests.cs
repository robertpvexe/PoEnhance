using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class PoBUniqueCatalogImporterRepresentationTests
{
    [Fact]
    public void Import_MultilineSourceCompositionWithOptionMembershipMismatch_PreservesOneLogicalBlock()
    {
        const string commissioned = "Commissioned (2000-160000) coins to commemorate Example";
        const string conquered = "Passives in radius are Conquered by the Eternal Empire";
        const string historic = "Historic";
        var modifier = Modifier(
            "unique.timeless.seed",
            ("version", 5m, 5m),
            ("seed", 2000m, 160000m),
            ("keystone", 1m, 3m),
            ("radius", 1500m, 1500m),
            ("is_alternate", 1m, 1m),
            ("revision", 1m, 1m)) with
        {
            SourceText = $"{commissioned}\n{conquered}\n{historic}",
        };
        var result = Import(
            raw: """
                Test Timeless
                Timeless Jewel
                Has Alt Variant: true
                Selected Variant: 2
                Variant: Keystone Alpha
                Variant: Keystone Beta
                Implicits: 0
                Commissioned (2000-160000) coins to commemorate Example
                Passives in radius are Conquered by the Eternal Empire
                Historic
                {variant:2}4% increased Brand Damage per 10 Devotion
                """,
            modifiers:
            [
                modifier,
                Modifier("unique.brand", "brand_damage_per_devotion", 4, 4, "unique"),
            ],
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
                                new StatTranslationCondition { Index = 0, MinValue = 5, MaxValue = 5 },
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
                                "Commissioned {1} coins to commemorate Example",
                                "Passives in radius are Conquered by the Eternal Empire",
                            ],
                        },
                    ],
                },
                new StatTranslationDefinition
                {
                    Id = "timeless-historic",
                    StatIds = ["is_alternate"],
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
                Translation("brand", "brand_damage_per_devotion",
                    "{0}% increased Brand Damage per {0} Devotion", "#"),
            ],
            optionAxes:
            [
                new SourceOptionAxisFixture(
                    SourceKind: "pobCoSelectableAxis",
                    SourceOrdinal: 1,
                    SelectionLimit: 1,
                    SourceChoiceIndices: [2],
                    SelectedChoiceIndices: [2]),
            ],
            baseItems: [new ItemBaseRecord { Name = "Timeless Jewel", Domain = "misc" }]);

        var currentVersions = Assert.Single(result.Catalog!.Items).Versions
            .Where(version => version.Role == UniqueItemVersionRole.Current)
            .ToArray();
        Assert.Equal(2, currentVersions.Length);
        var keystoneVersion = Assert.Single(currentVersions,
            version => version.Label == "Keystone Alpha");
        var seedBlock = Assert.Single(keystoneVersion.ModifierBlocks,
            block => block.Lines.Count > 1);
        Assert.Equal(3, seedBlock.Lines.Count);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, seedBlock.MechanicalMapping.Status);
        Assert.Equal(["unique.timeless.seed"], seedBlock.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["version", "seed", "keystone", "radius", "is_alternate", "revision"],
            seedBlock.MechanicalMapping.StatIds);
        Assert.Null(seedBlock.Composition);
        Assert.DoesNotContain(keystoneVersion.ModifierBlocks, block =>
            block.Lines.Count == 1 &&
            block.Lines[0] == historic &&
            block.MechanicalMapping.ModifierIds.Contains("unique.timeless.seed"));
    }

    [Fact]
    public void Import_MultilineSourceComposition_PreservesOrderedComponentStatsInComposition()
    {
        const string defenceLine = "(100-120)% increased Armour";
        const string stunLine = "10% increased Stun and Block Recovery";
        var modifier = Modifier(
            "unique.compound-defence",
            ("local_armour", 100m, 120m),
            ("base_stun_recovery_+%", 10m, 10m)) with
        {
            SourceText = $"{defenceLine}\n{stunLine}",
        };
        var result = ImportSingle(
            $"""
                Test Helmet
                Iron Hat
                Implicits: 0
                {defenceLine}
                {stunLine}
                """,
            modifiers: [modifier],
            translations:
            [
                Translation("defence", "local_armour", "{0}% increased Armour", "#"),
                Translation("stun-recovery", "base_stun_recovery_+%",
                    "{0}% increased Stun and Block Recovery", "#"),
            ],
            stats:
            [
                new StatDefinition { Id = "local_armour", IsLocal = true },
                new StatDefinition { Id = "base_stun_recovery_+%", IsLocal = false },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        var composition = Assert.IsType<UniqueModifierComposition>(block.Composition);
        Assert.Equal(2, composition.Components.Count);
        Assert.Equal([defenceLine], composition.Components[0].Lines);
        Assert.Equal(["local_armour"], composition.Components[0].StatIds);
        Assert.Equal([stunLine], composition.Components[1].Lines);
        Assert.Equal(["base_stun_recovery_+%"], composition.Components[1].StatIds);
    }

    [Fact]
    public void Import_EquivalentMultilineSourceObservations_DeduplicateWithoutSplitting()
    {
        const string first = "Does not inflict Mana Burn over time";
        const string second = "Inflicts Mana Burn on you when you Hit an Enemy with a Melee Weapon";
        var modifier = Modifier(
            "unique.mana-burn",
            ("mana_burn_negated", 1m, 1m),
            ("mana_burn_inflicted", 1m, 1m)) with
        {
            SourceText = $"{first}\n{second}",
        };
        var duplicate = modifier with { Id = "unique.mana-burn.divergent" };
        var result = ImportSingle(
            $"""
                Test Tincture
                Iron Flask
                Implicits: 0
                {first}
                {second}
                """,
            modifiers: [modifier, duplicate],
            translations: []);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(2, block.Lines.Count);
        Assert.Equal(
            UniqueModifierMechanicalMappingStatus.EquivalentSourceSet,
            block.MechanicalMapping.Status);
        Assert.Equal(
            ["unique.mana-burn", "unique.mana-burn.divergent"],
            block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_AdjacentButMechanicallyUnrelatedLines_DoNotMerge()
    {
        var result = ImportSingle(
            """
                Test Ring
                Diamond Ring
                Implicits: 0
                +(10-20) to maximum Life
                +(30-40)% to Fire Resistance
                """,
            modifiers:
            [
                Modifier("unique.life", "maximum_life", 10, 20, "unique"),
                Modifier("unique.fire", "base_fire_damage_resistance_%", 30, 40, "unique"),
            ],
            translations:
            [
                Translation("life", "maximum_life", "+{0} to maximum Life", "+#"),
                Translation("fire", "base_fire_damage_resistance_%",
                    "+{0}% to Fire Resistance", "+#"),
            ]);

        var blocks = Assert.Single(Assert.Single(result.Catalog!.Items).Versions).ModifierBlocks;
        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, block => Assert.Single(block.Lines));
    }

    [Fact]
    public void Import_PartialCompositionEvidence_RemainsFailClosed()
    {
        const string defenceLine = "(100-120)% increased Armour";
        const string stunLine = "10% increased Stun and Block Recovery";
        var modifier = Modifier(
            "unique.compound-defence",
            ("local_armour", 100m, 120m),
            ("base_stun_recovery_+%", 10m, 10m)) with
        {
            SourceText = $"{defenceLine}\n{stunLine}",
        };
        var result = ImportSingle(
            $"""
                Test Helmet
                Iron Hat
                Implicits: 0
                {defenceLine}
                """,
            modifiers: [modifier],
            translations:
            [
                Translation("defence", "local_armour", "{0}% increased Armour", "#"),
                Translation("stun-recovery", "base_stun_recovery_+%",
                    "{0}% increased Stun and Block Recovery", "#"),
            ],
            stats: [new StatDefinition { Id = "local_armour", IsLocal = true }]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Null(block.Composition);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Unsupported,
            block.MechanicalMapping.Status);
    }

    [Fact]
    public void Import_CoSelectableChoicesWithHistoricalVariant_ShareOneCurrentVersionPerKeystone()
    {
        const string carved = "Carved to glorify (2000-10000) new faithful converted by Example";
        const string conquered = "Passives in radius are Conquered by the Templars";
        const string historic = "Historic";
        var seedModifier = Modifier(
            "unique.timeless.seed",
            ("version", 4m, 4m),
            ("seed", 2000m, 10000m),
            ("keystone", 1m, 3m),
            ("radius", 1500m, 1500m),
            ("is_alternate", 1m, 1m),
            ("revision", 1m, 1m)) with
        {
            SourceText = $"{carved}\n{conquered}\n{historic}",
        };
        var result = Import(
            raw: """
                Test Timeless
                Timeless Jewel
                Has Alt Variant: true
                Selected Variant: 2
                Selected Alt Variant: 4
                Variant: Keystone Alpha
                Variant: Brand Damage
                Variant: Elemental Damage
                Variant: Skill Cost (Pre 3.29.0)
                Implicits: 0
                Carved to glorify (2000-10000) new faithful converted by Example
                Passives in radius are Conquered by the Templars
                Historic
                {variant:2}4% increased Brand Damage per 10 Devotion
                {variant:3}4% increased Elemental Damage per 10 Devotion
                {variant:4}1% reduced Mana Cost of Skills per 10 Devotion
                """,
            modifiers:
            [
                seedModifier,
                Modifier("unique.brand", "brand_damage_per_devotion", 4, 4, "unique"),
                Modifier("unique.elemental", "elemental_damage_per_devotion", 4, 4, "unique"),
                Modifier("unique.skill-cost-old", "mana_cost_per_devotion", 1, 1, "unique"),
            ],
            translations:
            [
                Translation("brand", "brand_damage_per_devotion",
                    "{0}% increased Brand Damage per {0} Devotion", "#"),
                Translation("elemental", "elemental_damage_per_devotion",
                    "{0}% increased Elemental Damage per {0} Devotion", "#"),
                Translation("skill-cost-old", "mana_cost_per_devotion",
                    "{0}% reduced Mana Cost of Skills per {0} Devotion", "#"),
            ],
            optionAxes:
            [
                new SourceOptionAxisFixture(
                    SourceKind: "pobCoSelectableAxis",
                    SourceOrdinal: 1,
                    SelectionLimit: 2,
                    SourceChoiceIndices: [2, 3],
                    SelectedChoiceIndices: [2, 3]),
            ],
            baseItems: [new ItemBaseRecord { Name = "Timeless Jewel", Domain = "misc" }]);

        var versions = Assert.Single(result.Catalog!.Items).Versions;
        Assert.Single(versions, version => version.Role == UniqueItemVersionRole.Historical);
        var currentVersions = versions
            .Where(version => version.Role == UniqueItemVersionRole.Current)
            .ToArray();
        Assert.Single(currentVersions, version => version.Label == "Keystone Alpha");
        var current = Assert.Single(currentVersions,
            version => version.OptionAxes.Count > 0);
        var axis = Assert.Single(current.OptionAxes);
        Assert.Equal(2, axis.SelectionLimit);
        Assert.Equal(2, axis.Choices.Count);
        Assert.Contains(current.ModifierBlocks, block =>
            block.Lines.Contains("4% increased Brand Damage per 10 Devotion"));
        Assert.Contains(current.ModifierBlocks, block =>
            block.Lines.Contains("4% increased Elemental Damage per 10 Devotion"));
        Assert.DoesNotContain(versions, version =>
            version.Role == UniqueItemVersionRole.Current &&
            version.Label == "Brand Damage");
        Assert.DoesNotContain(versions, version =>
            version.Role == UniqueItemVersionRole.Current &&
            version.Label == "Elemental Damage");
    }

    [Fact]
    public void Import_TrueMutuallyExclusiveVariants_RemainSeparateVersionsWithoutOptionAxis()
    {
        var result = ImportSingle(
            """
                Test Flask
                Diamond Flask
                Variant: Pre 3.15.0
                Variant: Current
                Implicits: 0
                {variant:1}30% increased Chaos Damage
                {variant:2}250% increased Chaos Damage
                """,
            modifiers:
            [
                Modifier("unique.chaos-old", "chaos_old", 30, 30, "unique"),
                Modifier("unique.chaos-current", "chaos_current", 250, 250, "unique"),
            ],
            translations:
            [
                Translation("chaos-old", "chaos_old", "{0}% increased Chaos Damage", "#"),
                Translation("chaos-current", "chaos_current", "{0}% increased Chaos Damage", "#"),
            ]);

        var versions = Assert.Single(result.Catalog!.Items).Versions;
        Assert.Equal(2, versions.Count);
        Assert.All(versions, version => Assert.Empty(version.OptionAxes));
    }

    [Fact]
    public void Import_SplitStyleCoSelectableChoices_RemainOneCurrentVersion()
    {
        var result = ImportSingle(
            """
                Test Split
                Crimson Jewel
                Has Alt Variant: true
                Selected Variant: 2
                Selected Alt Variant: 3
                Variant: Strength
                Variant: Intelligence
                Variant: Energy Shield
                Limited to: 2
                Implicits: 0
                This Jewel's Socket has 25% increased effect per Allocated Passive Skill between it and your Class' starting location
                {variant:1}+5 to Strength
                {variant:2}+5 to Intelligence
                {variant:3}+5 to maximum Energy Shield
                """,
            modifiers:
            [
                Modifier("unique.path-effect", "path_effect", 25, 25, "unique"),
                Modifier("unique.strength", "strength", 5, 5, "unique"),
                Modifier("unique.intelligence", "intelligence", 5, 5, "unique"),
                Modifier("unique.energy-shield", "energy_shield", 5, 5, "unique"),
            ],
            translations:
            [
                Translation("path-effect", "path_effect",
                    "This Jewel's Socket has {0}% increased effect per Allocated Passive Skill between it and your Class' starting location",
                    "#"),
                Translation("strength", "strength", "{0} to Strength", "+#"),
                Translation("intelligence", "intelligence", "{0} to Intelligence", "+#"),
                Translation("energy-shield", "energy_shield", "{0} to maximum Energy Shield", "+#"),
            ]);

        var version = Assert.Single(Assert.Single(result.Catalog!.Items).Versions);
        var axis = Assert.Single(version.OptionAxes);
        Assert.Equal(2, axis.SelectionLimit);
        Assert.Equal(3, axis.Choices.Count);
    }

    [Fact]
    public void Import_MultilineMechanicalLines_MatchRegardlessOfDisplayOrder()
    {
        var modifier = new ModifierDefinition
        {
            Id = "UniqueJewelAlternateTreeInRadiusEternal",
            GroupId = "PassiveJewelGrantsRadius",
            GenerationType = ModifierGenerationType.Implicit,
            SourceGenerationType = "unique",
            Domain = "misc",
            Stats =
            [
                new ModifierStat { Index = 0, StatId = "local_unique_jewel_alternate_tree_version", MinValue = 5, MaxValue = 5 },
                new ModifierStat { Index = 1, StatId = "local_unique_jewel_alternate_tree_seed", MinValue = 2000, MaxValue = 160000 },
                new ModifierStat { Index = 2, StatId = "local_unique_jewel_alternate_tree_keystone", MinValue = 1, MaxValue = 3 },
                new ModifierStat { Index = 3, StatId = "local_jewel_effect_base_radius", MinValue = 1500, MaxValue = 1500 },
                new ModifierStat { Index = 4, StatId = "local_is_alternate_tree_jewel", MinValue = 1, MaxValue = 1 },
                new ModifierStat { Index = 5, StatId = "local_unique_jewel_alternate_tree_internal_revision", MinValue = 1, MaxValue = 1 },
            ],
            SourceText = """
                Commissioned (2000-160000) coins to commemorate Caspiro
                Passives in radius are Conquered by the Eternal Empire
                Historic
                """,
        };
        var result = ImportSingle(
            """
                Test Jewel
                Timeless Jewel
                Variant: Victario (Supreme Grandstanding)
                Implicits: 0
                Historic
                Passives in radius are Conquered by the Eternal Empire
                Commissioned (2000-160000) coins to commemorate Victario
                """,
            modifiers: [modifier],
            translations:
            [
                new StatTranslationDefinition
                {
                    Id = "timeless-seed",
                    StatIds =
                    [
                        "local_unique_jewel_alternate_tree_version",
                        "local_unique_jewel_alternate_tree_seed",
                        "local_unique_jewel_alternate_tree_keystone",
                        "local_unique_jewel_alternate_tree_internal_revision",
                    ],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions =
                            [
                                new StatTranslationCondition { Index = 0, MinValue = 5, MaxValue = 5 },
                                new StatTranslationCondition { Index = 1 },
                                new StatTranslationCondition { Index = 2, MinValue = 2, MaxValue = 2 },
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
                                "Commissioned {1} coins to commemorate Victario",
                                "Passives in radius are Conquered by the Eternal Empire",
                            ],
                        },
                    ],
                },
                new StatTranslationDefinition
                {
                    Id = "timeless-historic",
                    StatIds = ["local_is_alternate_tree_jewel"],
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
            baseItems: [new ItemBaseRecord { Name = "Timeless Jewel", Domain = "misc" }]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(3, block.Lines.Count);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["UniqueJewelAlternateTreeInRadiusEternal"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(6, block.MechanicalMapping.StatIds.Count);
    }

    [Fact]
    public void Import_CoSelectableLegacyAxisWithHistoricalVariant_SharesCurrentVersionPerKeystone()
    {
        const string carved = "Carved to glorify (2000-10000) new faithful converted by Example";
        const string conquered = "Passives in radius are Conquered by the Templars";
        const string historic = "Historic";
        var seedModifier = Modifier(
            "unique.timeless.seed",
            ("version", 4m, 4m),
            ("seed", 2000m, 10000m),
            ("keystone", 1m, 3m),
            ("radius", 1500m, 1500m),
            ("is_alternate", 1m, 1m),
            ("revision", 1m, 1m)) with
        {
            SourceText = $"{carved}\n{conquered}\n{historic}",
        };
        var result = Import(
            raw: """
                Test Timeless
                Timeless Jewel
                Has Alt Variant: true
                Selected Variant: 2
                Selected Alt Variant: 4
                Limited to: 1 Historic
                Variant: Keystone Alpha
                Variant: Brand Damage
                Variant: Elemental Damage
                Variant: Skill Cost (Pre 3.29.0)
                Implicits: 0
                Carved to glorify (2000-10000) new faithful converted by Example
                Passives in radius are Conquered by the Templars
                Historic
                {variant:2}4% increased Brand Damage per 10 Devotion
                {variant:3}4% increased Elemental Damage per 10 Devotion
                {variant:4}1% reduced Mana Cost of Skills per 10 Devotion
                """,
            modifiers:
            [
                seedModifier,
                Modifier("unique.brand", "brand_damage_per_devotion", 4, 4, "unique"),
                Modifier("unique.elemental", "elemental_damage_per_devotion", 4, 4, "unique"),
                Modifier("unique.skill-cost-old", "mana_cost_per_devotion", 1, 1, "unique"),
            ],
            translations:
            [
                Translation("brand", "brand_damage_per_devotion",
                    "{0}% increased Brand Damage per {0} Devotion", "#"),
                Translation("elemental", "elemental_damage_per_devotion",
                    "{0}% increased Elemental Damage per {0} Devotion", "#"),
                Translation("skill-cost-old", "mana_cost_per_devotion",
                    "{0}% reduced Mana Cost of Skills per {0} Devotion", "#"),
            ],
            optionAxes:
            [
                new SourceOptionAxisFixture(
                    SourceKind: "legacySharedVariantSelection",
                    SourceOrdinal: 1,
                    SelectionLimit: 3,
                    SourceChoiceIndices: [2, 3],
                    SelectedChoiceIndices: [2, 3]),
            ],
            baseItems: [new ItemBaseRecord { Name = "Timeless Jewel", Domain = "misc" }]);

        var versions = Assert.Single(result.Catalog!.Items).Versions;
        Assert.Single(versions, version => version.Role == UniqueItemVersionRole.Historical);
        var currentVersions = versions
            .Where(version => version.Role == UniqueItemVersionRole.Current)
            .ToArray();
        Assert.Single(currentVersions, version => version.Label == "Keystone Alpha");
        var current = Assert.Single(currentVersions,
            version => version.OptionAxes.Count > 0);
        var axis = Assert.Single(current.OptionAxes);
        Assert.Equal(2, axis.SelectionLimit);
        Assert.Equal(2, axis.Choices.Count);
        Assert.DoesNotContain(versions, version =>
            version.Role == UniqueItemVersionRole.Current &&
            version.Label == "Brand Damage");
    }

    [Fact]
    public void Import_MultilineWithMixedSemanticFingerprints_PreservesOneLogicalBlock()
    {
        var modifier = new ModifierDefinition
        {
            Id = "UniqueJewelAlternateTreeInRadiusEternal",
            GroupId = "PassiveJewelGrantsRadius",
            GenerationType = ModifierGenerationType.Implicit,
            SourceGenerationType = "unique",
            Domain = "misc",
            Stats =
            [
                new ModifierStat { Index = 0, StatId = "local_unique_jewel_alternate_tree_version", MinValue = 5, MaxValue = 5 },
                new ModifierStat { Index = 1, StatId = "local_unique_jewel_alternate_tree_seed", MinValue = 100, MaxValue = 8000 },
                new ModifierStat { Index = 2, StatId = "local_unique_jewel_alternate_tree_keystone", MinValue = 1, MaxValue = 1 },
                new ModifierStat { Index = 3, StatId = "local_jewel_effect_base_radius", MinValue = 1500, MaxValue = 1500 },
                new ModifierStat { Index = 4, StatId = "local_is_alternate_tree_jewel", MinValue = 1, MaxValue = 1 },
                new ModifierStat { Index = 5, StatId = "local_unique_jewel_alternate_tree_internal_revision", MinValue = 1, MaxValue = 1 },
            ],
            SourceText = """
                Commissioned (2000-160000) coins to commemorate Cadiro
                Passives in radius are Conquered by the Eternal Empire
                Historic
                """,
        };
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-pob-repr-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                entries = new[]
                {
                    new
                    {
                        uniqueType = "jewel",
                        sourcePath = "Data/Uniques/jewel.lua",
                        generated = false,
                        raw = """
                            Test Jewel
                            Timeless Jewel
                            Variant: Cadiro (Supreme Decadence)
                            Implicits: 0
                            Commissioned (2000-160000) coins to commemorate Cadiro
                            Passives in radius are Conquered by the Eternal Empire
                            Historic
                            """,
                        semanticFingerprints = new object[]
                        {
                            new
                            {
                                kind = "unique",
                                lineIndex = 0,
                                line = "Commissioned (2000-160000) coins to commemorate Cadiro",
                                baseType = "Timeless Jewel",
                                locality = "global",
                                evidenceMethod = "pob-item-context-v1",
                            },
                            new
                            {
                                kind = "unique",
                                lineIndex = 1,
                                line = "Passives in radius are Conquered by the Eternal Empire",
                                baseType = "Timeless Jewel",
                                locality = "unknown",
                                evidenceMethod = "pob-item-context-v1",
                            },
                            new
                            {
                                kind = "unique",
                                lineIndex = 2,
                                line = "Historic",
                                baseType = "Timeless Jewel",
                                locality = "unknown",
                                evidenceMethod = "pob-item-context-v1",
                            },
                        },
                    },
                },
            }));
            var result = new PoBUniqueCatalogImporter().Import(
                path,
                "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                "v2.67.2",
                "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                [modifier],
                [
                    new StatTranslationDefinition
                    {
                        Id = "timeless-seed",
                        StatIds =
                        [
                            "local_unique_jewel_alternate_tree_version",
                            "local_unique_jewel_alternate_tree_seed",
                            "local_unique_jewel_alternate_tree_keystone",
                            "local_unique_jewel_alternate_tree_internal_revision",
                        ],
                        Variants =
                        [
                            new StatTranslationVariant
                            {
                                Conditions =
                                [
                                    new StatTranslationCondition { Index = 0, MinValue = 5, MaxValue = 5 },
                                    new StatTranslationCondition { Index = 1 },
                                    new StatTranslationCondition { Index = 2, MinValue = 1, MaxValue = 1 },
                                    new StatTranslationCondition { Index = 3 },
                                ],
                                ValueFormats = ["ignore", "#", "ignore", "ignore"],
                                IndexHandlers =
                                [
                                    new StatTranslationIndexHandler { Index = 0 },
                                    new StatTranslationIndexHandler
                                    {
                                        Index = 1,
                                        Handlers = ["times_twenty"],
                                    },
                                    new StatTranslationIndexHandler { Index = 2 },
                                    new StatTranslationIndexHandler { Index = 3 },
                                ],
                                FormatLines =
                                [
                                    "Commissioned {1} coins to commemorate Cadiro",
                                    "Passives in radius are Conquered by the Eternal Empire",
                                ],
                            },
                        ],
                    },
                    new StatTranslationDefinition
                    {
                        Id = "timeless-historic",
                        StatIds = ["local_is_alternate_tree_jewel"],
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
                [new ItemBaseRecord { Name = "Timeless Jewel", Domain = "misc" }]);

            var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
                .ModifierBlocks);
            Assert.Equal(3, block.Lines.Count);
            Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
            Assert.Equal(["UniqueJewelAlternateTreeInRadiusEternal"], block.MechanicalMapping.ModifierIds);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_TimelessSeedRegressionControl_ImportsOneCompleteMultilineBlock()
    {
        var modifier = new ModifierDefinition
        {
            Id = "UniqueJewelAlternateTreeInRadiusEternal",
            GroupId = "PassiveJewelGrantsRadius",
            GenerationType = ModifierGenerationType.Implicit,
            SourceGenerationType = "unique",
            Domain = "misc",
            Stats =
            [
                new ModifierStat { Index = 0, StatId = "local_unique_jewel_alternate_tree_version", MinValue = 5, MaxValue = 5 },
                new ModifierStat { Index = 1, StatId = "local_unique_jewel_alternate_tree_seed", MinValue = 2000, MaxValue = 160000 },
                new ModifierStat { Index = 2, StatId = "local_unique_jewel_alternate_tree_keystone", MinValue = 1, MaxValue = 3 },
                new ModifierStat { Index = 3, StatId = "local_jewel_effect_base_radius", MinValue = 1500, MaxValue = 1500 },
                new ModifierStat { Index = 4, StatId = "local_is_alternate_tree_jewel", MinValue = 1, MaxValue = 1 },
                new ModifierStat { Index = 5, StatId = "local_unique_jewel_alternate_tree_internal_revision", MinValue = 1, MaxValue = 1 },
            ],
            SourceText = """
                Commissioned (2000-160000) coins to commemorate Victario
                Passives in radius are Conquered by the Eternal Empire
                Historic
                """,
        };
        var result = ImportSingle(
            """
                Test Jewel
                Timeless Jewel
                Variant: Victario (Supreme Grandstanding)
                Implicits: 0
                Commissioned (2000-160000) coins to commemorate Victario
                Passives in radius are Conquered by the Eternal Empire
                Historic
                """,
            modifiers: [modifier],
            translations:
            [
                new StatTranslationDefinition
                {
                    Id = "timeless-seed",
                    StatIds =
                    [
                        "local_unique_jewel_alternate_tree_version",
                        "local_unique_jewel_alternate_tree_seed",
                        "local_unique_jewel_alternate_tree_keystone",
                        "local_unique_jewel_alternate_tree_internal_revision",
                    ],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions =
                            [
                                new StatTranslationCondition { Index = 0, MinValue = 5, MaxValue = 5 },
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
                                "Commissioned {1} coins to commemorate Victario",
                                "Passives in radius are Conquered by the Eternal Empire",
                            ],
                        },
                    ],
                },
                new StatTranslationDefinition
                {
                    Id = "timeless-historic",
                    StatIds = ["local_is_alternate_tree_jewel"],
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
            baseItems: [new ItemBaseRecord { Name = "Timeless Jewel", Domain = "misc" }]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(3, block.Lines.Count);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["UniqueJewelAlternateTreeInRadiusEternal"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(6, block.MechanicalMapping.StatIds.Count);
        Assert.DoesNotContain(result.Catalog.Items
            .SelectMany(item => item.Versions)
            .SelectMany(version => version.ModifierBlocks), candidate =>
            candidate.Lines.Count == 1 &&
            candidate.Lines[0] == "Historic" &&
            candidate.MechanicalMapping.ModifierIds.Contains("UniqueJewelAlternateTreeInRadiusEternal"));
    }

    private sealed record SourceOptionAxisFixture(
        string SourceKind,
        int SourceOrdinal,
        int SelectionLimit,
        IReadOnlyList<int> SourceChoiceIndices,
        IReadOnlyList<int> SelectedChoiceIndices);

    private static PoBUniqueCatalogImportResult Import(
        string raw,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations,
        IReadOnlyList<SourceOptionAxisFixture>? optionAxes = null,
        IReadOnlyList<ItemBaseRecord>? baseItems = null) =>
        ImportSingle(raw, modifiers, translations, baseItems, optionAxes);

    private static PoBUniqueCatalogImportResult ImportSingle(
        string raw,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations,
        IReadOnlyList<ItemBaseRecord>? baseItems = null,
        IReadOnlyList<SourceOptionAxisFixture>? optionAxes = null,
        IReadOnlyList<StatDefinition>? stats = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-pob-repr-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                entries = new[]
                {
                    new
                    {
                        uniqueType = "jewel",
                        sourcePath = "Data/Uniques/jewel.lua",
                        generated = false,
                        raw,
                        optionAxes = optionAxes?.Select(axis => new
                        {
                            sourceKind = axis.SourceKind,
                            sourceOrdinal = axis.SourceOrdinal,
                            selectionLimit = axis.SelectionLimit,
                            sourceChoiceIndices = axis.SourceChoiceIndices,
                            selectedChoiceIndices = axis.SelectedChoiceIndices,
                        }).ToArray(),
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
                itemPropertySemantics: null,
                stats);
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
        string sourceGenerationType = "unique") => new()
    {
        Id = id,
        GroupId = id,
        GenerationType = ModifierGenerationType.Implicit,
        SourceGenerationType = sourceGenerationType,
        Domain = "misc",
        Stats = [new ModifierStat { Index = 0, StatId = statId, MinValue = min, MaxValue = max }],
    };

    private static ModifierDefinition Modifier(
        string id,
        params (string StatId, decimal Min, decimal Max)[] stats) => new()
    {
        Id = id,
        GroupId = id,
        GenerationType = ModifierGenerationType.Implicit,
        SourceGenerationType = "unique",
        Domain = "misc",
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
}
