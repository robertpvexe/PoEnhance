using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

/// <summary>
/// Generated Unique option pools (PoB veiledMods / ModJewelCharm) own unveiled and
/// affliction_charm Exact rows. Fixed Unique Exact must not claim those domains.
/// Cluster AfflictionNotable Disabled recovery is covered by dedicated Megalomaniac-style tests
/// without adding affliction_jewel to IsGeneratedOptionOwnedDomain.
/// </summary>
public sealed class GeneratedOptionDomainExactTests
{
    private const string VeiledBleedModId =
        "JunMasterVeiledLocalIncreasedPhysicalDamageAndBleedChanceCrafted_";
    private const string VeiledSpellBlockModId = "JunMasterVeiledSpellBlockPercent____";
    private const string CharmManaReservationModId = "AnimalCharmManaReservationEfficiency2";
    private const string CharmSuppressModId = "AnimalCharmSpellSuppressionChance2";
    private const string CharmOnKillModId = "AnimalCharmLifeManaESOnKill2";

    [Fact]
    public void Eligibility_UniqueGeneration_AndGeneratedOptionDomains_Only()
    {
        Assert.True(PoBUniqueCatalogImporter.IsEligibleForStrictExactEnrollment(
            Modifier("u", "s", 1, 1, "unique") with { Domain = "item" }));
        Assert.True(PoBUniqueCatalogImporter.IsEligibleForStrictExactEnrollment(
            Modifier("v", "s", 1, 1, "prefix") with { Domain = "unveiled" }));
        Assert.True(PoBUniqueCatalogImporter.IsEligibleForStrictExactEnrollment(
            Modifier("c", "s", 1, 1, "prefix") with { Domain = "affliction_charm" }));
        Assert.False(PoBUniqueCatalogImporter.IsEligibleForStrictExactEnrollment(
            Modifier("j", "s", 1, 1, "prefix") with { Domain = "affliction_jewel" }));
        Assert.False(PoBUniqueCatalogImporter.IsEligibleForStrictExactEnrollment(
            Modifier("p", "s", 1, 1, "prefix") with { Domain = "item" }));
        Assert.False(PoBUniqueCatalogImporter.IsEligibleForStrictExactEnrollment(
            Modifier("craft", "s", 1, 1, "prefix") with { Domain = "crafted" }));
    }

    [Fact]
    public void Import_GeneratedClusterNotable_DisabledAfflictionJewel_ExactOwnsNotable()
    {
        // Megalomaniac-style: RePoE AfflictionNotable with spawn_weights all zero is Disabled,
        // but PoB GeneratedCandidate + official Trade still own the ExactText.
        const string notableModId = "AfflictionNotableNoWitnesses";
        var notable = Modifier(
            notableModId,
            "local_affliction_notable_no_witnesses",
            1m,
            1m,
            "prefix") with
        {
            Domain = "affliction_jewel",
            SourceAvailability = ModifierSourceAvailability.Disabled,
        };

        var result = ImportSingle(
            """
                Megalomaniac
                Medium Cluster Jewel
                Selected Variant: 1
                Variant: No Witnesses
                Implicits: 0
                {variant:1}1 Added Passive Skill is No Witnesses
                """,
            generated: true,
            modifiers: [notable],
            translations:
            [
                Translation(
                    "no-witnesses",
                    "local_affliction_notable_no_witnesses",
                    "1 Added Passive Skill is No Witnesses",
                    "ignore"),
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Jewels/JewelPassiveTreeExpansionMedium",
                    Name = "Medium Cluster Jewel",
                    Domain = "affliction_jewel",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, block.SourceSemantics);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal([notableModId], block.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["local_affliction_notable_no_witnesses"],
            block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_GeneratedClusterNotable_PotentiallyEligibleAfflictionJewel_StillExact()
    {
        const string notableModId = "AfflictionNotableDiseaseVector";
        var notable = Modifier(
            notableModId,
            "local_affliction_notable_disease_vector",
            1m,
            1m,
            "prefix") with
        {
            Domain = "affliction_jewel",
            SourceAvailability = ModifierSourceAvailability.PotentiallyEligible,
        };

        var result = ImportSingle(
            """
                Megalomaniac
                Medium Cluster Jewel
                Selected Variant: 1
                Variant: Disease Vector
                Implicits: 0
                {variant:1}1 Added Passive Skill is Disease Vector
                """,
            generated: true,
            modifiers: [notable],
            translations:
            [
                Translation(
                    "disease-vector",
                    "local_affliction_notable_disease_vector",
                    "1 Added Passive Skill is Disease Vector",
                    "ignore"),
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Jewels/JewelPassiveTreeExpansionMedium",
                    Name = "Medium Cluster Jewel",
                    Domain = "affliction_jewel",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal([notableModId], block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_FixedUniqueMatchingAfflictionJewelDisplay_DoesNotExactClaimDisabledNotable()
    {
        // Negative: bare Fixed Unique must not Exact-claim Disabled affliction_jewel rows that
        // share display text with a GeneratedCandidate cluster option.
        const string notableModId = "AfflictionNotableNoWitnesses";
        var notable = Modifier(
            notableModId,
            "local_affliction_notable_no_witnesses",
            1m,
            1m,
            "prefix") with
        {
            Domain = "affliction_jewel",
            SourceAvailability = ModifierSourceAvailability.Disabled,
        };

        var result = ImportSingle(
            """
                Hypothetical Fixed Cluster
                Medium Cluster Jewel
                Implicits: 0
                1 Added Passive Skill is No Witnesses
                """,
            generated: false,
            modifiers: [notable],
            translations:
            [
                Translation(
                    "no-witnesses",
                    "local_affliction_notable_no_witnesses",
                    "1 Added Passive Skill is No Witnesses",
                    "ignore"),
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Jewels/JewelPassiveTreeExpansionMedium",
                    Name = "Medium Cluster Jewel",
                    Domain = "affliction_jewel",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierSourceSemantics.Fixed, block.SourceSemantics);
        Assert.Equal(
            UniqueModifierMechanicalMappingStatus.Unsupported,
            block.MechanicalMapping.Status);
        Assert.Empty(block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_GeneratedClusterNotable_LiveTwinBeatsDisabledRename()
    {
        // Evil Eye (PotentiallyEligible) must win over Disabled Whispers of Death sharing ExactText.
        const string liveModId = "AfflictionNotableEvilEye";
        const string disabledRenameId = "AfflictionNotableWhispersofDeath";
        var live = Modifier(
            liveModId,
            "local_affliction_notable_evil_eye",
            1m,
            1m,
            "prefix") with
        {
            Domain = "affliction_jewel",
            SourceAvailability = ModifierSourceAvailability.PotentiallyEligible,
        };
        var disabledRename = Modifier(
            disabledRenameId,
            "local_affliction_notable_whispers_of_death",
            1m,
            1m,
            "prefix") with
        {
            Domain = "affliction_jewel",
            SourceAvailability = ModifierSourceAvailability.Disabled,
        };

        var result = ImportSingle(
            """
                Megalomaniac
                Medium Cluster Jewel
                Selected Variant: 1
                Variant: Evil Eye
                Implicits: 0
                {variant:1}1 Added Passive Skill is Evil Eye
                """,
            generated: true,
            modifiers: [live, disabledRename],
            translations:
            [
                Translation(
                    "evil-eye",
                    "local_affliction_notable_evil_eye",
                    "1 Added Passive Skill is Evil Eye",
                    "ignore"),
                Translation(
                    "whispers",
                    "local_affliction_notable_whispers_of_death",
                    "1 Added Passive Skill is Evil Eye",
                    "ignore"),
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Jewels/JewelPassiveTreeExpansionMedium",
                    Name = "Medium Cluster Jewel",
                    Domain = "affliction_jewel",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal([liveModId], block.MechanicalMapping.ModifierIds);
        Assert.DoesNotContain(disabledRenameId, block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_GeneratedClusterNotable_ForbiddenWords_OwnsWardbreakerRename()
    {
        // PoB/Trade still display "Forbidden Words"; pinned RePoE mod id is Wardbreaker_.
        const string notableModId = "AfflictionNotableWardbreaker_";
        var notable = Modifier(
            notableModId,
            "local_affliction_notable_wardbreaker",
            1m,
            1m,
            "prefix") with
        {
            Domain = "affliction_jewel",
            SourceAvailability = ModifierSourceAvailability.Disabled,
        };

        var result = ImportSingle(
            """
                Megalomaniac
                Medium Cluster Jewel
                Selected Variant: 1
                Variant: Forbidden Words
                Implicits: 0
                {variant:1}1 Added Passive Skill is Forbidden Words
                """,
            generated: true,
            modifiers: [notable],
            translations:
            [
                Translation(
                    "forbidden-words",
                    "local_affliction_notable_wardbreaker",
                    "1 Added Passive Skill is Forbidden Words",
                    "ignore"),
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Jewels/JewelPassiveTreeExpansionMedium",
                    Name = "Medium Cluster Jewel",
                    Domain = "affliction_jewel",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal([notableModId], block.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["local_affliction_notable_wardbreaker"],
            block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_GeneratedVeiledBleedMultiline_ExactOwnsUnveiledMod_NotUniqueBleedPool()
    {
        var uniqueBleedCompetitor = Modifier(
            "CausesBleedingUnique__1",
            "local_chance_to_bleed_on_hit_%",
            21m,
            25m,
            "unique");
        var veiled = new ModifierDefinition
        {
            Id = VeiledBleedModId,
            GroupId = VeiledBleedModId,
            Name = VeiledBleedModId,
            GenerationType = ModifierGenerationType.Prefix,
            SourceGenerationType = "prefix",
            Domain = "unveiled",
            SourceText =
                "(120-139)% increased Physical Damage\n(21-25)% chance to cause Bleeding on Hit",
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = "local_physical_damage_+%",
                    MinValue = 120m,
                    MaxValue = 139m,
                },
                new ModifierStat
                {
                    Index = 1,
                    StatId = "local_chance_to_bleed_on_hit_%",
                    MinValue = 21m,
                    MaxValue = 25m,
                },
            ],
        };

        var result = ImportSingle(
            """
                Paradoxica
                Vaal Rapier
                Selected Variant: 1
                Variant: (Prefix) Increased Physical Damage And Bleed Chance
                Implicits: 0
                {variant:1}(120-139)% increased Physical Damage
                {variant:1}(21-25)% chance to cause Bleeding on Hit
                """,
            generated: true,
            modifiers: [veiled, uniqueBleedCompetitor],
            translations:
            [
                Translation(
                    "phys",
                    "local_physical_damage_+%",
                    "{0}% increased Physical Damage",
                    "#"),
                Translation(
                    "bleed",
                    "local_chance_to_bleed_on_hit_%",
                    "{0}% chance to cause Bleeding on Hit",
                    "#"),
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Weapons/OneHandWeapons/ThrustingOneHandSwords/Rapier9",
                    Name = "Vaal Rapier",
                    Domain = "item",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, block.SourceSemantics);
        Assert.Equal(2, block.Lines.Count);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal([VeiledBleedModId], block.MechanicalMapping.ModifierIds);
        Assert.Contains(
            PoBUniqueCatalogImporter.GeneratedOptionDomainExactReason,
            block.MechanicalMapping.Provenance!.ResolutionReasons);
        Assert.DoesNotContain(
            "CausesBleedingUnique__1",
            block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_GeneratedVeiledBleed_BleedLineAlone_DoesNotExactClaimVeiledComposition()
    {
        // Incomplete composition must not Exact-claim the multi-stat unveiled mod from bleed alone.
        // Two Unique-generation bleed Exact competitors keep the orphan line Ambiguous/fail-closed.
        var uniqueBleedA = Modifier(
            "CausesBleedingUnique__1",
            "local_chance_to_bleed_on_hit_%",
            21m,
            25m,
            "unique");
        var uniqueBleedB = Modifier(
            "CausesBleedingUnique__2",
            "local_chance_to_bleed_on_hit_%",
            21m,
            25m,
            "unique");
        var veiled = new ModifierDefinition
        {
            Id = VeiledBleedModId,
            GroupId = VeiledBleedModId,
            GenerationType = ModifierGenerationType.Prefix,
            SourceGenerationType = "prefix",
            Domain = "unveiled",
            SourceText =
                "(120-139)% increased Physical Damage\n(21-25)% chance to cause Bleeding on Hit",
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = "local_physical_damage_+%",
                    MinValue = 120m,
                    MaxValue = 139m,
                },
                new ModifierStat
                {
                    Index = 1,
                    StatId = "local_chance_to_bleed_on_hit_%",
                    MinValue = 21m,
                    MaxValue = 25m,
                },
            ],
        };

        var result = ImportSingle(
            """
                Paradoxica
                Vaal Rapier
                Selected Variant: 1
                Variant: Bleed Only Line
                Implicits: 0
                {variant:1}(21-25)% chance to cause Bleeding on Hit
                """,
            generated: true,
            modifiers: [veiled, uniqueBleedA, uniqueBleedB],
            translations:
            [
                Translation(
                    "phys",
                    "local_physical_damage_+%",
                    "{0}% increased Physical Damage",
                    "#"),
                Translation(
                    "bleed",
                    "local_chance_to_bleed_on_hit_%",
                    "{0}% chance to cause Bleeding on Hit",
                    "#"),
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Weapons/OneHandWeapons/ThrustingOneHandSwords/Rapier9",
                    Name = "Vaal Rapier",
                    Domain = "item",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, block.SourceSemantics);
        Assert.True(
            block.MechanicalMapping.Status is
                UniqueModifierMechanicalMappingStatus.Ambiguous or
                UniqueModifierMechanicalMappingStatus.EquivalentSourceSet,
            $"Unexpected status {block.MechanicalMapping.Status}");
        Assert.DoesNotContain(VeiledBleedModId, block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_GeneratedCharmManaReservation_ExactOwnsAfflictionCharm_NotUniquePool()
    {
        var uniqueCompetitor = Modifier(
            "ManaReservationEfficiencyUnique__1",
            "base_mana_reservation_efficiency_+%",
            8m,
            12m,
            "unique");
        var charm = Modifier(
            CharmManaReservationModId,
            "base_mana_reservation_efficiency_+%",
            8m,
            12m,
            "prefix") with
        {
            Domain = "affliction_charm",
        };

        var result = ImportSingle(
            """
                That Which Was Taken
                Cobalt Jewel
                Selected Variant: 1
                Variant: Mana Reservation
                Implicits: 0
                {variant:1}(8-12)% increased Mana Reservation Efficiency of Skills
                """,
            generated: true,
            modifiers: [charm, uniqueCompetitor],
            translations:
            [
                Translation(
                    "mana-res",
                    "base_mana_reservation_efficiency_+%",
                    "{0}% increased Mana Reservation Efficiency of Skills",
                    "#"),
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Jewels/JewelInt",
                    Name = "Cobalt Jewel",
                    Domain = "misc",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal([CharmManaReservationModId], block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_GeneratedCharmSuppress_ExactOwnsAfflictionCharm()
    {
        var uniqueCompetitor = Modifier(
            "ChanceToSuppressSpellsUnique__1",
            "base_spell_suppression_chance_%",
            10m,
            15m,
            "unique");
        var charm = Modifier(
            CharmSuppressModId,
            "base_spell_suppression_chance_%",
            10m,
            15m,
            "prefix") with
        {
            Domain = "affliction_charm",
        };

        var result = ImportSingle(
            """
                That Which Was Taken
                Cobalt Jewel
                Selected Variant: 1
                Variant: Suppress
                Implicits: 0
                {variant:1}+(10-15)% chance to Suppress Spell Damage
                """,
            generated: true,
            modifiers: [charm, uniqueCompetitor],
            translations:
            [
                Translation(
                    "suppress",
                    "base_spell_suppression_chance_%",
                    "{0}% chance to Suppress Spell Damage",
                    "+#"),
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Jewels/JewelInt",
                    Name = "Cobalt Jewel",
                    Domain = "misc",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal([CharmSuppressModId], block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_GeneratedCharmOnKillMultiline_ExactOwnsWholeCharmMod()
    {
        var uniqueMana = Modifier(
            "MaximumManaOnKillPercentUnique__1",
            "recover_%_maximum_mana_on_kill",
            2m,
            2m,
            "unique");
        var charm = new ModifierDefinition
        {
            Id = CharmOnKillModId,
            GroupId = CharmOnKillModId,
            GenerationType = ModifierGenerationType.Prefix,
            SourceGenerationType = "prefix",
            Domain = "affliction_charm",
            SourceText =
                "Recover 2% of Life on Kill\nRecover 2% of Mana on Kill\nRecover 2% of Energy Shield on Kill",
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = "recover_%_maximum_life_on_kill",
                    MinValue = 2m,
                    MaxValue = 2m,
                },
                new ModifierStat
                {
                    Index = 1,
                    StatId = "recover_%_maximum_mana_on_kill",
                    MinValue = 2m,
                    MaxValue = 2m,
                },
                new ModifierStat
                {
                    Index = 2,
                    StatId = "recover_%_maximum_energy_shield_on_kill",
                    MinValue = 2m,
                    MaxValue = 2m,
                },
            ],
        };

        var result = ImportSingle(
            """
                That Which Was Taken
                Cobalt Jewel
                Selected Variant: 1
                Variant: On Kill Recover
                Implicits: 0
                {variant:1}Recover 2% of Life on Kill
                {variant:1}Recover 2% of Mana on Kill
                {variant:1}Recover 2% of Energy Shield on Kill
                """,
            generated: true,
            modifiers: [charm, uniqueMana],
            translations:
            [
                Translation(
                    "life-kill",
                    "recover_%_maximum_life_on_kill",
                    "Recover {0}% of Life on Kill",
                    "#"),
                Translation(
                    "mana-kill",
                    "recover_%_maximum_mana_on_kill",
                    "Recover {0}% of Mana on Kill",
                    "#"),
                Translation(
                    "es-kill",
                    "recover_%_maximum_energy_shield_on_kill",
                    "Recover {0}% of Energy Shield on Kill",
                    "#"),
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Jewels/JewelInt",
                    Name = "Cobalt Jewel",
                    Domain = "misc",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(3, block.Lines.Count);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal([CharmOnKillModId], block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_GeneratedVeiledSpellBlock_ExactOwnsUnveiled_NotUniqueSpellBlockPool()
    {
        var uniqueSpellBlock = Modifier(
            "SpellBlockPercentageUnique__2",
            "base_spell_block_%",
            9m,
            10m,
            "unique");
        var veiled = Modifier(
            VeiledSpellBlockModId,
            "base_spell_block_%",
            9m,
            10m,
            "prefix") with
        {
            Domain = "unveiled",
        };

        var result = ImportSingle(
            """
                The Queen's Hunger
                Vaal Regalia
                Selected Variant: 1
                Variant: (Prefix) Spell Block Percent
                Implicits: 0
                {variant:1}(9-10)% Chance to Block Spell Damage
                """,
            generated: true,
            modifiers: [veiled, uniqueSpellBlock],
            translations:
            [
                Translation(
                    "spell-block",
                    "base_spell_block_%",
                    "{0}% Chance to Block Spell Damage",
                    "#"),
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Armours/BodyArmours/BodyInt3C",
                    Name = "Vaal Regalia",
                    Domain = "item",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal([VeiledSpellBlockModId], block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_FixedUniqueMatchingVeiledDisplay_DoesNotExactClaimUnveiledDomain()
    {
        var uniqueSpellBlock = Modifier(
            "SpellBlockPercentageUnique__2",
            "base_spell_block_%",
            9m,
            10m,
            "unique");
        var veiled = Modifier(
            VeiledSpellBlockModId,
            "base_spell_block_%",
            9m,
            10m,
            "prefix") with
        {
            Domain = "unveiled",
        };

        var result = ImportSingle(
            """
                Wings of Entropy
                Sundering Axe
                Implicits: 0
                (9-10)% Chance to Block Spell Damage
                """,
            generated: false,
            modifiers: [veiled, uniqueSpellBlock],
            translations:
            [
                Translation(
                    "spell-block",
                    "base_spell_block_%",
                    "{0}% Chance to Block Spell Damage",
                    "#"),
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Weapons/TwoHandWeapons/TwoHandAxes/TwoHandAxe7",
                    Name = "Sundering Axe",
                    Domain = "item",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierSourceSemantics.Fixed, block.SourceSemantics);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["SpellBlockPercentageUnique__2"], block.MechanicalMapping.ModifierIds);
        Assert.DoesNotContain(VeiledSpellBlockModId, block.MechanicalMapping.ModifierIds);
    }

    private const string VeiledFlaskIgniteModId =
        "JunMasterVeiledLocalFlaskPhysicalDamageCanIgnite";
    private const string VeiledFlaskRarityModId =
        "JunMasterVeiledLocalFlaskItemFoundRarityDuringFlaskEffect";
    private const string UniqueFlaskRarityModId = "FlaskItemRarityUniqueFlask1";

    [Fact]
    public void Import_FixedOptionAxisFlaskUnveil_ExactOwnsUnveiledDomain()
    {
        // Cinderswallow-style Fixed+OptionAxis ExactText owns RePoE unveiled flask rows.
        // Bare Fixed must still refuse unveiled (Import_FixedUniqueMatchingVeiledDisplay_*).
        var veiled = new ModifierDefinition
        {
            Id = VeiledFlaskIgniteModId,
            GroupId = VeiledFlaskIgniteModId,
            Name = "Catarina's",
            GenerationType = ModifierGenerationType.Prefix,
            SourceGenerationType = "prefix",
            Domain = "unveiled",
            SourceText = "Your Physical Damage can Ignite during Effect",
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = "local_flask_physical_damage_can_ignite_during_effect",
                    MinValue = 1m,
                    MaxValue = 1m,
                },
            ],
        };

        var result = ImportSingle(
            """
                Test Silver Urn
                Silver Flask
                Has Alt Variant: true
                Selected Variant: 1
                Selected Alt Variant: 2
                Variant: Current
                Variant: Physical Damage can Ignite
                Implicits: 0
                {variant:1}+(10-20) to Maximum Charges
                {variant:2}{crafted}Your Physical Damage can Ignite during Effect
                """,
            generated: false,
            modifiers:
            [
                Modifier("FlaskMaxChargesUnique__1", "local_flask_maximum_charges_+", 10, 20, "unique"),
                veiled,
            ],
            translations:
            [
                Translation(
                    "charges",
                    "local_flask_maximum_charges_+",
                    "{0} to Maximum Charges",
                    "#|+#"),
                Translation(
                    "ignite",
                    "local_flask_physical_damage_can_ignite_during_effect",
                    "Your Physical Damage can Ignite during Effect"),
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Flasks/FlaskUtility14",
                    Name = "Silver Flask",
                    Domain = "flask",
                },
            ]);

        var version = Assert.Single(Assert.Single(result.Catalog!.Items).Versions);
        var ignite = Assert.Single(
            version.ModifierBlocks,
            block => block.Lines[0].Contains("Physical Damage can Ignite", StringComparison.Ordinal));
        Assert.Equal(UniqueModifierSourceSemantics.Fixed, ignite.SourceSemantics);
        Assert.NotEmpty(ignite.OptionChoiceMemberships);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, ignite.MechanicalMapping.Status);
        Assert.Equal([VeiledFlaskIgniteModId], ignite.MechanicalMapping.ModifierIds);
        Assert.Contains(
            PoBUniqueCatalogImporter.GeneratedOptionDomainExactReason,
            ignite.MechanicalMapping.Provenance!.ResolutionReasons);
    }

    [Fact]
    public void Import_FixedOptionAxisFlask_PrefersUniqueGenerationOverUnveiledSameDisplay()
    {
        // Real RePoE: Unique ExactText is (30-50)% while PoB/Cinderswallow option is (20-30)%.
        // Unveiled ExactText matches (20-30)%; Fixed option-axis must still prefer Unique broad
        // ownership (FlaskItemRarityUniqueFlask1) over flipping to the unveiled twin.
        var uniqueRarity = Modifier(
            UniqueFlaskRarityModId,
            "local_unique_flask_item_rarity_+%_while_healing",
            30m,
            50m,
            "unique") with
        {
            Domain = "flask",
            SourceText = "(30-50)% increased Rarity of Items found during Effect",
        };
        var veiledRarity = new ModifierDefinition
        {
            Id = VeiledFlaskRarityModId,
            GroupId = VeiledFlaskRarityModId,
            Name = "of the Order",
            GenerationType = ModifierGenerationType.Prefix,
            SourceGenerationType = "prefix",
            Domain = "unveiled",
            SourceText = "(20-30)% increased Rarity of Items found during Effect",
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = "local_flask_item_found_rarity_+%_during_flask_effect",
                    MinValue = 20m,
                    MaxValue = 30m,
                },
            ],
        };

        var result = ImportSingle(
            """
                Test Silver Urn
                Silver Flask
                Has Alt Variant: true
                Selected Variant: 1
                Selected Alt Variant: 2
                Variant: Current
                Variant: Item Rarity
                Implicits: 0
                {variant:1}+(10-20) to Maximum Charges
                {variant:2}{crafted}(20-30)% increased Rarity of Items found during Effect
                """,
            generated: false,
            modifiers:
            [
                Modifier("FlaskMaxChargesUnique__1", "local_flask_maximum_charges_+", 10, 20, "unique"),
                uniqueRarity,
                veiledRarity,
            ],
            translations:
            [
                Translation(
                    "charges",
                    "local_flask_maximum_charges_+",
                    "{0} to Maximum Charges",
                    "#|+#"),
                Translation(
                    "rarity-unique",
                    "local_unique_flask_item_rarity_+%_while_healing",
                    "{0}% increased Rarity of Items found during Effect",
                    "#"),
                Translation(
                    "rarity-veiled",
                    "local_flask_item_found_rarity_+%_during_flask_effect",
                    "{0}% increased Rarity of Items found during Effect",
                    "#"),
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Flasks/FlaskUtility14",
                    Name = "Silver Flask",
                    Domain = "flask",
                },
            ]);

        var version = Assert.Single(Assert.Single(result.Catalog!.Items).Versions);
        var rarity = Assert.Single(
            version.ModifierBlocks,
            block => block.Lines[0].Contains("Rarity of Items found", StringComparison.Ordinal));
        Assert.Equal(UniqueModifierSourceSemantics.Fixed, rarity.SourceSemantics);
        Assert.NotEmpty(rarity.OptionChoiceMemberships);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, rarity.MechanicalMapping.Status);
        Assert.Equal([UniqueFlaskRarityModId], rarity.MechanicalMapping.ModifierIds);
        Assert.DoesNotContain(VeiledFlaskRarityModId, rarity.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_FixedOptionAxisFlask_ReducedReflectWording_RemainsUnsupported()
    {
        // PoB "(60-80)% reduced Reflected Damage taken during Effect" has no RePoE ExactText;
        // Prevent +(80-60)% form must not synonym-claim Exact.
        var preventReflect = new ModifierDefinition
        {
            Id = "JunMasterVeiledLocalFlaskReducedReflectDuringEffect",
            GroupId = "JunMasterVeiledLocalFlaskReducedReflectDuringEffect",
            Name = "of the Order",
            GenerationType = ModifierGenerationType.Prefix,
            SourceGenerationType = "prefix",
            Domain = "unveiled",
            SourceText = "Prevent +(80-60)% of Reflected Damage during Effect",
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = "local_flask_-%_of_your_damage_cannot_be_reflected",
                    MinValue = -80m,
                    MaxValue = -60m,
                },
            ],
        };

        var result = ImportSingle(
            """
                Test Silver Urn
                Silver Flask
                Has Alt Variant: true
                Selected Variant: 1
                Selected Alt Variant: 2
                Variant: Current
                Variant: Reduced Reflected Damage Taken
                Implicits: 0
                {variant:1}+(10-20) to Maximum Charges
                {variant:2}{crafted}(60-80)% reduced Reflected Damage taken during Effect
                """,
            generated: false,
            modifiers:
            [
                Modifier("FlaskMaxChargesUnique__1", "local_flask_maximum_charges_+", 10, 20, "unique"),
                preventReflect,
            ],
            translations:
            [
                Translation(
                    "charges",
                    "local_flask_maximum_charges_+",
                    "{0} to Maximum Charges",
                    "#|+#"),
                Translation(
                    "prevent-reflect",
                    "local_flask_-%_of_your_damage_cannot_be_reflected",
                    "Prevent +{0}% of Reflected Damage during Effect",
                    "#|+#"),
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Flasks/FlaskUtility14",
                    Name = "Silver Flask",
                    Domain = "flask",
                },
            ]);

        var version = Assert.Single(Assert.Single(result.Catalog!.Items).Versions);
        var reflect = Assert.Single(
            version.ModifierBlocks,
            block => block.Lines[0].Contains("reduced Reflected Damage", StringComparison.Ordinal));
        Assert.Equal(UniqueModifierSourceSemantics.Fixed, reflect.SourceSemantics);
        Assert.NotEmpty(reflect.OptionChoiceMemberships);
        Assert.Equal(
            UniqueModifierMechanicalMappingStatus.Unsupported,
            reflect.MechanicalMapping.Status);
        Assert.Empty(reflect.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_GeneratedQueensHungerFortification_DivideByFive_ExactOwnsUnveiled()
    {
        // U.2.5M: JunMasterVeiledFortifyEffectWhileFocused_ stores raw 50 with RePoE
        // divide_by_five → PoB/AID "+10 to maximum Fortification while Focused".
        const string veiledId = "JunMasterVeiledFortifyEffectWhileFocused_";
        var veiled = new ModifierDefinition
        {
            Id = veiledId,
            GroupId = "FortifyEffectWhileFocused",
            Name = "of the Order",
            GenerationType = ModifierGenerationType.Suffix,
            SourceGenerationType = "suffix",
            Domain = "unveiled",
            SourceAvailability = ModifierSourceAvailability.PotentiallyEligible,
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = "max_fortification_while_focused_+1_per_5",
                    MinValue = 50m,
                    MaxValue = 50m,
                },
                new ModifierStat
                {
                    Index = 1,
                    StatId = "grant_focus_skill",
                    MinValue = 1m,
                    MaxValue = 1m,
                },
            ],
        };

        var result = ImportSingle(
            """
                The Queen's Hunger
                Vaal Regalia
                Selected Variant: 1
                Variant: (Suffix) Fortify Effect While Focused
                Implicits: 0
                {variant:1}{crafted}+10 to maximum Fortification while Focused
                """,
            generated: true,
            modifiers: [veiled],
            translations:
            [
                new StatTranslationDefinition
                {
                    Id = "fortify-focused",
                    StatIds = ["max_fortification_while_focused_+1_per_5"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0, MinValue = null, MaxValue = null }],
                            ValueFormats = ["+#"],
                            IndexHandlers =
                            [
                                new StatTranslationIndexHandler
                                {
                                    Index = 0,
                                    Handlers = ["divide_by_five"],
                                },
                            ],
                            FormatLines = ["{0} to maximum Fortification while Focused"],
                        },
                    ],
                },
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Armours/BodyArmours/BodyInt3C",
                    Name = "Vaal Regalia",
                    Domain = "item",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, block.SourceSemantics);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal([veiledId], block.MechanicalMapping.ModifierIds);
        Assert.Contains(
            "max_fortification_while_focused_+1_per_5",
            block.MechanicalMapping.StatIds,
            StringComparer.Ordinal);
    }

    [Fact]
    public void Import_GeneratedQueensHungerFortification_WrongProjectedValue_RemainsUnsupported()
    {
        // Negative: PoB "+9 …" must not Exact-own the veiled raw-50 / divide_by_five (+10) row.
        var veiled = new ModifierDefinition
        {
            Id = "JunMasterVeiledFortifyEffectWhileFocused_",
            GroupId = "FortifyEffectWhileFocused",
            GenerationType = ModifierGenerationType.Suffix,
            SourceGenerationType = "suffix",
            Domain = "unveiled",
            SourceAvailability = ModifierSourceAvailability.PotentiallyEligible,
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = "max_fortification_while_focused_+1_per_5",
                    MinValue = 50m,
                    MaxValue = 50m,
                },
                new ModifierStat
                {
                    Index = 1,
                    StatId = "grant_focus_skill",
                    MinValue = 1m,
                    MaxValue = 1m,
                },
            ],
        };

        var result = ImportSingle(
            """
                The Queen's Hunger
                Vaal Regalia
                Selected Variant: 1
                Variant: (Suffix) Fortify Effect While Focused
                Implicits: 0
                {variant:1}{crafted}+9 to maximum Fortification while Focused
                """,
            generated: true,
            modifiers: [veiled],
            translations:
            [
                new StatTranslationDefinition
                {
                    Id = "fortify-focused",
                    StatIds = ["max_fortification_while_focused_+1_per_5"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0 }],
                            ValueFormats = ["+#"],
                            IndexHandlers =
                            [
                                new StatTranslationIndexHandler
                                {
                                    Index = 0,
                                    Handlers = ["divide_by_five"],
                                },
                            ],
                            FormatLines = ["{0} to maximum Fortification while Focused"],
                        },
                    ],
                },
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Armours/BodyArmours/BodyInt3C",
                    Name = "Vaal Regalia",
                    Domain = "item",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Unsupported, block.MechanicalMapping.Status);
        Assert.Empty(block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_FixedQueensHungerOfferingLines_WithoutTradeWording_RemainUnsupported()
    {
        // U.2.5M Offering NO-FIX guard: PoB uses "or" and omits "in sequence"; RePoE/Trade use
        // commas + "in sequence" as one multi-line Unique FormatLines. Do not alias those words.
        var unique = new ModifierDefinition
        {
            Id = "TriggerRandomOfferingSkillUnique__1",
            GroupId = "GrantedSkill",
            GenerationType = ModifierGenerationType.Implicit,
            SourceGenerationType = "unique",
            Domain = "item",
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = "local_display_trigger_level_X_offering_every_5_seconds",
                    MinValue = 20m,
                    MaxValue = 20m,
                },
                new ModifierStat
                {
                    Index = 1,
                    StatId = "buff_impl_stat",
                    MinValue = 0m,
                    MaxValue = 0m,
                },
            ],
        };

        var result = ImportSingle(
            """
                The Queen's Hunger
                Vaal Regalia
                Implicits: 0
                Trigger Level 20 Bone Offering, Flesh Offering or Spirit Offering every 5 seconds
                Offering Skills Triggered this way also affect you
                """,
            generated: true,
            modifiers: [unique],
            translations:
            [
                new StatTranslationDefinition
                {
                    Id = "offering-trigger",
                    StatIds = ["local_display_trigger_level_X_offering_every_5_seconds"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0, MinValue = 1 }],
                            ValueFormats = ["#"],
                            IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
                            FormatLines =
                            [
                                "Trigger Level {0} Bone Offering, Flesh Offering, Spirit Offering every 5 seconds in sequence",
                                "Offering Skills Triggered this way also affect you",
                            ],
                        },
                    ],
                },
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Armours/BodyArmours/BodyInt3C",
                    Name = "Vaal Regalia",
                    Domain = "item",
                },
            ]);

        var version = Assert.Single(Assert.Single(result.Catalog!.Items).Versions);
        Assert.Equal(2, version.ModifierBlocks.Count);
        Assert.All(
            version.ModifierBlocks,
            block =>
            {
                Assert.Equal(
                    UniqueModifierMechanicalMappingStatus.Unsupported,
                    block.MechanicalMapping.Status);
                Assert.Empty(block.MechanicalMapping.ModifierIds);
            });
    }

    [Fact]
    public void Import_FixedNonGeneratedFortification_DoesNotClaimUnveiledDivideByFive()
    {
        // Negative: Fixed Unique without GeneratedCandidate must not Exact-claim unveiled domain.
        var veiled = new ModifierDefinition
        {
            Id = "JunMasterVeiledFortifyEffectWhileFocused_",
            GroupId = "FortifyEffectWhileFocused",
            GenerationType = ModifierGenerationType.Suffix,
            SourceGenerationType = "suffix",
            Domain = "unveiled",
            SourceAvailability = ModifierSourceAvailability.PotentiallyEligible,
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = "max_fortification_while_focused_+1_per_5",
                    MinValue = 50m,
                    MaxValue = 50m,
                },
                new ModifierStat
                {
                    Index = 1,
                    StatId = "grant_focus_skill",
                    MinValue = 1m,
                    MaxValue = 1m,
                },
            ],
        };

        var result = ImportSingle(
            """
                Unrelated Regalia
                Vaal Regalia
                Implicits: 0
                +10 to maximum Fortification while Focused
                """,
            generated: false,
            modifiers: [veiled],
            translations:
            [
                new StatTranslationDefinition
                {
                    Id = "fortify-focused",
                    StatIds = ["max_fortification_while_focused_+1_per_5"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0 }],
                            ValueFormats = ["+#"],
                            IndexHandlers =
                            [
                                new StatTranslationIndexHandler
                                {
                                    Index = 0,
                                    Handlers = ["divide_by_five"],
                                },
                            ],
                            FormatLines = ["{0} to maximum Fortification while Focused"],
                        },
                    ],
                },
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Armours/BodyArmours/BodyInt3C",
                    Name = "Vaal Regalia",
                    Domain = "item",
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierSourceSemantics.Fixed, block.SourceSemantics);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Unsupported, block.MechanicalMapping.Status);
        Assert.Empty(block.MechanicalMapping.ModifierIds);
    }

    private static PoBUniqueCatalogImportResult ImportSingle(
        string raw,
        bool generated,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations,
        IReadOnlyList<ItemBaseRecord>? baseItems = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-gen-opt-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                entries = new[]
                {
                    new
                    {
                        uniqueType = generated ? "generated" : "weapon",
                        sourcePath = generated
                            ? "Data/Uniques/Special/Generated.lua"
                            : "Data/Uniques/axe.lua",
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
        string sourceGenerationType) => new()
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
}
