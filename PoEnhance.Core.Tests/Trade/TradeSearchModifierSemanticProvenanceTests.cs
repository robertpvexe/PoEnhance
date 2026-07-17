using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Tests.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeSearchModifierSemanticProvenanceTests
{
    private const string PhysicalPercent = "local_physical_damage_+%";
    private const string PhysicalMinimum = "local_minimum_added_physical_damage";
    private const string PhysicalMaximum = "local_maximum_added_physical_damage";
    private const string FireMinimum = "local_minimum_added_fire_damage";
    private const string FireMaximum = "local_maximum_added_fire_damage";
    private const string ColdMinimum = "local_minimum_added_cold_damage";
    private const string ColdMaximum = "local_maximum_added_cold_damage";
    private const string LightningMinimum = "local_minimum_added_lightning_damage";
    private const string LightningMaximum = "local_maximum_added_lightning_damage";
    private const string ChaosMinimum = "local_minimum_added_chaos_damage";
    private const string ChaosMaximum = "local_maximum_added_chaos_damage";
    private const string LocalAttackSpeed = "local_attack_speed_+%";
    private const string LocalCriticalStrikeChancePercent = "local_critical_strike_chance_+%";
    private const string LocalCriticalStrikeChanceAdded = "local_critical_strike_chance";
    private const string ConditionalLocalCriticalStrikeChance =
        "local_critical_strike_chance_+%_if_item_corrupted";
    private const string PerQualityLocalCriticalStrikeChance =
        "local_critical_strike_chance_+%_per_4%_quality";
    private const string QualityDoesNotIncreasePhysicalDamage =
        "local_quality_does_not_increase_physical_damage";
    private const string LocalExplicitModifierEffect = "local_explicit_mod_effect_+%";

    private readonly ItemTextParser parser = new();
    private readonly ParsedItemModifierCandidateResolver resolver = new();
    private readonly TradeSearchDraftMapper mapper = new();

    [Fact]
    public void CreateDraft_HorrorManglerAggregateRetainsReviewedSemanticProofAndLeafTierRank()
    {
        var catalog = ReviewedWeaponCatalog();

        var draft = CreateDraft(HorrorMangler, catalog);

        var aggregate = Assert.Single(draft.ModifierFilters, component =>
            component.OriginalText == "146% increased Physical Damage");
        Assert.Equal([146m], aggregate.ObservedNumericValues);
        Assert.Equal([146m], aggregate.CanonicalNumericValues);
        Assert.Equal("weapon.physical-damage.increased-percent.local", aggregate.ReviewedItemPropertySemantic?.Id);
        Assert.Equal(ModifierStatMappingProofStatus.Unknown, aggregate.StatMappingProof);
        Assert.Null(aggregate.Tier);
        Assert.Null(aggregate.Rank);
        Assert.Equal([0, 1], aggregate.Sources.Select(source => source.SourceModifierIndex));
        Assert.Equal(["Explicit", "Crafted"], aggregate.Sources.Select(source => source.ProviderDomain));
        Assert.Equal(new int?[] { 3, null }, aggregate.Sources.Select(source => source.Tier));
        Assert.Equal(new int?[] { null, 4 }, aggregate.Sources.Select(source => source.Rank));
        Assert.Equal(
            [ModifierStatMappingProofStatus.ProvenExact, ModifierStatMappingProofStatus.WholeVector],
            aggregate.Sources.Select(source => source.StatMappingProof));
        Assert.All(aggregate.Sources, source =>
            Assert.Equal(aggregate.ReviewedItemPropertySemantic, source.ReviewedItemPropertySemantic));
        Assert.Equal(
            aggregate.Sources.Select(source => (source.Tier, source.Rank)),
            aggregate.Contributors.Select(contributor =>
                (contributor.Source.Tier, contributor.Source.Rank)));
        Assert.DoesNotContain(draft.ModifierFilters, component =>
            component.OriginalText.StartsWith("30", StringComparison.Ordinal) ||
            component.OriginalText.StartsWith("116", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1, "weapon.fire-damage.added.local", 6)]
    [InlineData(3, "weapon.physical-damage.added.local", 1)]
    public void CreateDraft_TrackedSingleDamageWeaponFixturesRetainWholeVectorSemantic(
        int fixtureIndex,
        string expectedSemanticId,
        int expectedTier)
    {
        var draft = CreateDraft(CopiedItemCorpus.LoadItems()[fixtureIndex], ReviewedWeaponCatalog());

        var component = Assert.Single(draft.ModifierFilters, candidate =>
            candidate.ReviewedItemPropertySemantic?.Id == expectedSemanticId);
        Assert.Equal(ModifierStatMappingProofStatus.WholeVector, component.StatMappingProof);
        Assert.Equal(expectedTier, component.Tier);
        Assert.Equal(expectedTier, Assert.Single(component.Sources).Tier);
        Assert.True(component.IsSearchable);
    }

    [Fact]
    public void CreateDraft_GolemFletchRetainsDistinctSemanticsInSourceOrder()
    {
        var draft = CreateDraft(CopiedItemCorpus.LoadItems()[2], ReviewedWeaponCatalog());
        var elemental = draft.ModifierFilters
            .Where(component => component.ReviewedItemPropertySemantic is not null)
            .ToArray();

        Assert.Equal(
        [
            "weapon.cold-damage.added.local",
            "weapon.fire-damage.added.local",
            "weapon.lightning-damage.added.local",
        ], elemental.Select(component => component.ReviewedItemPropertySemantic!.Id));
        Assert.Equal([0, 1, 2], elemental.Select(component => component.SourceModifierIndex));
        Assert.Equal([6, 5, 6], elemental.Select(component => component.Tier));
        Assert.All(elemental, component =>
            Assert.Equal(ModifierStatMappingProofStatus.WholeVector, component.StatMappingProof));
    }

    [Fact]
    public void CreateDraft_LocalChaosRetainsReviewedWholeVectorSemantic()
    {
        var draft = CreateDraft(LocalChaos, ReviewedWeaponCatalog());

        var component = Assert.Single(draft.ModifierFilters);
        Assert.Equal("weapon.chaos-damage.added.local", component.ReviewedItemPropertySemantic?.Id);
        Assert.Equal(ModifierStatMappingProofStatus.WholeVector, component.StatMappingProof);
        Assert.Equal([ChaosMinimum, ChaosMaximum], component.ResolvedStatIds);
        Assert.Equal(ItemPropertyApplicability.UnconditionalDisplayedLocal,
            component.ReviewedItemPropertySemantic!.Applicability);
        var contribution = Assert.Single(component.ReviewedItemPropertySemantic.Contributions);
        Assert.Equal(ItemPropertyOperation.Added, contribution.Operation);
        Assert.Equal([ItemPropertyTarget.ChaosDamage], contribution.Targets);
        var evidence = Assert.Single(component.ReviewedItemPropertySemantic.Evidence);
        Assert.Equal(ItemPropertySemanticEvidenceMethod.ReviewedOverride, evidence.Method);
        Assert.Equal("aps-crit-defence-v1", evidence.ReviewVersion);
    }

    [Fact]
    public void CreateDraft_MorbidBiteAttachesReviewedAttackSpeedSemantic()
    {
        var draft = CreateDraft(CopiedItemCorpus.LoadItems()[3], ReviewedWeaponCatalog());

        var attackSpeed = Assert.Single(draft.ModifierFilters, component =>
            component.ResolvedModifierId == "LocalIncreasedAttackSpeed8");
        Assert.Equal("weapon.attack-speed.increased-percent.local", attackSpeed.ReviewedItemPropertySemantic?.Id);
        Assert.Equal([LocalAttackSpeed], attackSpeed.ResolvedStatIds);
        Assert.Equal(ModifierLocality.Local, attackSpeed.Locality);
        Assert.Equal(ModifierStatMappingProofStatus.ProvenExact, attackSpeed.StatMappingProof);
    }

    [Fact]
    public void CreateDraft_ActiveIncreasedCritRecordSyntheticHarnessAttachesDistinctPercentSemantic()
    {
        // Fixture gap: the repository still has no genuine copied local-weapon-Crit capture.
        // This harness uses the active package's LocalCriticalStrikeChance1 record and translation only.
        var draft = CreateDraft(IncreasedCritSyntheticHarness, ReviewedWeaponCatalog());

        var component = Assert.Single(draft.ModifierFilters);
        Assert.Equal("LocalCriticalStrikeChance1", component.ResolvedModifierId);
        Assert.Equal(
            "weapon.critical-strike-chance.increased-percent.local",
            component.ReviewedItemPropertySemantic?.Id);
        Assert.Equal(ItemPropertyOperation.IncreasedPercent,
            Assert.Single(component.ReviewedItemPropertySemantic!.Contributions).Operation);
        Assert.Empty(component.ValueBoundTranslationHandlers.Single());
        Assert.Equal(14m, component.RequestedMinimum);
    }

    [Fact]
    public void CreateDraft_ActiveFlatCritRecordSyntheticHarnessRetainsDivideByOneHundredHandler()
    {
        // Fixture gap: WeaponTreeLocalCritChance1 is an active GameData record, not a genuine copied fixture.
        var item = parser.Parse(FlatCritSyntheticHarness);
        var catalog = ReviewedWeaponCatalog();
        var activeRecord = Assert.Single(catalog.Modifiers, modifier =>
            modifier.Id == "WeaponTreeLocalCritChance1");
        var parsedModifier = Assert.Single(item.Modifiers);
        var resolution = new ModifierCandidateResolutionResult(
            0,
            parsedModifier,
            parsedModifier.Name,
            parsedModifier.Kind,
            ModifierGenerationType.Unknown,
            ModifierCandidateResolutionStatus.Exact,
            [activeRecord],
            [],
            Locality: ModifierLocality.Local);

        var result = mapper.CreateDraft(item, modifierResolutions: [resolution], gameDataCatalog: catalog);

        Assert.True(result.IsSuccess);
        var component = Assert.Single(Assert.IsType<TradeSearchDraft>(result.Draft).ModifierFilters);
        Assert.Equal("weapon.critical-strike-chance.added.local", component.ReviewedItemPropertySemantic?.Id);
        Assert.Equal(ItemPropertyOperation.Added,
            Assert.Single(component.ReviewedItemPropertySemantic!.Contributions).Operation);
        Assert.Equal(["divide_by_one_hundred"], Assert.Single(component.ValueBoundTranslationHandlers));
        Assert.Equal(0.4m, component.RequestedMinimum);
        Assert.NotEqual(
            "weapon.critical-strike-chance.increased-percent.local",
            component.ReviewedItemPropertySemantic.Id);
    }

    [Fact]
    public void CreateDraft_GlobalConditionalPerQualityAndIndirectApsCritFamiliesReceiveNoSemantic()
    {
        var draft = CreateDraft(UnsafeApsCritSyntheticHarness, ReviewedWeaponCatalog());

        var excluded = draft.ModifierFilters.ToArray();
        Assert.Equal(6, excluded.Length);
        Assert.All(excluded, component => Assert.Null(component.ReviewedItemPropertySemantic));
        Assert.All(excluded.Take(3), component => Assert.Equal(ModifierLocality.Global, component.Locality));
        Assert.All(excluded.Skip(3), component => Assert.Equal(ModifierLocality.Local, component.Locality));
        Assert.Empty(draft.ItemPropertyContributionGroups);
    }

    [Fact]
    public void CreateDraft_WrathCryAndEagleSpiralGlobalAttackOrSpellEffectsHaveNoReviewedLocalSemantic()
    {
        var catalog = ReviewedWeaponCatalog();
        var wrath = CreateDraft(CopiedItemCorpus.LoadItems()[4], catalog);
        var eagle = CreateDraft(CopiedItemCorpus.LoadItems()[7], catalog);

        var spellLightning = Assert.Single(wrath.ModifierFilters, component =>
            component.OriginalText.Contains("Lightning Damage to Spells", StringComparison.Ordinal));
        var globalLightning = Assert.Single(wrath.ModifierFilters, component =>
            component.OriginalText.Contains("increased Lightning Damage", StringComparison.Ordinal));
        var ringPhysical = Assert.Single(eagle.ModifierFilters, component =>
            component.OriginalText.Contains("Physical Damage to Attacks", StringComparison.Ordinal));

        Assert.All([spellLightning, globalLightning, ringPhysical], component =>
        {
            Assert.Null(component.ReviewedItemPropertySemantic);
            Assert.Equal(ModifierLocality.Global, component.Locality);
            Assert.True(component.IsSearchable);
        });
    }

    [Fact]
    public void CreateDraft_ConditionalExactLocalVectorRemainsOrdinaryFilterWithoutPositiveSemantic()
    {
        const string minimum = "local_minimum_added_fire_damage_if_condition";
        const string maximum = "local_maximum_added_fire_damage_if_condition";
        var modifier = Modifier("conditional-fire", "Conditional", ModifierGenerationType.Prefix, minimum, maximum);
        var catalog = Catalog(
            [modifier],
            [RangeTranslation("conditional-fire", minimum, maximum, "Fire Damage while condition is met")],
            [Semantic("conditional-fire", [minimum, maximum], ItemPropertyTarget.FireDamage, applicability: ItemPropertyApplicability.Conditional)],
            [minimum, maximum]);

        var component = Assert.Single(CreateDraft(ConditionalFire, catalog).ModifierFilters);

        Assert.Equal(ModifierStatMappingProofStatus.WholeVector, component.StatMappingProof);
        Assert.Equal(ModifierLocality.Local, component.Locality);
        Assert.Null(component.ReviewedItemPropertySemantic);
        Assert.True(component.IsSearchable);
    }

    [Fact]
    public void CreateDraft_PositionalFallbackIsMarkedAndCannotReceiveReviewedSemantic()
    {
        const string first = "local_first_test_value";
        const string second = "local_second_test_value";
        var catalog = Catalog(
            [Modifier("fallback", "Fallback", ModifierGenerationType.Prefix, first, second)],
            [],
            [Semantic("fallback.first", [first], ItemPropertyTarget.FireDamage)],
            [first, second]);

        var components = CreateDraft(PositionalFallback, catalog).ModifierFilters;

        Assert.Equal(2, components.Count);
        Assert.All(components, component =>
        {
            Assert.Equal(ModifierStatMappingProofStatus.PositionalFallback, component.StatMappingProof);
            Assert.Null(component.ReviewedItemPropertySemantic);
            Assert.True(component.IsSearchable);
        });
    }

    [Fact]
    public void CreateDraft_AmbiguousContainingTranslationsRemainPositionalFallback()
    {
        const string first = "local_ambiguous_first_value";
        const string second = "local_ambiguous_second_value";
        const string firstControl = "ambiguous_first_control";
        const string secondControl = "ambiguous_second_control";
        var catalog = Catalog(
            [Modifier("ambiguous-containing", "Fallback", ModifierGenerationType.Prefix, first, second)],
            [
                ContainingTranslation("ambiguous-one", first, firstControl, "{0} First Value"),
                ContainingTranslation("ambiguous-two", first, secondControl, "{0} First Value", ["double"]),
                Translation("second", [second], "{0} Second Value"),
            ],
            [],
            [first, second]);

        var components = CreateDraft(PositionalFallback, catalog).ModifierFilters;

        Assert.Equal(2, components.Count);
        Assert.All(components, component =>
            Assert.Equal(ModifierStatMappingProofStatus.PositionalFallback, component.StatMappingProof));
    }

    [Fact]
    public void CreateDraft_UnresolvedAndAmbiguousCandidatesRemainUnknownWithoutPositiveSemantic()
    {
        const string statId = "local_ambiguous_test_value";
        var unresolvedCatalog = Catalog([], [], [], []);
        var ambiguousCatalog = Catalog(
        [
            Modifier("ambiguous.one", "Ambiguous", ModifierGenerationType.Prefix, statId),
            Modifier("ambiguous.two", "Ambiguous", ModifierGenerationType.Prefix, statId),
        ],
            [],
            [Semantic("ambiguous", [statId], ItemPropertyTarget.PhysicalDamage)],
            [statId]);

        var unresolved = Assert.Single(CreateDraft(Unresolved, unresolvedCatalog).ModifierFilters);
        var ambiguous = Assert.Single(CreateDraft(Ambiguous, ambiguousCatalog).ModifierFilters);

        Assert.All([unresolved, ambiguous], component =>
        {
            Assert.Equal(ModifierStatMappingProofStatus.Unknown, component.StatMappingProof);
            Assert.Null(component.ReviewedItemPropertySemantic);
            Assert.False(component.IsSearchable);
        });
    }

    [Fact]
    public void CreateDraft_ReversedSubsetAndExtraVectorsDoNotResolveReviewedSemantic()
    {
        const string minimum = "local_minimum_test_damage";
        const string maximum = "local_maximum_test_damage";
        const string extra = "local_extra_test_damage";
        var descriptor = Semantic("test.damage", [minimum, maximum], ItemPropertyTarget.FireDamage);
        var cases = new[]
        {
            new VectorCase(
                VectorItem("Reversed", "Adds 10 to 20 Test Damage"),
                Modifier("reversed", "Reversed", ModifierGenerationType.Prefix, maximum, minimum),
                Translation("reversed", [maximum, minimum], "Adds {1} to {0} Test Damage")),
            new VectorCase(
                VectorItem("Subset", "Adds 10 Test Damage"),
                Modifier("subset", "Subset", ModifierGenerationType.Prefix, minimum),
                Translation("subset", [minimum], "Adds {0} Test Damage")),
            new VectorCase(
                VectorItem("Extra", "Adds 10 to 20 Test Damage and 5 extra"),
                Modifier("extra", "Extra", ModifierGenerationType.Prefix, minimum, maximum, extra),
                Translation("extra", [minimum, maximum, extra], "Adds {0} to {1} Test Damage and {2} extra")),
        };

        foreach (var vectorCase in cases)
        {
            var catalog = Catalog(
                [vectorCase.Modifier],
                [vectorCase.Translation],
                [descriptor],
                [minimum, maximum, extra]);
            var component = Assert.Single(CreateDraft(vectorCase.ItemText, catalog).ModifierFilters);

            Assert.Null(component.ReviewedItemPropertySemantic);
            Assert.True(component.IsSearchable);
        }
    }

    [Fact]
    public void CreateDraft_ExactReviewedVectorWithoutDescriptorRemainsSearchableWithoutSemantic()
    {
        var modifier = Modifier("chaos", "Chaotic", ModifierGenerationType.Prefix, ChaosMinimum, ChaosMaximum);
        var catalog = Catalog(
            [modifier],
            [RangeTranslation("chaos", ChaosMinimum, ChaosMaximum, "Chaos Damage")],
            [],
            [ChaosMinimum, ChaosMaximum]);

        var component = Assert.Single(CreateDraft(LocalChaos, catalog).ModifierFilters);

        Assert.Equal(ModifierStatMappingProofStatus.WholeVector, component.StatMappingProof);
        Assert.Null(component.ReviewedItemPropertySemantic);
        Assert.True(component.IsSearchable);
    }

    private TradeSearchDraft CreateDraft(string itemText, GameDataCatalog catalog)
    {
        var item = parser.Parse(itemText);
        var resolutions = resolver.Resolve(item, catalog);
        var result = mapper.CreateDraft(
            item,
            modifierResolutions: resolutions,
            gameDataCatalog: catalog);

        Assert.True(result.IsSuccess);
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }

    internal static GameDataCatalog ReviewedWeaponCatalog()
    {
        var modifiers = new[]
        {
            Modifier("reavers", "Reaver's", ModifierGenerationType.Prefix, PhysicalPercent, "local_accuracy_rating"),
            Modifier("physical-aps-hybrid", "Cruel Celebration", ModifierGenerationType.Prefix,
                PhysicalPercent, LocalAttackSpeed),
            ModifierWithDomain("upgraded", "Upgraded", ModifierGenerationType.Prefix, "crafted", PhysicalPercent),
            Modifier("flaming", "Flaming", ModifierGenerationType.Prefix, FireMinimum, FireMaximum),
            Modifier("flaring", "Flaring", ModifierGenerationType.Prefix, PhysicalMinimum, PhysicalMaximum),
            Modifier("freezing", "Freezing", ModifierGenerationType.Prefix, ColdMinimum, ColdMaximum),
            Modifier("glaciated", "Glaciated", ModifierGenerationType.Prefix, ColdMinimum, ColdMaximum),
            Modifier("scorching", "Scorching", ModifierGenerationType.Prefix, FireMinimum, FireMaximum),
            Modifier("sparking", "Sparking", ModifierGenerationType.Prefix, LightningMinimum, LightningMaximum),
            Modifier("chaos", "Chaotic", ModifierGenerationType.Prefix, ChaosMinimum, ChaosMaximum),
            Modifier("LocalIncreasedAttackSpeed8", "of Celebration", ModifierGenerationType.Suffix, LocalAttackSpeed),
            ModifierWithDomain(
                "EinharMasterLocalIncreasedAttackSpeed3",
                "of Craft",
                ModifierGenerationType.Suffix,
                "crafted",
                LocalAttackSpeed),
            Modifier(
                "LocalCriticalStrikeChance1",
                "of Needling",
                ModifierGenerationType.Suffix,
                LocalCriticalStrikeChancePercent),
            Modifier(
                "WeaponTreeLocalCritChance1",
                string.Empty,
                ModifierGenerationType.Unknown,
                LocalCriticalStrikeChanceAdded),
            Modifier("global-attack-speed", "of Global Speed Harness", ModifierGenerationType.Suffix,
                "attack_speed_+%"),
            Modifier("global-critical-strike", "of Global Crit Harness", ModifierGenerationType.Suffix,
                "critical_strike_chance_+%"),
            Modifier("spell-critical-strike", "of Spell Crit Harness", ModifierGenerationType.Suffix,
                "spell_critical_strike_chance_+%"),
            Modifier("conditional-local-critical-strike", "of Conditional Crit Harness",
                ModifierGenerationType.Suffix, ConditionalLocalCriticalStrikeChance),
            Modifier("per-quality-local-critical-strike", "of Per Quality Crit Harness",
                ModifierGenerationType.Suffix, QualityDoesNotIncreasePhysicalDamage,
                PerQualityLocalCriticalStrikeChance),
            Modifier("indirect-local-explicit-effect", "of Indirect Effect Harness",
                ModifierGenerationType.Suffix, LocalExplicitModifierEffect),
            Modifier("spell-lightning", "Shocking", ModifierGenerationType.Prefix,
                "minimum_added_lightning_damage_to_spells", "maximum_added_lightning_damage_to_spells"),
            Modifier("global-lightning", "of Electricity", ModifierGenerationType.Suffix, "lightning_damage_+%"),
            Modifier("ring-physical", "Gleaming", ModifierGenerationType.Prefix,
                "minimum_added_physical_damage_to_attacks", "maximum_added_physical_damage_to_attacks"),
        };
        var translations = new[]
        {
            ContainingTranslation(
                "physical-percent",
                PhysicalPercent,
                "local_weapon_no_physical_damage",
                "{0}% increased Physical Damage"),
            Translation("accuracy", ["local_accuracy_rating"], "+{0} to Accuracy Rating"),
            RangeTranslation("physical", PhysicalMinimum, PhysicalMaximum, "Physical Damage"),
            RangeTranslation("fire", FireMinimum, FireMaximum, "Fire Damage"),
            RangeTranslation("cold", ColdMinimum, ColdMaximum, "Cold Damage"),
            RangeTranslation("lightning", LightningMinimum, LightningMaximum, "Lightning Damage"),
            RangeTranslation("chaos", ChaosMinimum, ChaosMaximum, "Chaos Damage"),
            Translation("attack-speed", [LocalAttackSpeed], "{0}% increased Attack Speed"),
            Translation("critical-strike-chance-percent", [LocalCriticalStrikeChancePercent],
                "{0}% increased Critical Strike Chance"),
            Translation(
                "critical-strike-chance-added",
                [LocalCriticalStrikeChanceAdded],
                "{0}% to Critical Strike Chance",
                ["divide_by_one_hundred"],
                "+#"),
            Translation("global-attack-speed", ["attack_speed_+%"], "{0}% increased Global Attack Speed"),
            Translation("global-critical-strike", ["critical_strike_chance_+%"],
                "{0}% increased Global Critical Strike Chance"),
            Translation("spell-critical-strike", ["spell_critical_strike_chance_+%"],
                "{0}% increased Spell Critical Strike Chance"),
            Translation("conditional-local-critical-strike", [ConditionalLocalCriticalStrikeChance],
                "{0}% increased Conditional Critical Strike Chance"),
            ContainingTranslation(
                "per-quality-local-critical-strike",
                PerQualityLocalCriticalStrikeChance,
                QualityDoesNotIncreasePhysicalDamage,
                "{0}% increased Per Quality Critical Strike Chance"),
            Translation("indirect-local-explicit-effect", [LocalExplicitModifierEffect],
                "{0}% increased Indirect Explicit Modifier Effect"),
            RangeTranslation("spell-lightning", "minimum_added_lightning_damage_to_spells",
                "maximum_added_lightning_damage_to_spells", "Lightning Damage to Spells"),
            Translation("global-lightning", ["lightning_damage_+%"], "{0}% increased Lightning Damage"),
            RangeTranslation("ring-physical", "minimum_added_physical_damage_to_attacks",
                "maximum_added_physical_damage_to_attacks", "Physical Damage to Attacks"),
        };
        var semantics = new[]
        {
            Semantic("weapon.physical-damage.increased-percent.local", [PhysicalPercent],
                ItemPropertyTarget.PhysicalDamage, ItemPropertyOperation.IncreasedPercent),
            Semantic("weapon.physical-damage.added.local", [PhysicalMinimum, PhysicalMaximum],
                ItemPropertyTarget.PhysicalDamage),
            Semantic("weapon.fire-damage.added.local", [FireMinimum, FireMaximum], ItemPropertyTarget.FireDamage),
            Semantic("weapon.cold-damage.added.local", [ColdMinimum, ColdMaximum], ItemPropertyTarget.ColdDamage),
            Semantic("weapon.lightning-damage.added.local", [LightningMinimum, LightningMaximum],
                ItemPropertyTarget.LightningDamage),
            Semantic("weapon.chaos-damage.added.local", [ChaosMinimum, ChaosMaximum], ItemPropertyTarget.ChaosDamage),
            Semantic("weapon.attack-speed.increased-percent.local", [LocalAttackSpeed],
                ItemPropertyTarget.AttacksPerSecond, ItemPropertyOperation.IncreasedPercent),
            Semantic("weapon.critical-strike-chance.increased-percent.local", [LocalCriticalStrikeChancePercent],
                ItemPropertyTarget.CriticalStrikeChance, ItemPropertyOperation.IncreasedPercent),
            Semantic("weapon.critical-strike-chance.added.local", [LocalCriticalStrikeChanceAdded],
                ItemPropertyTarget.CriticalStrikeChance),
        };
        var localStats = semantics
            .SelectMany(semantic => semantic.OrderedStatIds)
            .Append("local_accuracy_rating")
            .Append(ConditionalLocalCriticalStrikeChance)
            .Append(PerQualityLocalCriticalStrikeChance)
            .Append(QualityDoesNotIncreasePhysicalDamage)
            .Append(LocalExplicitModifierEffect)
            .ToArray();
        return Catalog(modifiers, translations, semantics, localStats);
    }

    private static GameDataCatalog Catalog(
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations,
        IReadOnlyList<ItemPropertySemanticDescriptor> semantics,
        IReadOnlyCollection<string> localStatIds)
    {
        var local = localStatIds.ToHashSet(StringComparer.Ordinal);
        var statIds = modifiers
            .SelectMany(modifier => modifier.Stats.Select(stat => stat.StatId))
            .Concat(translations.SelectMany(translation => translation.StatIds))
            .Concat(semantics.SelectMany(semantic => semantic.OrderedStatIds))
            .Where(statId => !string.IsNullOrWhiteSpace(statId))
            .Select(statId => statId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 1,
                DataVersion = "reviewed-semantics-test",
                CreatedAtUtc = DateTimeOffset.UnixEpoch,
                League = "test",
                Patch = "test",
                Sources =
                [
                    new GameDataPackageSource
                    {
                        SourceId = "test",
                        RetrievedAtUtc = DateTimeOffset.UnixEpoch,
                        SourceVersion = "test",
                        SourceUri = "https://example.test",
                    },
                ],
            },
            ItemBases =
            [
                WeaponBase(
                    "Metadata/Items/Weapons/OneHandWeapons/OneHandAxes/OneHandAxe18",
                    "Reaver Axe",
                    "One Hand Axe",
                    38,
                    114,
                    833,
                    5.00m),
                WeaponBase(
                    "Metadata/Items/Weapons/TwoHandWeapons/Bows/Bow18",
                    "Ranger Bow",
                    "Bow",
                    56,
                    117,
                    769,
                    6.00m),
                WeaponBase(
                    "Metadata/Items/Weapons/OneHandWeapons/Wands/WandK2",
                    "Blasting Wand",
                    "Wand",
                    21,
                    39,
                    625,
                    8.50m),
            ],
            Modifiers = modifiers,
            Stats = statIds.Select(statId => new StatDefinition
            {
                Id = statId,
                IsLocal = local.Contains(statId),
            }).ToArray(),
            StatTranslations = translations,
            ItemPropertySemantics = semantics,
        });
    }

    private static ItemBaseRecord WeaponBase(
        string id,
        string name,
        string itemClass,
        int physicalMinimum,
        int physicalMaximum,
        int attackTimeMilliseconds,
        decimal criticalStrikeChancePercent)
    {
        var source = new GameDataSourceReference
        {
            SourceId = "test",
            ExternalId = id,
            ExternalUri = $"https://repoe-fork.github.io/base_items.json#{id}",
        };
        return new ItemBaseRecord
        {
            Id = id,
            Name = name,
            ItemClass = itemClass,
            Sources = [source],
            WeaponProperties = new ItemBaseWeaponProperties
            {
                PhysicalDamageMinimum = physicalMinimum,
                PhysicalDamageMaximum = physicalMaximum,
                AttackTimeMilliseconds = attackTimeMilliseconds,
                CriticalStrikeChancePercent = criticalStrikeChancePercent,
                Sources = [source],
            },
        };
    }

    private static ModifierDefinition Modifier(
        string id,
        string name,
        ModifierGenerationType generationType,
        params string[] statIds)
    {
        return ModifierWithDomain(id, name, generationType, domain: null, statIds);
    }

    private static ModifierDefinition ModifierWithDomain(
        string id,
        string name,
        ModifierGenerationType generationType,
        string? domain,
        params string[] statIds)
    {
        return new ModifierDefinition
        {
            Id = id,
            GroupId = $"group.{id}",
            Name = name,
            GenerationType = generationType,
            Domain = domain,
            Stats = statIds.Select((statId, index) => new ModifierStat
            {
                Index = index,
                StatId = statId,
            }).ToArray(),
        };
    }

    private static StatTranslationDefinition RangeTranslation(
        string id,
        string minimumStatId,
        string maximumStatId,
        string suffix)
    {
        return Translation(id, [minimumStatId, maximumStatId], $"Adds {{0}} to {{1}} {suffix}");
    }

    private static StatTranslationDefinition Translation(
        string id,
        IReadOnlyList<string> statIds,
        string format,
        IReadOnlyList<string>? handlers = null,
        string valueFormat = "#")
    {
        return new StatTranslationDefinition
        {
            Id = id,
            StatIds = statIds,
            Variants =
            [
                new StatTranslationVariant
                {
                    Conditions = statIds.Select((_, index) => new StatTranslationCondition { Index = index }).ToArray(),
                    ValueFormats = statIds.Select(_ => valueFormat).ToArray(),
                    IndexHandlers = statIds.Select((_, index) =>
                        new StatTranslationIndexHandler
                        {
                            Index = index,
                            Handlers = handlers ?? [],
                        }).ToArray(),
                    FormatLines = [format],
                },
            ],
        };
    }

    private static StatTranslationDefinition ContainingTranslation(
        string id,
        string statId,
        string controlStatId,
        string format,
        IReadOnlyList<string>? handlers = null)
    {
        return new StatTranslationDefinition
        {
            Id = id,
            StatIds = [statId, controlStatId],
            Variants =
            [
                new StatTranslationVariant
                {
                    Conditions =
                    [
                        new StatTranslationCondition { Index = 0 },
                        new StatTranslationCondition { Index = 1 },
                    ],
                    ValueFormats = ["#", "ignore"],
                    IndexHandlers =
                    [
                        new StatTranslationIndexHandler { Index = 0, Handlers = handlers ?? [] },
                        new StatTranslationIndexHandler { Index = 1, Handlers = [] },
                    ],
                    FormatLines = [format],
                },
            ],
        };
    }

    private static ItemPropertySemanticDescriptor Semantic(
        string id,
        IReadOnlyList<string> statIds,
        ItemPropertyTarget target,
        ItemPropertyOperation operation = ItemPropertyOperation.Added,
        ItemPropertyApplicability applicability = ItemPropertyApplicability.UnconditionalDisplayedLocal)
    {
        return new ItemPropertySemanticDescriptor
        {
            Id = id,
            OrderedStatIds = statIds,
            Contributions =
            [
                new ItemPropertyContribution
                {
                    Targets = [target],
                    Operation = operation,
                },
            ],
            Applicability = applicability,
            Evidence =
            [
                new ItemPropertySemanticEvidence
                {
                    Method = ItemPropertySemanticEvidenceMethod.ReviewedOverride,
                    SourceId = "poenhance.item-property-semantics",
                    ReviewVersion = "aps-crit-defence-v1",
                    ReviewReference = "tracked-test-fixture",
                },
            ],
        };
    }

    private static string VectorItem(string modifierName, string line) => $$"""
        Item Class: One Hand Axes
        Rarity: Rare
        Vector Test
        Reaver Axe
        --------
        Item Level: 85
        --------
        { Prefix Modifier "{{modifierName}}" (Tier: 1) - Damage }
        {{line}}
        """;

    private const string HorrorMangler = """
        Item Class: One Hand Axes
        Rarity: Rare
        Horror Mangler
        Reaver Axe
        --------
        Item Level: 85
        --------
        { Prefix Modifier "Reaver's" (Tier: 3) - Damage, Physical, Attack }
        30(25-34)% increased Physical Damage
        +60(47-72) to Accuracy Rating
        { Master Crafted Prefix Modifier "Upgraded" (Rank: 4) - Damage, Physical, Attack }
        116(100-129)% increased Physical Damage
        """;

    private const string LocalChaos = """
        Item Class: One Hand Axes
        Rarity: Rare
        Chaos Test
        Reaver Axe
        --------
        Item Level: 85
        --------
        { Prefix Modifier "Chaotic" (Tier: 2) - Damage, Chaos, Attack }
        Adds 10(8-12) to 20(18-22) Chaos Damage
        """;

    private const string IncreasedCritSyntheticHarness = """
        Item Class: One Hand Axes
        Rarity: Rare
        Synthetic Crit Harness
        Reaver Axe
        --------
        One Handed Axe
        Physical Damage: 38-114
        Critical Strike Chance: 5.70% (augmented)
        Attacks per Second: 1.20
        --------
        Item Level: 85
        --------
        { Suffix Modifier "of Needling" (Tier: 1) - Critical }
        14(10-14)% increased Critical Strike Chance
        """;

    private const string FlatCritSyntheticHarness = """
        Item Class: One Hand Axes
        Rarity: Rare
        Synthetic Flat Crit Harness
        Reaver Axe
        --------
        One Handed Axe
        Physical Damage: 38-114
        Critical Strike Chance: 5.40% (augmented)
        Attacks per Second: 1.20
        --------
        Item Level: 85
        --------
        { Implicit Modifier }
        +0.4% to Critical Strike Chance
        """;

    private const string UnsafeApsCritSyntheticHarness = """
        Item Class: One Hand Axes
        Rarity: Rare
        Unsafe APS Crit Synthetic Harness
        Reaver Axe
        --------
        One Handed Axe
        Physical Damage: 38-114
        Critical Strike Chance: 5.00%
        Attacks per Second: 1.20
        --------
        Item Level: 85
        --------
        { Suffix Modifier "of Global Speed Harness" }
        10% increased Global Attack Speed
        { Suffix Modifier "of Global Crit Harness" }
        20% increased Global Critical Strike Chance
        { Suffix Modifier "of Spell Crit Harness" }
        30% increased Spell Critical Strike Chance
        { Suffix Modifier "of Conditional Crit Harness" }
        40% increased Conditional Critical Strike Chance
        { Suffix Modifier "of Per Quality Crit Harness" }
        5% increased Per Quality Critical Strike Chance
        { Suffix Modifier "of Indirect Effect Harness" }
        15% increased Indirect Explicit Modifier Effect
        """;

    private const string ConditionalFire = """
        Item Class: One Hand Axes
        Rarity: Rare
        Conditional Test
        Reaver Axe
        --------
        Item Level: 85
        --------
        { Prefix Modifier "Conditional" (Tier: 1) - Damage, Fire, Attack }
        Adds 10(8-12) to 20(18-22) Fire Damage while condition is met
        """;

    private const string PositionalFallback = """
        Item Class: One Hand Axes
        Rarity: Rare
        Fallback Test
        Reaver Axe
        --------
        Item Level: 85
        --------
        { Prefix Modifier "Fallback" (Tier: 1) - Damage }
        10 First Value
        20 Second Value
        """;

    private const string Unresolved = """
        Item Class: One Hand Axes
        Rarity: Rare
        Unknown Test
        Reaver Axe
        --------
        Item Level: 85
        --------
        { Prefix Modifier "Missing" (Tier: 1) - Damage }
        10 Test Value
        """;

    private const string Ambiguous = """
        Item Class: One Hand Axes
        Rarity: Rare
        Ambiguous Test
        Reaver Axe
        --------
        Item Level: 85
        --------
        { Prefix Modifier "Ambiguous" (Tier: 1) - Damage }
        10 Test Value
        """;

    private sealed record VectorCase(
        string ItemText,
        ModifierDefinition Modifier,
        StatTranslationDefinition Translation);
}
