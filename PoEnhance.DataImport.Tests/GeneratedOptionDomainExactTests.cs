using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

/// <summary>
/// Generated Unique option pools (PoB veiledMods / ModJewelCharm) own unveiled and
/// affliction_charm Exact rows. Fixed Unique Exact must not claim those domains.
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
            Modifier("p", "s", 1, 1, "prefix") with { Domain = "item" }));
        Assert.False(PoBUniqueCatalogImporter.IsEligibleForStrictExactEnrollment(
            Modifier("craft", "s", 1, 1, "prefix") with { Domain = "crafted" }));
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
