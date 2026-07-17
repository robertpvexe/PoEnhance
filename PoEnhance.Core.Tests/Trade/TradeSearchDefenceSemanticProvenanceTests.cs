using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Tests.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeSearchDefenceSemanticProvenanceTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemModifierCandidateResolver resolver = new();
    private readonly TradeSearchDraftMapper mapper = new();

    [Fact]
    public void ActiveGameDataRecordHarnesses_AttachEveryQualityAndDefenceDescriptorWithOrderedTargets()
    {
        foreach (var semanticCase in ActiveSemanticCases)
        {
            var catalog = Catalog(
                [semanticCase.Modifier],
                [semanticCase.Translation],
                [semanticCase.Semantic]);
            var item = parser.Parse(HarnessItem(semanticCase));
            var parsedModifier = Assert.Single(item.Modifiers);
            var resolution = new ModifierCandidateResolutionResult(
                0,
                parsedModifier,
                parsedModifier.Name,
                parsedModifier.Kind,
                semanticCase.Modifier.GenerationType,
                ModifierCandidateResolutionStatus.Exact,
                [semanticCase.Modifier],
                [],
                Locality: ModifierLocality.Local);

            var result = mapper.CreateDraft(
                item,
                modifierResolutions: [resolution],
                gameDataCatalog: catalog);

            Assert.True(result.IsSuccess, semanticCase.Semantic.Id);
            var draft = Assert.IsType<TradeSearchDraft>(result.Draft);
            var component = Assert.Single(draft.ModifierFilters);
            Assert.Equal(semanticCase.Modifier.Id, component.ResolvedModifierId);
            Assert.Equal(ModifierLocality.Local, component.Locality);
            Assert.Equal(ModifierStatMappingProofStatus.ProvenExact, component.StatMappingProof);
            Assert.Equal(semanticCase.Semantic.Id, component.ReviewedItemPropertySemantic?.Id);
            var contribution = Assert.Single(component.ReviewedItemPropertySemantic!.Contributions);
            Assert.Equal(semanticCase.Targets, contribution.Targets);
            Assert.Equal(semanticCase.Operation, contribution.Operation);
            Assert.Empty(draft.ItemPropertyContributionGroups);
        }
    }

    [Theory]
    [InlineData(8, "item.armour.increased-percent.local", "item.armour.added.local")]
    [InlineData(9, "item.energy-shield.increased-percent.local")]
    [InlineData(
        11,
        "item.armour.added.local",
        "item.evasion.added.local",
        "item.armour-evasion.increased-percent.local")]
    [InlineData(
        13,
        "item.energy-shield.added.local",
        "item.evasion-energy-shield.increased-percent.local",
        "item.evasion.added.local")]
    public void GenuineCopiedDefenceCorpus_AttachesOnlySafelyProvenDirectComponents(
        int fixtureIndex,
        params string[] expectedSemanticIds)
    {
        var draft = CreateCorpusDraft(CopiedItemCorpus.LoadItems()[fixtureIndex]);

        Assert.Equal(
            expectedSemanticIds,
            draft.ModifierFilters
                .Where(component => component.ReviewedItemPropertySemantic is not null)
                .Select(component => component.ReviewedItemPropertySemantic!.Id));
        Assert.All(
            draft.ModifierFilters.Where(component => component.ReviewedItemPropertySemantic is not null),
            component => Assert.Equal(
                ModifierStatMappingProofStatus.ProvenExact,
                component.StatMappingProof));
        Assert.Equal(
            expectedSemanticIds
                .SelectMany(id => draft.ModifierFilters
                    .Where(component => component.ReviewedItemPropertySemantic?.Id == id)
                    .SelectMany(component => component.ReviewedItemPropertySemantic!.Contributions)
                    .SelectMany(contribution => contribution.Targets))
                .Where(target => target is ItemPropertyTarget.Armour or ItemPropertyTarget.Evasion or
                    ItemPropertyTarget.EnergyShield or ItemPropertyTarget.Ward or ItemPropertyTarget.Block)
                .Select(TargetKind)
                .Distinct(),
            draft.ItemPropertyContributionGroups.Select(group => group.ParentKind));
    }

    [Fact]
    public void GenuineNecroticArmourHasNoModifierContributorsAndCreatesNoDefenceGroups()
    {
        var draft = CreateCorpusDraft(CopiedItemCorpus.LoadItems()[0]);

        Assert.Empty(draft.ModifierFilters);
        Assert.Empty(draft.ItemPropertyContributionGroups);
    }

    [Fact]
    public void GenuineSlinkBootsStunAndBlockRecoveryIsNotBlockChance()
    {
        var draft = CreateCorpusDraft(SlinkBoots);

        var recovery = Assert.Single(draft.ModifierFilters);
        Assert.Contains("Stun and Block Recovery", recovery.OriginalText, StringComparison.Ordinal);
        Assert.Null(recovery.ReviewedItemPropertySemantic);
        Assert.Empty(draft.ItemPropertyContributionGroups);
    }

    [Fact]
    public void WardDescriptorsHaveActiveRecordCoverageButNoGenuineCopiedFixture()
    {
        // Tracked fixture gap: there is no genuine copied Ward item in either copied-item corpus.
        Assert.Equal(
            ["item.ward.added.local", "item.ward.increased-percent.local"],
            ActiveSemanticCases
                .Where(semanticCase => semanticCase.Targets.SequenceEqual([ItemPropertyTarget.Ward]))
                .Select(semanticCase => semanticCase.Semantic.Id));
    }

    private TradeSearchDraft CreateCorpusDraft(string itemText)
    {
        var catalog = CorpusCatalog();
        var item = parser.Parse(itemText);
        var result = mapper.CreateDraft(
            item,
            modifierResolutions: resolver.Resolve(item, catalog),
            gameDataCatalog: catalog);

        Assert.True(result.IsSuccess);
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }

    private static TradeSearchItemPropertyKind TargetKind(ItemPropertyTarget target) => target switch
    {
        ItemPropertyTarget.Armour => TradeSearchItemPropertyKind.Armour,
        ItemPropertyTarget.Evasion => TradeSearchItemPropertyKind.EvasionRating,
        ItemPropertyTarget.EnergyShield => TradeSearchItemPropertyKind.EnergyShield,
        ItemPropertyTarget.Ward => TradeSearchItemPropertyKind.Ward,
        ItemPropertyTarget.Block => TradeSearchItemPropertyKind.ChanceToBlock,
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };

    private static GameDataCatalog CorpusCatalog()
    {
        const string life = "base_maximum_life";
        const string stunRecovery = "base_stun_recovery_+%";
        var modifiers = new[]
        {
            Mod("LocalIncreasedPhysicalDamageReductionRatingPercent5", "Thickened",
                ModifierGenerationType.Prefix, "local_physical_damage_reduction_rating_+%"),
            Mod("LocalBaseArmourAndLife4", "Crocodile's", ModifierGenerationType.Prefix,
                "local_base_physical_damage_reduction_rating", life),
            Mod("LocalIncreasedEnergyShieldPercentAndStunRecovery3", "Boggart's",
                ModifierGenerationType.Prefix, "local_energy_shield_+%", stunRecovery),
            Mod("LocalBaseArmourAndEvasionRating7", "Adaptable", ModifierGenerationType.Prefix,
                "local_base_physical_damage_reduction_rating", "local_base_evasion_rating"),
            Mod("LocalIncreasedArmourAndEvasionAndStunRecovery5", "Elephant's",
                ModifierGenerationType.Prefix, "local_armour_and_evasion_+%", stunRecovery),
            Mod("LocalIncreasedArmourAndEvasion5", "Duelist's", ModifierGenerationType.Prefix,
                "local_armour_and_evasion_+%"),
            Mod("LocalBaseEnergyShieldAndLife2", "Prior's", ModifierGenerationType.Prefix,
                "local_energy_shield", life),
            Mod("LocalIncreasedEvasionAndEnergyShieldAndStunRecovery4", "Wasp's",
                ModifierGenerationType.Prefix, "local_evasion_and_energy_shield_+%", stunRecovery),
            Mod("LocalBaseEvasionRatingAndEnergyShield4", "Cherub's", ModifierGenerationType.Prefix,
                "local_base_evasion_rating", "local_energy_shield"),
            Mod("StunRecoveryOnly", "of Steel Skin", ModifierGenerationType.Suffix, stunRecovery),
        };
        var translations = new[]
        {
            Translation("armour-percent", "local_physical_damage_reduction_rating_+%",
                "{0}% increased Armour"),
            Translation("armour-flat", "local_base_physical_damage_reduction_rating", "{0} to Armour", "+#"),
            Translation("evasion-flat", "local_base_evasion_rating", "{0} to Evasion Rating", "+#"),
            Translation("energy-shield-flat", "local_energy_shield", "{0} to maximum Energy Shield", "+#"),
            Translation("energy-shield-percent", "local_energy_shield_+%", "{0}% increased Energy Shield"),
            Translation("armour-evasion-percent", "local_armour_and_evasion_+%",
                "{0}% increased Armour and Evasion"),
            Translation("evasion-energy-shield-percent", "local_evasion_and_energy_shield_+%",
                "{0}% increased Evasion and Energy Shield"),
            Translation("life", life, "{0} to maximum Life", "+#"),
            Translation("stun-recovery", stunRecovery, "{0}% increased Stun and Block Recovery"),
        };
        var semantics = ActiveSemanticCases
            .Where(semanticCase => semanticCase.Targets.Any(target => target is
                ItemPropertyTarget.Armour or
                ItemPropertyTarget.Evasion or
                ItemPropertyTarget.EnergyShield))
            .Select(semanticCase => semanticCase.Semantic)
            .ToArray();
        return Catalog(modifiers, translations, semantics);
    }

    private static GameDataCatalog Catalog(
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations,
        IReadOnlyList<ItemPropertySemanticDescriptor> semantics)
    {
        var localStatIds = semantics
            .SelectMany(semantic => semantic.OrderedStatIds)
            .ToHashSet(StringComparer.Ordinal);
        var statIds = modifiers
            .SelectMany(modifier => modifier.Stats.Select(stat => stat.StatId))
            .Concat(translations.SelectMany(translation => translation.StatIds))
            .Concat(localStatIds)
            .Where(statId => !string.IsNullOrWhiteSpace(statId))
            .Select(statId => statId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 1,
                DataVersion = "active-record-defence-semantics-test",
                CreatedAtUtc = DateTimeOffset.UnixEpoch,
                League = "test",
                Patch = "test",
                Sources =
                [
                    new GameDataPackageSource
                    {
                        SourceId = "repoe",
                        RetrievedAtUtc = DateTimeOffset.UnixEpoch,
                        SourceVersion = "c50acab2ed660a70511e7f91ee09db4e632089e4",
                        SourceUri = "https://github.com/repoe-fork/repoe",
                    },
                ],
            },
            Modifiers = modifiers,
            Stats = statIds.Select(statId => new StatDefinition
            {
                Id = statId,
                IsLocal = localStatIds.Contains(statId),
            }).ToArray(),
            StatTranslations = translations,
            ItemPropertySemantics = semantics,
        });
    }

    private static string HarnessItem(ActiveSemanticCase semanticCase)
    {
        var header = string.IsNullOrWhiteSpace(semanticCase.Modifier.Name)
            ? "{ Implicit Modifier }"
            : $"{{ Prefix Modifier \"{semanticCase.Modifier.Name}\" }}";
        return $$"""
            Item Class: Body Armours
            Rarity: Rare
            Active Record Harness
            Vaal Regalia
            --------
            Item Level: 85
            --------
            {{header}}
            {{semanticCase.CopiedLine}}
            """;
    }

    private static ModifierDefinition Mod(
        string id,
        string? name,
        ModifierGenerationType generationType,
        params string[] statIds)
    {
        return new ModifierDefinition
        {
            Id = id,
            GroupId = $"group.{id}",
            Name = name,
            GenerationType = generationType,
            Domain = "item",
            Stats = statIds.Select((statId, index) => new ModifierStat
            {
                Index = index,
                StatId = statId,
            }).ToArray(),
        };
    }

    private static StatTranslationDefinition Translation(
        string id,
        string statId,
        string format,
        string valueFormat = "#")
    {
        return new StatTranslationDefinition
        {
            Id = id,
            StatIds = [statId],
            Variants =
            [
                new StatTranslationVariant
                {
                    Conditions = [new StatTranslationCondition { Index = 0 }],
                    ValueFormats = [valueFormat],
                    IndexHandlers = [new StatTranslationIndexHandler { Index = 0, Handlers = [] }],
                    FormatLines = [format],
                },
            ],
        };
    }

    private static ActiveSemanticCase Case(
        string descriptorId,
        string modifierId,
        string? modifierName,
        ModifierGenerationType generationType,
        string statId,
        string copiedLine,
        string translationFormat,
        string valueFormat,
        ItemPropertyOperation operation,
        params ItemPropertyTarget[] targets)
    {
        var semantic = new ItemPropertySemanticDescriptor
        {
            Id = descriptorId,
            OrderedStatIds = [statId],
            Contributions =
            [
                new ItemPropertyContribution
                {
                    Targets = targets,
                    Operation = operation,
                },
            ],
            Applicability = ItemPropertyApplicability.UnconditionalDisplayedLocal,
            Evidence =
            [
                new ItemPropertySemanticEvidence
                {
                    Method = ItemPropertySemanticEvidenceMethod.ReviewedOverride,
                    SourceId = "poenhance.item-property-semantics",
                    ReviewVersion = "aps-crit-defence-v1",
                    ReviewReference = "complete-item-property-contributor-and-locality-audit:2026-07-17#section-7",
                },
            ],
        };
        return new ActiveSemanticCase(
            Mod(modifierId, modifierName, generationType, statId),
            Translation($"translation.{modifierId}", statId, translationFormat, valueFormat),
            semantic,
            copiedLine,
            targets,
            operation);
    }

    private static IReadOnlyList<ActiveSemanticCase> ActiveSemanticCases { get; } =
    [
        Case("item.quality.added.local", "DelveArmourQuality", "of the Underground",
            ModifierGenerationType.Suffix, "local_item_quality_+", "+20% to Quality", "{0}% to Quality", "+#",
            ItemPropertyOperation.Added, ItemPropertyTarget.Quality),
        Case("item.armour.added.local", "LocalIncreasedPhysicalDamageReductionRating1", "Lacquered",
            ModifierGenerationType.Prefix, "local_base_physical_damage_reduction_rating", "+12 to Armour",
            "{0} to Armour", "+#", ItemPropertyOperation.Added, ItemPropertyTarget.Armour),
        Case("item.armour.increased-percent.local", "LocalIncreasedPhysicalDamageReductionRatingPercent1",
            "Reinforced", ModifierGenerationType.Prefix, "local_physical_damage_reduction_rating_+%",
            "26% increased Armour", "{0}% increased Armour", "#", ItemPropertyOperation.IncreasedPercent,
            ItemPropertyTarget.Armour),
        Case("item.evasion.added.local", "LocalIncreasedEvasionRating1", "Agile",
            ModifierGenerationType.Prefix, "local_base_evasion_rating", "+12 to Evasion Rating",
            "{0} to Evasion Rating", "+#", ItemPropertyOperation.Added, ItemPropertyTarget.Evasion),
        Case("item.evasion.increased-percent.local", "LocalIncreasedEvasionRatingPercent1", "Shade's",
            ModifierGenerationType.Prefix, "local_evasion_rating_+%", "26% increased Evasion Rating",
            "{0}% increased Evasion Rating", "#", ItemPropertyOperation.IncreasedPercent,
            ItemPropertyTarget.Evasion),
        Case("item.energy-shield.added.local", "LocalIncreasedEnergyShield1", "Shining",
            ModifierGenerationType.Prefix, "local_energy_shield", "+5 to maximum Energy Shield",
            "{0} to maximum Energy Shield", "+#", ItemPropertyOperation.Added,
            ItemPropertyTarget.EnergyShield),
        Case("item.energy-shield.increased-percent.local", "LocalIncreasedEnergyShieldPercent1", "Protective",
            ModifierGenerationType.Prefix, "local_energy_shield_+%", "28% increased Energy Shield",
            "{0}% increased Energy Shield", "#", ItemPropertyOperation.IncreasedPercent,
            ItemPropertyTarget.EnergyShield),
        Case("item.ward.added.local", "LocalIncreasedWard1", "Farrier's", ModifierGenerationType.Prefix,
            "local_ward", "+9 to Ward", "{0} to Ward", "+#", ItemPropertyOperation.Added,
            ItemPropertyTarget.Ward),
        Case("item.ward.increased-percent.local", "LocalIncreasedWardPercent1", "Chiseled",
            ModifierGenerationType.Prefix, "local_ward_+%", "28% increased Ward", "{0}% increased Ward", "#",
            ItemPropertyOperation.IncreasedPercent, ItemPropertyTarget.Ward),
        Case("item.block.added.local", "AdditionalBlockChance1", "of Intercepting",
            ModifierGenerationType.Suffix, "local_additional_block_chance_%", "+3% Chance to Block",
            "{0}% Chance to Block", "+#", ItemPropertyOperation.Added, ItemPropertyTarget.Block),
        Case("item.block.increased-percent.local", "LocalIncreasedBlockPercentage1", "Steadfast",
            ModifierGenerationType.Prefix, "local_block_chance_+%", "45% increased Chance to Block",
            "{0}% increased Chance to Block", "#", ItemPropertyOperation.IncreasedPercent,
            ItemPropertyTarget.Block),
        Case("item.armour-evasion.increased-percent.local", "LocalIncreasedArmourAndEvasion1", "Scrapper's",
            ModifierGenerationType.Prefix, "local_armour_and_evasion_+%", "26% increased Armour and Evasion",
            "{0}% increased Armour and Evasion", "#", ItemPropertyOperation.IncreasedPercent,
            ItemPropertyTarget.Armour, ItemPropertyTarget.Evasion),
        Case("item.armour-energy-shield.increased-percent.local", "LocalIncreasedArmourAndEnergyShield1",
            "Infixed", ModifierGenerationType.Prefix, "local_armour_and_energy_shield_+%",
            "26% increased Armour and Energy Shield", "{0}% increased Armour and Energy Shield", "#",
            ItemPropertyOperation.IncreasedPercent, ItemPropertyTarget.Armour, ItemPropertyTarget.EnergyShield),
        Case("item.evasion-energy-shield.increased-percent.local", "LocalIncreasedEvasionAndEnergyShield1",
            "Shadowy", ModifierGenerationType.Prefix, "local_evasion_and_energy_shield_+%",
            "26% increased Evasion and Energy Shield", "{0}% increased Evasion and Energy Shield", "#",
            ItemPropertyOperation.IncreasedPercent, ItemPropertyTarget.Evasion, ItemPropertyTarget.EnergyShield),
        Case("item.armour-evasion-energy-shield.increased-percent.local", "DelveArmourDefences1",
            "of the Underground", ModifierGenerationType.Suffix,
            "local_armour_and_evasion_and_energy_shield_+%", "50% increased Armour, Evasion and Energy Shield",
            "{0}% increased Armour, Evasion and Energy Shield", "#", ItemPropertyOperation.IncreasedPercent,
            ItemPropertyTarget.Armour, ItemPropertyTarget.Evasion, ItemPropertyTarget.EnergyShield),
        Case("item.evasion-energy-shield.added.local", "LocalFlatIncreasedEvasionAndEnergyShieldUnique__1",
            null, ModifierGenerationType.Implicit, "local_evasion_rating_and_energy_shield",
            "+100 to Evasion Rating and Energy Shield", "{0} to Evasion Rating and Energy Shield", "+#",
            ItemPropertyOperation.Added, ItemPropertyTarget.Evasion, ItemPropertyTarget.EnergyShield),
    ];

    private const string SlinkBoots = """
        Item Class: Boots
        Rarity: Rare
        Cataclysm League
        Slink Boots
        --------
        Evasion Rating: 326 (augmented)
        --------
        Item Level: 84
        --------
        { Suffix Modifier "of Steel Skin" (Tier: 3) }
        22(20-22)% increased Stun and Block Recovery
        """;

    private sealed record ActiveSemanticCase(
        ModifierDefinition Modifier,
        StatTranslationDefinition Translation,
        ItemPropertySemanticDescriptor Semantic,
        string CopiedLine,
        IReadOnlyList<ItemPropertyTarget> Targets,
        ItemPropertyOperation Operation);
}
