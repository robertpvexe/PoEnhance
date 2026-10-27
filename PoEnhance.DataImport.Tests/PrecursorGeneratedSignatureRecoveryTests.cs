using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

/// <summary>
/// U.2.5C — shared NormalizeSignature / broad-key recovery for Precursor's Emblem
/// GeneratedCandidate charge options that fail UNIQUE_GENERATED_MECHANICS_NOT_FOUND solely due to
/// proven PoB↔RePoE text shape gaps (cosmetic +#, consonant article, FormatLines newline fold).
/// </summary>
public sealed class PrecursorGeneratedSignatureRecoveryTests
{
    [Fact]
    public void StripOptionalPositivePlus_AliasesBarePoBAgainstPlusHashRender()
    {
        var withPlus = PoBUniqueCatalogImporter.NormalizeSignature(
            "+(1-2)% Chance to Block Attack Damage per Endurance Charge");
        var bare = PoBUniqueCatalogImporter.NormalizeSignature(
            "(1-2)% Chance to Block Attack Damage per Endurance Charge");

        Assert.Equal("+<number>% Chance to Block Attack Damage per Endurance Charge", withPlus);
        Assert.Equal("<number>% Chance to Block Attack Damage per Endurance Charge", bare);
        Assert.Equal(bare, PoBUniqueCatalogImporter.StripOptionalPositivePlusFromSignature(withPlus));
    }

    [Fact]
    public void StripRePoEDisplayLinks_UsesDisplayTextOnly()
    {
        Assert.Equal(
            "Roiling Tempest",
            PoBUniqueCatalogImporter.StripRePoEDisplayLinks(
                "[KeystoneLightningPurist|Roiling Tempest]"));
        Assert.Equal(
            "Areas contain Abysses",
            PoBUniqueCatalogImporter.StripRePoEDisplayLinks(
                "Areas contain [ContainsAbyss|Abysses]"));
        Assert.Equal(
            "Elemental Equilibrium",
            PoBUniqueCatalogImporter.StripRePoEDisplayLinks("Elemental Equilibrium"));
        Assert.Equal(
            "Roiling Tempest",
            PoBUniqueCatalogImporter.NormalizeSignature(
                "[KeystoneLightningPurist|Roiling Tempest]"));
    }

    [Fact]
    public void NormalizeSignature_PreservesPositivePlusAndNegativeSign()
    {
        Assert.Equal(
            "+<number>% Chance to Block Spell Damage",
            PoBUniqueCatalogImporter.NormalizeSignature("+(4-6)% Chance to Block Spell Damage"));
        Assert.Equal(
            "-<number> Physical Damage taken from Attack Hits",
            PoBUniqueCatalogImporter.NormalizeSignature(
                "-(30-60) Physical Damage taken from Attack Hits"));
        Assert.NotEqual(
            PoBUniqueCatalogImporter.NormalizeSignature("+(30-60) Physical Damage taken from Attack Hits"),
            PoBUniqueCatalogImporter.NormalizeSignature("-(30-60) Physical Damage taken from Attack Hits"));
    }

    [Fact]
    public void Import_FixedPlusBlockSpell_DoesNotCollapseIntoBareHashCompetitorPool()
    {
        // Regression guard for Craiceann/Rathpith: PoB "+(n)% Chance to Block Spell Damage" must
        // keep resolving the +# Staff Spell Block pool, not Ambiguous broad merge with a bare-"#"
        // different-StatId competitor that only shares English after stripping '+'.
        var result = ImportSingle(
            """
                Craiceann's Chitin
                Magistrate Crown
                Implicits: 0
                +(4-6)% Chance to Block Spell Damage
                """,
            generated: false,
            modifiers:
            [
                Modifier("StaffSpellBlockPercent2", "base_spell_block_%", 4m, 6m),
                Modifier("BlockingBlocksSpellsUnique__2", "block_while_dual_wielding_spells_%", 4m, 6m),
            ],
            translations:
            [
                Translation(
                    "staff-plus",
                    "base_spell_block_%",
                    "{0}% Chance to Block Spell Damage",
                    "+#"),
                Translation(
                    "blocking-bare",
                    "block_while_dual_wielding_spells_%",
                    "{0}% Chance to Block Spell Damage",
                    "#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierSourceSemantics.Fixed, block.SourceSemantics);
        Assert.NotEqual(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
        Assert.Equal(["StaffSpellBlockPercent2"], block.MechanicalMapping.ModifierIds);
        Assert.DoesNotContain("BlockingBlocksSpellsUnique__2", block.MechanicalMapping.ModifierIds);
    }

    [Theory]
    [InlineData(
        "(10-30)% chance to gain an Frenzy Charge on Kill",
        "(10-30)% chance to gain a Frenzy Charge on Kill")]
    [InlineData(
        "(10-30)% chance to gain an Power Charge on Kill",
        "(10-30)% chance to gain a Power Charge on Kill")]
    public void NormalizeSignature_ArticleBeforeConsonant_MatchesRePoEArticle(string pob, string repoe) =>
        Assert.Equal(
            PoBUniqueCatalogImporter.NormalizeSignature(pob),
            PoBUniqueCatalogImporter.NormalizeSignature(repoe));

    [Fact]
    public void NormalizeSignature_ArticleBeforeVowel_Preserved()
    {
        Assert.Equal(
            "<number>% chance to gain an Endurance Charge on Kill",
            PoBUniqueCatalogImporter.NormalizeSignature(
                "(10-30)% chance to gain an Endurance Charge on Kill"));
        Assert.NotEqual(
            PoBUniqueCatalogImporter.NormalizeSignature(
                "(10-30)% chance to gain an Endurance Charge on Kill"),
            PoBUniqueCatalogImporter.NormalizeSignature(
                "(10-30)% chance to gain a Endurance Charge on Kill"));
    }

    [Fact]
    public void ComposeBroadSignatureKey_FoldsFormattingNewlinesWithoutSorting()
    {
        var multiline = PoBUniqueCatalogImporter.ComposeBroadSignatureKey(
        [
            PoBUniqueCatalogImporter.NormalizeSignature(
                "(15-45)% chance that if you would gain Power Charges, you instead gain up to"),
            PoBUniqueCatalogImporter.NormalizeSignature(
                "your maximum number of Power Charges"),
        ]);
        var single = PoBUniqueCatalogImporter.ComposeBroadSignatureKey(
        [
            PoBUniqueCatalogImporter.NormalizeSignature(
                "(15-45)% chance that if you would gain Power Charges, you instead gain up to your maximum number of Power Charges"),
        ]);

        Assert.Equal(single, multiline);
        Assert.DoesNotContain('\n', multiline);
    }

    [Fact]
    public void Import_GeneratedBlockWithoutPlus_MatchesUniqueGenerationPlusHashFormat()
    {
        var result = ImportGeneratedChargeOption(
            "(1-2)% Chance to Block Attack Damage per Endurance Charge",
            Modifier(
                "ChargeBonusBlockChancePerEnduranceCharge",
                "additional_attack_block_%_per_endurance_charge",
                1m,
                2m),
            Translation(
                "block-endurance",
                "additional_attack_block_%_per_endurance_charge",
                "{0}% Chance to Block Attack Damage per Endurance Charge",
                "+#"));

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, block.SourceSemantics);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(
            ["ChargeBonusBlockChancePerEnduranceCharge"],
            block.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["additional_attack_block_%_per_endurance_charge"],
            block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_GeneratedSuppressWithoutPlus_MatchesUniqueGenerationPlusHashFormat()
    {
        var result = ImportGeneratedChargeOption(
            "(1-2)% chance to Suppress Spell Damage per Power Charge",
            Modifier(
                "ChargeBonusDodgeChancePerPowerCharge",
                "spell_suppression_chance_%_per_power_charge",
                1m,
                2m),
            Translation(
                "suppress-power",
                "spell_suppression_chance_%_per_power_charge",
                "{0}% chance to Suppress Spell Damage per Power Charge",
                "+#"));

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(
            ["ChargeBonusDodgeChancePerPowerCharge"],
            block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_GeneratedAnFrenzyOnKill_MatchesRePoEArticle()
    {
        var result = ImportGeneratedChargeOption(
            "(10-30)% chance to gain an Frenzy Charge on Kill",
            Modifier(
                "ChargeBonusFrenzyChargeOnKill",
                "add_frenzy_charge_on_kill_%_chance",
                10m,
                30m),
            Translation(
                "frenzy-on-kill",
                "add_frenzy_charge_on_kill_%_chance",
                "{0}% chance to gain a Frenzy Charge on Kill",
                "#"));

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(
            ["ChargeBonusFrenzyChargeOnKill"],
            block.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["add_frenzy_charge_on_kill_%_chance"],
            block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_GeneratedAnPowerOnKill_MatchesRePoEArticle()
    {
        var result = ImportGeneratedChargeOption(
            "(10-30)% chance to gain an Power Charge on Kill",
            Modifier(
                "ChargeBonusPowerChargeOnKill",
                "add_power_charge_on_kill_%_chance",
                10m,
                30m),
            Translation(
                "power-on-kill",
                "add_power_charge_on_kill_%_chance",
                "{0}% chance to gain a Power Charge on Kill",
                "#"));

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(
            ["ChargeBonusPowerChargeOnKill"],
            block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_GeneratedAnEnduranceOnKill_RemainsExactControl()
    {
        var result = ImportGeneratedChargeOption(
            "(10-30)% chance to gain an Endurance Charge on Kill",
            Modifier(
                "ChargeBonusEnduranceChargeOnKill",
                "endurance_charge_on_kill_%",
                10m,
                30m),
            Translation(
                "endurance-on-kill",
                "endurance_charge_on_kill_%",
                "{0}% chance to gain an Endurance Charge on Kill",
                "#"));

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(
            ["ChargeBonusEnduranceChargeOnKill"],
            block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_GeneratedPowerMaxSingleLine_MatchesMultilineFormatLines()
    {
        var result = ImportGeneratedChargeOption(
            "(15-45)% chance that if you would gain Power Charges, you instead gain up to your maximum number of Power Charges",
            Modifier(
                "ChargeBonusChanceToGainMaximumPowerCharges",
                "gain_maximum_power_charges_on_power_charge_gained_%_chance",
                15m,
                45m),
            new StatTranslationDefinition
            {
                Id = "power-max",
                StatIds = ["gain_maximum_power_charges_on_power_charge_gained_%_chance"],
                Variants =
                [
                    new StatTranslationVariant
                    {
                        Conditions = [new StatTranslationCondition { Index = 0 }],
                        FormatLines =
                        [
                            "{0}% chance that if you would gain Power Charges, you instead gain up to",
                            "your maximum number of Power Charges",
                        ],
                        ValueFormats = ["#"],
                        IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
                    },
                ],
            });

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(
            ["ChargeBonusChanceToGainMaximumPowerCharges"],
            block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_GeneratedFrenzyMax_RemainsExactControl()
    {
        var result = ImportGeneratedChargeOption(
            "(15-45)% chance that if you would gain Frenzy Charges, you instead gain up to your maximum number of Frenzy Charges",
            Modifier(
                "ChargeBonusChanceToGainMaximumFrenzyCharges",
                "gain_maximum_frenzy_charges_on_frenzy_charge_gained_%_chance",
                15m,
                45m),
            Translation(
                "frenzy-max",
                "gain_maximum_frenzy_charges_on_frenzy_charge_gained_%_chance",
                "{0}% chance that if you would gain Frenzy Charges, you instead gain up to your maximum number of Frenzy Charges",
                "#"));

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(
            ["ChargeBonusChanceToGainMaximumFrenzyCharges"],
            block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void NormalizeSignature_CollapsesYourMaximumNumberOf_ToMaximum()
    {
        Assert.Equal(
            PoBUniqueCatalogImporter.NormalizeSignature(
                "(15-45)% chance that if you would gain Endurance Charges, you instead gain up to maximum Endurance Charges"),
            PoBUniqueCatalogImporter.NormalizeSignature(
                "(15-45)% chance that if you would gain Endurance Charges, you instead gain up to your maximum number of Endurance Charges"));
        Assert.Equal(
            PoBUniqueCatalogImporter.NormalizeSignature(
                "(15-45)% chance that if you would gain Frenzy Charges, you instead gain up to your maximum number of Frenzy Charges"),
            PoBUniqueCatalogImporter.NormalizeSignature(
                "(15-45)% chance that if you would gain Frenzy Charges, you instead gain up to maximum Frenzy Charges"));
    }

    [Fact]
    public void StripOptionalAddedDamageQualifier_AliasesBarePoBAgainstAddedRender()
    {
        var withAdded = PoBUniqueCatalogImporter.NormalizeSignature(
            "(6-24) to (12-39) Added Cold Damage per Frenzy Charge");
        var bare = PoBUniqueCatalogImporter.NormalizeSignature(
            "(6-24) to (12-39) Cold Damage per Frenzy Charge");
        Assert.Equal(
            bare,
            PoBUniqueCatalogImporter.StripOptionalAddedDamageQualifierFromSignature(withAdded));
        Assert.Equal(
            bare,
            PoBUniqueCatalogImporter.StripOptionalAddedDamageQualifierFromSignature(bare));
    }

    [Fact]
    public void ExpandGainNumberToIndefiniteArticleAliases_CoversPoBArticleForm()
    {
        var numeric = PoBUniqueCatalogImporter.NormalizeSignature(
            "Gain 1 Endurance Charge every second if you've been Hit Recently");
        var aliases = PoBUniqueCatalogImporter.ExpandGainNumberToIndefiniteArticleAliases(numeric);
        Assert.Contains(
            PoBUniqueCatalogImporter.NormalizeSignature(
                "Gain an Endurance Charge every second if you've been Hit Recently"),
            aliases);
        Assert.Contains(
            PoBUniqueCatalogImporter.NormalizeSignature(
                "Gain a Endurance Charge every second if you've been Hit Recently"),
            aliases);
    }

    [Fact]
    public void Import_GeneratedEnduranceMaxWording_MatchesRePoEShortForm()
    {
        // PoB long form vs RePoE/Trade short form — same ChargeBonus max-chance mechanic as Frenzy.
        var result = ImportGeneratedChargeOption(
            "(15-45)% chance that if you would gain Endurance Charges, you instead gain up to your maximum number of Endurance Charges",
            Modifier(
                "ChargeBonusChanceToGainMaximumEnduranceCharges",
                "gain_maximum_endurance_charges_on_endurance_charge_gained_%_chance",
                15m,
                45m),
            Translation(
                "endurance-max",
                "gain_maximum_endurance_charges_on_endurance_charge_gained_%_chance",
                "{0}% chance that if you would gain Endurance Charges, you instead gain up to maximum Endurance Charges",
                "#"));

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(
            ["ChargeBonusChanceToGainMaximumEnduranceCharges"],
            block.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["gain_maximum_endurance_charges_on_endurance_charge_gained_%_chance"],
            block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_GeneratedHitRecentlyArticleVsNumeric_MatchesFixedOneRePoE()
    {
        var result = ImportGeneratedChargeOption(
            "Gain an Endurance Charge every second if you've been Hit Recently",
            Modifier(
                "ChargeBonusEnduranceChargeIfHitRecently",
                "gain_endurance_charge_per_second_if_have_been_hit_recently",
                1m,
                1m),
            Translation(
                "hit-recently",
                "gain_endurance_charge_per_second_if_have_been_hit_recently",
                "Gain {0} Endurance Charge every second if you've been Hit Recently",
                "#"));

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(
            ["ChargeBonusEnduranceChargeIfHitRecently"],
            block.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["gain_endurance_charge_per_second_if_have_been_hit_recently"],
            block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_GeneratedColdMissingAdded_MatchesRePoEAddedCold()
    {
        // Fire sibling Exact without "Added" on both sides; Cold PoB omits "Added" while
        // RePoE/Trade/live AID include it. Optional-Added broad alias recovers ChargeBonus.
        var result = ImportGeneratedChargeOption(
            "(6-24) to (12-39) Cold Damage per Frenzy Charge",
            TwoStatModifier(
                "ChargeBonusAddedColdDamagePerFrenzyCharge",
                "minimum_added_cold_damage_per_frenzy_charge",
                6m,
                24m,
                "maximum_added_cold_damage_per_frenzy_charge",
                12m,
                39m),
            TwoStatTranslation(
                "cold-added",
                "minimum_added_cold_damage_per_frenzy_charge",
                "maximum_added_cold_damage_per_frenzy_charge",
                "{0} to {1} Added Cold Damage per Frenzy Charge"));

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(
            ["ChargeBonusAddedColdDamagePerFrenzyCharge"],
            block.MechanicalMapping.ModifierIds);
        Assert.Equal(
            [
                "minimum_added_cold_damage_per_frenzy_charge",
                "maximum_added_cold_damage_per_frenzy_charge",
            ],
            block.MechanicalMapping.StatIds);
    }

    [Fact]
    public void Import_GeneratedColdAdded_DoesNotStealUnrelatedIncreasedColdPerFrenzy()
    {
        // Negative: "% increased Cold Damage per Frenzy" must not Exact via optional-Added alias.
        var result = ImportGeneratedChargeOption(
            "(6-24) to (12-39) Cold Damage per Frenzy Charge",
            Modifier(
                "SomeIncreasedColdPerFrenzy",
                "cold_damage_+%_per_frenzy_charge",
                6m,
                24m),
            Translation(
                "increased-cold",
                "cold_damage_+%_per_frenzy_charge",
                "{0}% increased Cold Damage per Frenzy Charge",
                "#"));

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Unsupported, block.MechanicalMapping.Status);
        Assert.Empty(block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_GeneratedFireDamageSibling_RemainsExactControl()
    {
        var result = ImportGeneratedChargeOption(
            "(7-27) to (13-42) Fire Damage per Endurance Charge",
            TwoStatModifier(
                "ChargeBonusAddedFireDamagePerEnduranceCharge",
                "minimum_added_fire_damage_per_endurance_charge",
                7m,
                27m,
                "maximum_added_fire_damage_per_endurance_charge",
                13m,
                42m),
            TwoStatTranslation(
                "fire-added",
                "minimum_added_fire_damage_per_endurance_charge",
                "maximum_added_fire_damage_per_endurance_charge",
                "{0} to {1} Fire Damage per Endurance Charge"));

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(
            ["ChargeBonusAddedFireDamagePerEnduranceCharge"],
            block.MechanicalMapping.ModifierIds);
    }

    [Fact]
    public void Import_UnrelatedUniqueWithSameBlockDisplay_DoesNotCrossClaimChargeBonus()
    {
        // Negative control: Fixed Unique with Block-per-Endurance text must not steal ChargeBonus
        // when a non-charge Unique-generation competitor also exists — and must not invent Exact
        // via article/plus rules alone when no matching Unique-generation row is present.
        var result = ImportSingle(
            """
                Unrelated Emblem
                Prismatic Ring
                Implicits: 0
                (1-2)% Chance to Block Attack Damage per Endurance Charge
                """,
            generated: false,
            modifiers:
            [
                Modifier(
                    "SomeOtherUniqueBlock",
                    "additional_attack_block_%_per_endurance_charge",
                    1m,
                    2m),
            ],
            translations:
            [
                Translation(
                    "block-endurance",
                    "additional_attack_block_%_per_endurance_charge",
                    "{0}% Chance to Block Attack Damage per Endurance Charge",
                    "+#"),
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierSourceSemantics.Fixed, block.SourceSemantics);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["SomeOtherUniqueBlock"], block.MechanicalMapping.ModifierIds);
    }

    private static PoBUniqueCatalogImportResult ImportGeneratedChargeOption(
        string optionLine,
        ModifierDefinition modifier,
        StatTranslationDefinition translation)
    {
        var raw =
            "Precursor's Emblem\n" +
            "Prismatic Ring\n" +
            "Selected Variant: 1\n" +
            "Variant: Endurance - Block Attacks\n" +
            "Implicits: 0\n" +
            "{variant:1}" + optionLine;
        return ImportSingle(
            raw,
            generated: true,
            modifiers: [modifier],
            translations: [translation]);
    }

    private static PoBUniqueCatalogImportResult ImportSingle(
        string raw,
        bool generated,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations)
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-precursor-{Guid.NewGuid():N}.json");
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
                [
                    new ItemBaseRecord
                    {
                        Id = "Metadata/Items/Rings/Ring12",
                        Name = "Prismatic Ring",
                        Domain = "item",
                    },
                ]);
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
        decimal max) => new()
    {
        Id = id,
        GroupId = id,
        Name = id,
        GenerationType = ModifierGenerationType.Implicit,
        SourceGenerationType = "unique",
        Domain = "item",
        Stats = [new ModifierStat { Index = 0, StatId = statId, MinValue = min, MaxValue = max }],
    };

    private static ModifierDefinition TwoStatModifier(
        string id,
        string statId0,
        decimal min0,
        decimal max0,
        string statId1,
        decimal min1,
        decimal max1) => new()
    {
        Id = id,
        GroupId = id,
        Name = id,
        GenerationType = ModifierGenerationType.Implicit,
        SourceGenerationType = "unique",
        Domain = "item",
        Stats =
        [
            new ModifierStat { Index = 0, StatId = statId0, MinValue = min0, MaxValue = max0 },
            new ModifierStat { Index = 1, StatId = statId1, MinValue = min1, MaxValue = max1 },
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
                ValueFormats = valueFormats,
                IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
            },
        ],
    };

    private static StatTranslationDefinition TwoStatTranslation(
        string id,
        string statId0,
        string statId1,
        string format) => new()
    {
        Id = id,
        StatIds = [statId0, statId1],
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions =
                [
                    new StatTranslationCondition { Index = 0 },
                    new StatTranslationCondition { Index = 1 },
                ],
                FormatLines = [format],
                ValueFormats = ["#", "#"],
                IndexHandlers =
                [
                    new StatTranslationIndexHandler { Index = 0 },
                    new StatTranslationIndexHandler { Index = 1 },
                ],
            },
        ],
    };
}
