using System.Collections.Immutable;
using System.Globalization;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Tests.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeSearchItemPropertyContributionGroupTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemModifierCandidateResolver resolver = new();
    private readonly TradeSearchDraftMapper mapper = new();

    [Fact]
    public void CreateDraft_GroupsAreImmutableSeparateMetadataWithValidCanonicalIndexes()
    {
        var draft = CreateDraft(CopiedItemCorpus.LoadItems()[2]);

        var group = Assert.Single(draft.ItemPropertyContributionGroups);
        Assert.Equal(TradeSearchItemPropertyKind.ElementalDps, group.ParentKind);
        Assert.IsType<ImmutableArray<TradeSearchItemPropertyContribution>>(
            group.Contributions);
        Assert.All(group.Contributions, contribution =>
        {
            Assert.InRange(contribution.ModifierFilterIndex, 0, draft.ModifierFilters.Count - 1);
            var canonicalComponent = draft.ModifierFilters[contribution.ModifierFilterIndex];
            Assert.Equal(
                canonicalComponent.ReviewedItemPropertySemantic?.Id,
                contribution.ReviewedSemanticDescriptorId);
        });
        Assert.DoesNotContain(
            typeof(TradeSearchItemPropertyContribution).GetProperties(),
            property => typeof(ResolvedSearchComponent).IsAssignableFrom(property.PropertyType));
        Assert.DoesNotContain(
            typeof(TradeSearchItemPropertyContributionGroup).GetProperties(),
            property => typeof(ResolvedSearchComponent).IsAssignableFrom(property.PropertyType));
        Assert.False(typeof(TradeSearchItemProperty).IsAssignableFrom(
            typeof(TradeSearchItemPropertyContributionGroup)));
        Assert.False(typeof(ResolvedSearchComponent).IsAssignableFrom(
            typeof(TradeSearchItemPropertyContributionGroup)));
        Assert.Equal(5, draft.ItemProperties.Length);
        Assert.Equal(4, draft.ModifierFilters.Count);
    }

    [Fact]
    public void CreateDraft_GolemFletchCreatesOneOrderedElementalGroup()
    {
        var draft = CreateDraft(CopiedItemCorpus.LoadItems()[2]);

        var group = Assert.Single(draft.ItemPropertyContributionGroups);
        Assert.Equal(TradeSearchItemPropertyKind.ElementalDps, group.ParentKind);
        Assert.Equal([0, 1, 2], group.Contributions.Select(contribution => contribution.ModifierFilterIndex));
        Assert.Equal(
        [
            ItemPropertyTarget.ColdDamage,
            ItemPropertyTarget.FireDamage,
            ItemPropertyTarget.LightningDamage,
        ], group.Contributions.Select(contribution => contribution.Target));
        Assert.All(group.Contributions, contribution =>
            Assert.Equal(ItemPropertyOperation.Added, contribution.Operation));
        Assert.Equal(
        [
            "Adds 46(41-55) to 81(81-95) Cold Damage",
            "Adds 70(63-85) to 139(128-148) Fire Damage",
            "Adds 9(8-10) to 155(148-173) Lightning Damage",
        ], group.Contributions.Select(contribution =>
            draft.ModifierFilters[contribution.ModifierFilterIndex].OriginalText));
    }

    [Theory]
    [InlineData(1, TradeSearchItemPropertyKind.ElementalDps, ItemPropertyTarget.FireDamage, "Fire Damage")]
    public void CreateDraft_SingleDamageCorpusWeaponsLinkTheirCanonicalComponent(
        int fixtureIndex,
        TradeSearchItemPropertyKind expectedParent,
        ItemPropertyTarget expectedTarget,
        string expectedText)
    {
        var draft = CreateDraft(CopiedItemCorpus.LoadItems()[fixtureIndex]);

        var group = Assert.Single(draft.ItemPropertyContributionGroups);
        Assert.Equal(expectedParent, group.ParentKind);
        var contribution = Assert.Single(group.Contributions);
        Assert.Equal(expectedTarget, contribution.Target);
        Assert.Contains(
            expectedText,
            draft.ModifierFilters[contribution.ModifierFilterIndex].OriginalText,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CreateDraft_MorbidBiteLinksLocalAttackSpeedOnlyToAttacksPerSecond()
    {
        var draft = CreateDraft(CopiedItemCorpus.LoadItems()[3]);
        Assert.True(
            draft.ItemProperties.Any(property => property.Kind == TradeSearchItemPropertyKind.PhysicalDps),
            string.Join(" | ", draft.ItemPropertyDiagnostics.Select(diagnostic => diagnostic.Message)));

        Assert.Equal(
            [TradeSearchItemPropertyKind.PhysicalDps, TradeSearchItemPropertyKind.AttacksPerSecond],
            draft.ItemPropertyContributionGroups.Select(group => group.ParentKind));
        var attackSpeedGroup = Assert.Single(draft.ItemPropertyContributionGroups, group =>
            group.ParentKind == TradeSearchItemPropertyKind.AttacksPerSecond);
        var contribution = Assert.Single(attackSpeedGroup.Contributions);
        Assert.Equal(ItemPropertyTarget.AttacksPerSecond, contribution.Target);
        Assert.Equal(ItemPropertyOperation.IncreasedPercent, contribution.Operation);
        Assert.Equal(
            "weapon.attack-speed.increased-percent.local",
            contribution.ReviewedSemanticDescriptorId);
        Assert.Contains(
            "increased Attack Speed",
            draft.ModifierFilters[contribution.ModifierFilterIndex].OriginalText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            draft.ItemPropertyContributionGroups
                .Where(group => group.ParentKind != TradeSearchItemPropertyKind.AttacksPerSecond)
                .SelectMany(group => group.Contributions),
            candidate => candidate.ModifierFilterIndex == contribution.ModifierFilterIndex);
    }

    [Fact]
    public void CreateDraft_ArmageddonThirstLinksCraftedAttackSpeedToCanonicalAttacksPerSecondFilter()
    {
        var draft = CreateDraft(ArmageddonThirst);

        var group = Assert.Single(draft.ItemPropertyContributionGroups);
        Assert.Equal(TradeSearchItemPropertyKind.AttacksPerSecond, group.ParentKind);
        var contribution = Assert.Single(group.Contributions);
        var canonical = draft.ModifierFilters[contribution.ModifierFilterIndex];
        Assert.Equal("20(16-20)% increased Attack Speed", canonical.OriginalText);
        Assert.True(canonical.IsCrafted);
        Assert.Equal("EinharMasterLocalIncreasedAttackSpeed3", canonical.ResolvedModifierId);
        Assert.Equal("weapon.attack-speed.increased-percent.local", canonical.ReviewedItemPropertySemantic?.Id);
        Assert.Equal(ItemPropertyTarget.AttacksPerSecond, contribution.Target);
    }

    [Fact]
    public void CreateDraft_HorrorManglerReusesAggregateAndKeepsAddedPhysicalBesideIt()
    {
        var draft = CreateDraft(HorrorManglerWithAddedPhysical);
        Assert.True(
            draft.ItemProperties.Any(property => property.Kind == TradeSearchItemPropertyKind.PhysicalDps),
            string.Join(" | ", draft.ItemPropertyDiagnostics.Select(diagnostic => diagnostic.Message)));
        var physicalDps = Assert.Single(draft.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.PhysicalDps);
        Assert.Equal(382.2m, physicalDps.ObservedValue);
        Assert.Equal("Q20", physicalDps.CalculationBasisLabel);

        var group = Assert.Single(draft.ItemPropertyContributionGroups);
        Assert.Equal(TradeSearchItemPropertyKind.PhysicalDps, group.ParentKind);
        Assert.Equal(2, group.Contributions.Length);
        Assert.Equal(
            group.Contributions.Select(contribution => contribution.ModifierFilterIndex).Order(),
            group.Contributions.Select(contribution => contribution.ModifierFilterIndex));
        Assert.Equal(
            [ItemPropertyOperation.IncreasedPercent, ItemPropertyOperation.Added],
            group.Contributions.Select(contribution => contribution.Operation));
        Assert.All(group.Contributions, contribution =>
            Assert.Equal(ItemPropertyTarget.PhysicalDamage, contribution.Target));

        var aggregate = draft.ModifierFilters[group.Contributions[0].ModifierFilterIndex];
        Assert.Equal("146% increased Physical Damage", aggregate.OriginalText);
        Assert.Equal(2, aggregate.Contributors.Count);
        Assert.Equal([30m, 116m], aggregate.Contributors.Select(contributor => contributor.RequestedMinimum));
        Assert.Equal(new int?[] { 3, null }, aggregate.Contributors.Select(contributor => contributor.Source.Tier));
        Assert.Equal(new int?[] { null, 4 }, aggregate.Contributors.Select(contributor => contributor.Source.Rank));

        var added = draft.ModifierFilters[group.Contributions[1].ModifierFilterIndex];
        Assert.Equal("Adds 23(22-29) to 46(45-52) Physical Damage", added.OriginalText);
        Assert.Single(added.Sources);
        Assert.DoesNotContain(draft.ModifierFilters, component =>
            component.OriginalText.StartsWith("30", StringComparison.Ordinal) ||
            component.OriginalText.StartsWith("116", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateDraft_PhysicalAndAttackSpeedHybridUsesPhysicalForQ20AndDisplayedAps()
    {
        var draft = CreateDraft(PhysicalAndAttackSpeedHybrid);

        var physicalDps = Assert.Single(draft.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.PhysicalDps);
        Assert.Equal(164.16m, physicalDps.ObservedValue);
        Assert.Equal("Q20", physicalDps.CalculationBasisLabel);
        var hybrid = draft.ModifierFilters.Where(component => component.SourceModifierIndex == 0).ToArray();
        Assert.Equal(2, hybrid.Length);
        Assert.Contains(hybrid, component => component.ReviewedItemPropertySemantic?.Contributions.Any(
            contribution => contribution.Targets.Contains(ItemPropertyTarget.PhysicalDamage)) == true);
        Assert.Contains(hybrid, component => component.ReviewedItemPropertySemantic?.Contributions.Any(
            contribution => contribution.Targets.Contains(ItemPropertyTarget.AttacksPerSecond)) == true);
    }

    [Fact]
    public void CreateDraft_WrathCryLinksOnlyReviewedLocalDisplayedWeaponDamage()
    {
        var draft = CreateDraft(CopiedItemCorpus.LoadItems()[4]);

        var group = Assert.Single(draft.ItemPropertyContributionGroups);
        Assert.Equal(TradeSearchItemPropertyKind.ElementalDps, group.ParentKind);
        var contribution = Assert.Single(group.Contributions);
        Assert.Equal(ItemPropertyTarget.ColdDamage, contribution.Target);
        Assert.Contains(
            "Cold Damage",
            draft.ModifierFilters[contribution.ModifierFilterIndex].OriginalText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(group.Contributions, candidate =>
        {
            var text = draft.ModifierFilters[candidate.ModifierFilterIndex].OriginalText;
            return text.Contains("Lightning Damage to Spells", StringComparison.Ordinal) ||
                text.Contains("increased Lightning Damage", StringComparison.Ordinal);
        });
        Assert.Contains(draft.ModifierFilters, component =>
            component.OriginalText.Contains("Lightning Damage to Spells", StringComparison.Ordinal));
        Assert.Contains(draft.ModifierFilters, component =>
            component.OriginalText.Contains("increased Lightning Damage", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateDraft_LocalChaosCreatesOneChaosDpsGroup()
    {
        var draft = CreateDraft(LocalChaosWeapon);

        var group = Assert.Single(draft.ItemPropertyContributionGroups);
        Assert.Equal(TradeSearchItemPropertyKind.ChaosDps, group.ParentKind);
        var contribution = Assert.Single(group.Contributions);
        Assert.Equal(ItemPropertyTarget.ChaosDamage, contribution.Target);
        Assert.Equal(ItemPropertyOperation.Added, contribution.Operation);
        Assert.Equal(
            "Adds 10(8-12) to 20(18-22) Chaos Damage",
            draft.ModifierFilters[contribution.ModifierFilterIndex].OriginalText);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void CreateDraft_NonWeaponCorpusFixturesCreateNoWeaponGroups(int fixtureIndex)
    {
        var draft = CreateDraft(CopiedItemCorpus.LoadItems()[fixtureIndex]);

        Assert.Empty(draft.ItemPropertyContributionGroups);
        if (fixtureIndex != 0)
        {
            Assert.NotEmpty(draft.ModifierFilters);
        }
    }

    [Fact]
    public void CreateDraft_SlinkBootsCreatesEvasionParentButNoRecoveryContributionGroup()
    {
        var draft = CreateDraft(SlinkBoots);

        Assert.Equal(
            TradeSearchItemPropertyKind.EvasionRating,
            Assert.Single(draft.ItemProperties).Kind);
        Assert.Empty(draft.ItemPropertyContributionGroups);
    }

    [Fact]
    public void CreateDraft_ReviewedSemanticWithoutItsParentKeepsOrdinaryFilterAndCreatesNoDiagnostic()
    {
        var draft = CreateDraft(PhysicalModifierWithoutPhysicalProperty);

        var component = Assert.Single(draft.ModifierFilters);
        Assert.Equal("weapon.physical-damage.added.local", component.ReviewedItemPropertySemantic?.Id);
        Assert.Empty(draft.ItemPropertyContributionGroups);
        Assert.Empty(draft.ModifierAggregationDiagnostics);
        Assert.Contains(draft.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.ElementalDps);
        Assert.DoesNotContain(draft.ItemProperties, property =>
            property.Kind == TradeSearchItemPropertyKind.PhysicalDps);
    }

    [Fact]
    public void Builder_SemanticAbsenceUnsupportedApplicabilityAndUnsupportedOperationCreateNoLinks()
    {
        var properties = ImmutableArray.Create(Property(TradeSearchItemPropertyKind.PhysicalDps));
        var modifiers = new[]
        {
            Component("absent", semantic: null),
            Component("conditional", Semantic(
                "conditional",
                ItemPropertyApplicability.Conditional,
                new ItemPropertyContribution
                {
                    Targets = [ItemPropertyTarget.PhysicalDamage],
                    Operation = ItemPropertyOperation.Added,
                })),
            Component("unsupported-operation", Semantic(
                "unsupported-operation",
                ItemPropertyApplicability.UnconditionalDisplayedLocal,
                new ItemPropertyContribution
                {
                    Targets = [ItemPropertyTarget.PhysicalDamage],
                    Operation = ItemPropertyOperation.Unknown,
                })),
        };

        var groups = TradeSearchItemPropertyContributionGroupBuilder.Create(properties, modifiers);

        Assert.Empty(groups);
    }

    [Fact]
    public void Builder_GroupOrderFollowsItemPropertiesAndContributionOrderFollowsModifierFilters()
    {
        var properties = ImmutableArray.Create(
            Property(TradeSearchItemPropertyKind.TotalDps),
            Property(TradeSearchItemPropertyKind.ChaosDps),
            Property(TradeSearchItemPropertyKind.ElementalDps),
            Property(TradeSearchItemPropertyKind.PhysicalDps),
            Property(TradeSearchItemPropertyKind.AttacksPerSecond),
            Property(TradeSearchItemPropertyKind.CriticalStrikeChance));
        var modifiers = new[]
        {
            Component("lightning", DamageSemantic("lightning", ItemPropertyTarget.LightningDamage)),
            Component("physical", DamageSemantic("physical", ItemPropertyTarget.PhysicalDamage)),
            Component("chaos", DamageSemantic("chaos", ItemPropertyTarget.ChaosDamage)),
            Component("cold", DamageSemantic("cold", ItemPropertyTarget.ColdDamage)),
            Component("fire", DamageSemantic("fire", ItemPropertyTarget.FireDamage)),
            Component("aps", DamageSemantic("aps", ItemPropertyTarget.AttacksPerSecond)),
            Component("crit", DamageSemantic("crit", ItemPropertyTarget.CriticalStrikeChance)),
        };

        var groups = TradeSearchItemPropertyContributionGroupBuilder.Create(properties, modifiers);

        Assert.Equal(
        [
            TradeSearchItemPropertyKind.ChaosDps,
            TradeSearchItemPropertyKind.ElementalDps,
            TradeSearchItemPropertyKind.PhysicalDps,
            TradeSearchItemPropertyKind.AttacksPerSecond,
            TradeSearchItemPropertyKind.CriticalStrikeChance,
        ], groups.Select(group => group.ParentKind));
        Assert.Equal([2], groups[0].Contributions.Select(contribution => contribution.ModifierFilterIndex));
        Assert.Equal([0, 3, 4], groups[1].Contributions.Select(contribution => contribution.ModifierFilterIndex));
        Assert.Equal([1], groups[2].Contributions.Select(contribution => contribution.ModifierFilterIndex));
        Assert.Equal([5], groups[3].Contributions.Select(contribution => contribution.ModifierFilterIndex));
        Assert.Equal([6], groups[4].Contributions.Select(contribution => contribution.ModifierFilterIndex));
        Assert.DoesNotContain(groups, group => group.ParentKind == TradeSearchItemPropertyKind.TotalDps);
    }

    [Theory]
    [InlineData(ItemPropertyTarget.Quality)]
    [InlineData(ItemPropertyTarget.Armour)]
    [InlineData(ItemPropertyTarget.Evasion)]
    [InlineData(ItemPropertyTarget.EnergyShield)]
    [InlineData(ItemPropertyTarget.Ward)]
    [InlineData(ItemPropertyTarget.Block)]
    public void Builder_UnimplementedParentTargetsRemainUngrouped(ItemPropertyTarget target)
    {
        var groups = TradeSearchItemPropertyContributionGroupBuilder.Create(
            [Property(TradeSearchItemPropertyKind.AttacksPerSecond)],
            [Component("unmapped", DamageSemantic("unmapped", target))]);

        Assert.Empty(groups);
    }

    [Fact]
    public void Builder_ProcessesMultipleTargetsAndDeduplicatesOnlyIdenticalTargetOperationLinks()
    {
        var semantic = Semantic(
            "multi-target",
            ItemPropertyApplicability.UnconditionalDisplayedLocal,
            new ItemPropertyContribution
            {
                Targets =
                [
                    ItemPropertyTarget.FireDamage,
                    ItemPropertyTarget.ColdDamage,
                    ItemPropertyTarget.FireDamage,
                ],
                Operation = ItemPropertyOperation.Added,
            },
            new ItemPropertyContribution
            {
                Targets = [ItemPropertyTarget.FireDamage],
                Operation = ItemPropertyOperation.IncreasedPercent,
            });

        var groups = TradeSearchItemPropertyContributionGroupBuilder.Create(
            [Property(TradeSearchItemPropertyKind.ElementalDps)],
            [Component("multi-target", semantic)]);

        var group = Assert.Single(groups);
        Assert.Equal(
        [
            (ItemPropertyTarget.FireDamage, ItemPropertyOperation.Added),
            (ItemPropertyTarget.ColdDamage, ItemPropertyOperation.Added),
            (ItemPropertyTarget.FireDamage, ItemPropertyOperation.IncreasedPercent),
        ], group.Contributions.Select(contribution => (contribution.Target, contribution.Operation)));
        Assert.All(group.Contributions, contribution => Assert.Equal(0, contribution.ModifierFilterIndex));
    }

    [Fact]
    public void TradeSearchDraft_RecordCopiesKeepImmutableStableIndexGroups()
    {
        var original = CreateDraft(CopiedItemCorpus.LoadItems()[2]);
        var updatedModifiers = original.ModifierFilters
            .Select(component => component with { IsSelected = true })
            .ToArray();

        var copy = original with { ModifierFilters = updatedModifiers };

        Assert.Equal(original.ItemPropertyContributionGroups, copy.ItemPropertyContributionGroups);
        Assert.All(copy.ItemPropertyContributionGroups.SelectMany(group => group.Contributions), contribution =>
        {
            Assert.InRange(contribution.ModifierFilterIndex, 0, copy.ModifierFilters.Count - 1);
            Assert.True(copy.ModifierFilters[contribution.ModifierFilterIndex].IsSelected);
            Assert.False(original.ModifierFilters[contribution.ModifierFilterIndex].IsSelected);
        });

        var originalGroup = original.ItemPropertyContributionGroups[0];
        var changedGroup = originalGroup with
        {
            Contributions = originalGroup.Contributions.RemoveAt(0),
        };
        Assert.Equal(3, originalGroup.Contributions.Length);
        Assert.Equal(2, changedGroup.Contributions.Length);
    }

    [Fact]
    public void CreateDraft_GroupsDoNotChangeSelectionBoundsOrValidation()
    {
        var draft = CreateDraft(HorrorManglerWithAddedPhysical);
        var withoutPresentationMetadata = draft with { ItemPropertyContributionGroups = [] };

        Assert.All(draft.ModifierFilters, component => Assert.False(component.IsSelected));
        Assert.Equal(146m, draft.ModifierFilters[0].RequestedMinimum);
        Assert.Null(draft.ModifierFilters[0].RequestedMaximum);
        Assert.Equivalent(
            new TradeSearchDraftValidator().Validate(withoutPresentationMetadata),
            new TradeSearchDraftValidator().Validate(draft),
            strict: true);
        Assert.Equivalent(
            withoutPresentationMetadata.ModifierFilters,
            draft.ModifierFilters,
            strict: true);
        Assert.Equivalent(
            withoutPresentationMetadata.ItemProperties,
            draft.ItemProperties,
            strict: true);
    }

    [Fact]
    public void CreateDraft_PolishCultureDoesNotChangeGroupConstruction()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");

            var draft = CreateDraft(CopiedItemCorpus.LoadItems()[2]);

            var group = Assert.Single(draft.ItemPropertyContributionGroups);
            Assert.Equal(TradeSearchItemPropertyKind.ElementalDps, group.ParentKind);
            Assert.Equal([0, 1, 2], group.Contributions.Select(contribution => contribution.ModifierFilterIndex));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private TradeSearchDraft CreateDraft(string itemText)
    {
        var item = parser.Parse(itemText);
        var catalog = TradeSearchModifierSemanticProvenanceTests.ReviewedWeaponCatalog();
        var result = mapper.CreateDraft(
            item,
            itemBaseResolution: new ParsedItemBaseResolver().Resolve(item, catalog),
            modifierResolutions: resolver.Resolve(item, catalog),
            gameDataCatalog: catalog);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }

    private static TradeSearchItemProperty Property(TradeSearchItemPropertyKind kind)
    {
        return new TradeSearchItemProperty
        {
            Kind = kind,
            Label = kind.ToString(),
            ObservedValue = 1m,
            RequestedMinimum = 1m,
        };
    }

    private static ResolvedSearchComponent Component(
        string id,
        ItemPropertySemanticDescriptor? semantic)
    {
        return new ResolvedSearchComponent
        {
            ComponentId = id,
            OriginalText = id,
            ReviewedItemPropertySemantic = semantic,
        };
    }

    private static ItemPropertySemanticDescriptor DamageSemantic(
        string id,
        ItemPropertyTarget target)
    {
        return Semantic(
            id,
            ItemPropertyApplicability.UnconditionalDisplayedLocal,
            new ItemPropertyContribution
            {
                Targets = [target],
                Operation = ItemPropertyOperation.Added,
            });
    }

    private static ItemPropertySemanticDescriptor Semantic(
        string id,
        ItemPropertyApplicability applicability,
        params ItemPropertyContribution[] contributions)
    {
        return new ItemPropertySemanticDescriptor
        {
            Id = id,
            Applicability = applicability,
            Contributions = contributions,
        };
    }

    private const string HorrorManglerWithAddedPhysical = """
        Item Class: One Hand Axes
        Rarity: Rare
        Horror Mangler
        Reaver Axe
        --------
        One Handed Axe
        Physical Damage: 94-283 (augmented)
        Critical Strike Chance: 5.00%
        Attacks per Second: 1.30
        --------
        Item Level: 85
        --------
        { Prefix Modifier "Reaver's" (Tier: 3) - Damage, Physical, Attack }
        30(25-34)% increased Physical Damage
        +60(47-72) to Accuracy Rating
        { Master Crafted Prefix Modifier "Upgraded" (Rank: 4) - Damage, Physical, Attack }
        116(100-129)% increased Physical Damage
        { Prefix Modifier "Flaring" (Tier: 1) - Damage, Physical, Attack }
        Adds 23(22-29) to 46(45-52) Physical Damage
        """;

    private const string LocalChaosWeapon = """
        Item Class: One Hand Axes
        Rarity: Rare
        Chaos Test
        Reaver Axe
        --------
        One Handed Axe
        Chaos Damage: 10-20 (augmented)
        Attacks per Second: 1.00
        --------
        Item Level: 85
        --------
        { Prefix Modifier "Chaotic" (Tier: 2) - Damage, Chaos, Attack }
        Adds 10(8-12) to 20(18-22) Chaos Damage
        """;

    private const string PhysicalAndAttackSpeedHybrid = """
        Item Class: One Hand Axes
        Rarity: Rare
        Hybrid Edge
        Reaver Axe
        --------
        One Handed Axe
        Physical Damage: 49-148 (augmented)
        Critical Strike Chance: 5.00%
        Attacks per Second: 1.44 (augmented)
        --------
        Item Level: 85
        --------
        { Prefix Modifier "Cruel Celebration" (Tier: 3) - Damage, Physical, Attack, Speed }
        30(25-34)% increased Physical Damage
        20(16-20)% increased Attack Speed
        """;

    private const string PhysicalModifierWithoutPhysicalProperty = """
        Item Class: One Hand Axes
        Rarity: Rare
        Missing Parent Test
        Reaver Axe
        --------
        One Handed Axe
        Elemental Damage: 10-20 (augmented)
        Attacks per Second: 1.00
        --------
        Item Level: 85
        --------
        { Prefix Modifier "Flaring" (Tier: 1) - Damage, Physical, Attack }
        Adds 23(22-29) to 46(45-52) Physical Damage
        """;

    private const string ArmageddonThirst = """
        Item Class: One Hand Axes
        Rarity: Rare
        Armageddon Thirst
        Reaver Axe
        --------
        One Handed Axe
        Physical Damage: 38-114
        Critical Strike Chance: 5.00%
        Attacks per Second: 1.50 (augmented)
        Weapon Range: 1.1 metres
        --------
        Item Level: 85
        --------
        { Suffix Modifier "of Thirst" (Tier: 1) - Attack, Physical, Mana }
        2.83(2.6-3.2)% of Physical Attack Damage Leeched as Mana
        { Master Crafted Suffix Modifier "of Craft" (Rank: 3) - Attack, Speed }
        20(16-20)% increased Attack Speed
        """;

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
}
