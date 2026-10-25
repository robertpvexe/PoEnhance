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
