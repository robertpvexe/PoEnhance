using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

/// <summary>
/// Stage 03.11: a Crafted multi-line modifier stays ONE intact ordinary modifier by default, and
/// only keeps splitting when a stat is a proven local unconditional derived contributor.
/// The catalog and clipboard fixtures are SYNTHETIC (no real crafted multi-line copy was
/// captured); stat ids and the reviewed local semantics mirror real GameData shapes.
/// </summary>
public sealed class CraftedMultiLineKeepOneTests
{
    private const string PhysicalPercent = "local_physical_damage_+%";
    private const string Accuracy = "local_accuracy_rating";
    private const string LocalPhysicalMinimum = "local_minimum_added_physical_damage";
    private const string LocalPhysicalMaximum = "local_maximum_added_physical_damage";
    private const string LocalFireMinimum = "local_minimum_added_fire_damage";
    private const string LocalFireMaximum = "local_maximum_added_fire_damage";
    private const string GlobalFireMinimum = "global_minimum_added_fire_damage";
    private const string GlobalFireMaximum = "global_maximum_added_fire_damage";
    private const string GlobalLightningMinimum = "global_minimum_added_lightning_damage";
    private const string GlobalLightningMaximum = "global_maximum_added_lightning_damage";
    private const string GlobalChaosMinimum = "global_minimum_added_chaos_damage";
    private const string GlobalChaosMaximum = "global_maximum_added_chaos_damage";
    private const string GlobalPhysicalMinimum = "global_minimum_added_physical_damage";
    private const string GlobalPhysicalMaximum = "global_maximum_added_physical_damage";
    private const string Life = "base_maximum_life";
    private const string Mana = "base_maximum_mana";

    private readonly ItemTextParser parser = new();
    private readonly ParsedItemModifierCandidateResolver resolver = new();
    private readonly TradeSearchDraftMapper mapper = new();

    [Fact]
    public void LocalCraftedPhysicalAndAccuracy_StillSplitsAndFeedsPhysicalDps()
    {
        var draft = CreateDraft(
            Weapon(
                "Master Crafted Prefix Modifier \"Upgraded\" (Rank: 4) - Damage, Physical, Attack",
                "116(100-129)% increased Physical Damage",
                "+60(47-72) to Accuracy Rating"));

        Assert.Equal(2, draft.ModifierFilters.Count);
        var physical = Assert.Single(draft.ModifierFilters, component =>
            component.ReviewedItemPropertySemantic?.Id == "weapon.physical-damage.increased-percent.local");
        Assert.True(physical.IsCrafted);
        Assert.Equal(ModifierStatMappingProofStatus.ProvenExact, physical.StatMappingProof);
        Assert.Contains(
            draft.ItemPropertyContributionGroups,
            group => group.ParentKind == TradeSearchItemPropertyKind.PhysicalDps);
        Assert.All(draft.ModifierFilters, component => Assert.Equal(string.Empty, component.PhysicalSourceIdentity));
    }

    [Fact]
    public void LocalCraftedFlatPhysicalAndFire_StillSplitsAndFeedsBothDerivedParents()
    {
        var draft = CreateDraft(
            Weapon(
                "Master Crafted Prefix Modifier \"Blended\" (Rank: 3) - Damage, Physical, Attack",
                "Adds 5(4-6) to 9(8-10) Physical Damage",
                "Adds 3(2-4) to 11(10-12) Fire Damage"));

        Assert.Equal(2, draft.ModifierFilters.Count);
        Assert.Equal(
            ["weapon.physical-damage.added.local", "weapon.fire-damage.added.local"],
            draft.ModifierFilters.Select(component => component.ReviewedItemPropertySemantic?.Id));
        Assert.Contains(
            draft.ItemPropertyContributionGroups,
            group => group.ParentKind == TradeSearchItemPropertyKind.PhysicalDps);
        Assert.Contains(
            draft.ItemPropertyContributionGroups,
            group => group.ParentKind == TradeSearchItemPropertyKind.ElementalDps);
    }

    [Fact]
    public void GlobalCraftedFireAndLightningDamage_StaysOneOrdinaryModifierWithoutWeaponDps()
    {
        var draft = CreateDraft(
            Weapon(
                "Master Crafted Prefix Modifier \"Stormfire\" (Rank: 3) - Damage, Elemental",
                "Adds 4(3-5) to 8(7-9) Fire Damage",
                "Adds 1(1-2) to 12(11-13) Lightning Damage"));

        AssertOneIntactOrdinaryModifier(draft, expectedLineCount: 2);
        Assert.Empty(draft.ItemPropertyContributionGroups);
    }

    [Fact]
    public void GlobalCraftedChaosAndGlobalPhysical_StaysOneOrdinaryModifierWithoutWeaponDps()
    {
        var draft = CreateDraft(
            Weapon(
                "Master Crafted Prefix Modifier \"Bloodrot\" (Rank: 3) - Damage, Chaos, Physical",
                "Adds 3(2-4) to 5(4-6) Chaos Damage",
                "Adds 4(3-5) to 8(7-9) Physical Damage"));

        AssertOneIntactOrdinaryModifier(draft, expectedLineCount: 2);
        Assert.Empty(draft.ItemPropertyContributionGroups);
    }

    [Fact]
    public void CraftedLifeAndMana_StaysOneOrdinaryModifier()
    {
        var draft = CreateDraft(
            Weapon(
                "Master Crafted Prefix Modifier \"Blessed\" (Rank: 2) - Life, Mana",
                "+20(18-22) to maximum Life",
                "+10(8-12) to maximum Mana"));

        AssertOneIntactOrdinaryModifier(draft, expectedLineCount: 2);
    }

    [Fact]
    public void NonCraftedLifeAndMana_StillSplitsIntoIndependentLines()
    {
        var draft = CreateDraft(
            Weapon(
                "Prefix Modifier \"Sage's\" (Tier: 2) - Life, Mana",
                "+20(18-22) to maximum Life",
                "+10(8-12) to maximum Mana"));

        Assert.Equal(2, draft.ModifierFilters.Count);
        Assert.Equal(
            ["+20(18-22) to maximum Life", "+10(8-12) to maximum Mana"],
            draft.ModifierFilters.Select(component => component.OriginalText));
        Assert.Equal([0, 1], draft.ModifierFilters.Select(component => component.SourceLineIndex));
        Assert.All(draft.ModifierFilters, component =>
        {
            Assert.False(component.IsCrafted);
            Assert.Equal(ModifierStatMappingProofStatus.ProvenExact, component.StatMappingProof);
            Assert.Equal(string.Empty, component.PhysicalSourceIdentity);
        });
    }

    private static void AssertOneIntactOrdinaryModifier(TradeSearchDraft draft, int expectedLineCount)
    {
        var component = Assert.Single(draft.ModifierFilters);
        Assert.True(component.IsCrafted);
        Assert.Equal(ModifierStatMappingProofStatus.WholeVector, component.StatMappingProof);
        Assert.Equal(-1, component.SourceLineIndex);
        Assert.Equal(expectedLineCount, component.OriginalText.Split(Environment.NewLine).Length);
        Assert.True(component.ResolvedStatIds.Count >= 2);
        Assert.Null(component.ReviewedItemPropertySemantic);
        Assert.Equal(string.Empty, component.PhysicalSourceIdentity);
        Assert.Empty(component.LogicalContributions);
    }

    private TradeSearchDraft CreateDraft(string itemText)
    {
        var catalog = Catalog();
        var item = parser.Parse(itemText);
        var resolutions = resolver.Resolve(item, catalog);
        var result = mapper.CreateDraft(
            item,
            modifierResolutions: resolutions,
            gameDataCatalog: catalog);

        Assert.True(result.IsSuccess);
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }

    private static string Weapon(string header, params string[] lines) => $$"""
        Item Class: One Hand Axes
        Rarity: Rare
        Synthetic Keep One
        Reaver Axe
        --------
        One Handed Axe
        Physical Damage: 38-114
        Elemental Damage: 3-11 (augmented)
        Critical Strike Chance: 5.00%
        Attacks per Second: 1.50
        --------
        Item Level: 85
        --------
        { {{header}} }
        {{string.Join("\n", lines)}}
        """;

    private static GameDataCatalog Catalog()
    {
        var modifiers = new[]
        {
            Mod("upgraded", "Upgraded", ModifierGenerationType.Prefix, "crafted", PhysicalPercent, Accuracy),
            Mod("blended", "Blended", ModifierGenerationType.Prefix, "crafted",
                LocalPhysicalMinimum, LocalPhysicalMaximum, LocalFireMinimum, LocalFireMaximum),
            Mod("stormfire", "Stormfire", ModifierGenerationType.Prefix, "crafted",
                GlobalFireMinimum, GlobalFireMaximum, GlobalLightningMinimum, GlobalLightningMaximum),
            Mod("bloodrot", "Bloodrot", ModifierGenerationType.Prefix, "crafted",
                GlobalChaosMinimum, GlobalChaosMaximum, GlobalPhysicalMinimum, GlobalPhysicalMaximum),
            Mod("blessed", "Blessed", ModifierGenerationType.Prefix, "crafted", Life, Mana),
            Mod("sages", "Sage's", ModifierGenerationType.Prefix, null, Life, Mana),
        };
        var translations = new[]
        {
            Translation("physical-percent", [PhysicalPercent], "{0}% increased Physical Damage"),
            Translation("accuracy", [Accuracy], "+{0} to Accuracy Rating"),
            Translation("local-physical", [LocalPhysicalMinimum, LocalPhysicalMaximum], "Adds {0} to {1} Physical Damage"),
            Translation("local-fire", [LocalFireMinimum, LocalFireMaximum], "Adds {0} to {1} Fire Damage"),
            Translation("global-fire", [GlobalFireMinimum, GlobalFireMaximum], "Adds {0} to {1} Fire Damage"),
            Translation("global-lightning", [GlobalLightningMinimum, GlobalLightningMaximum], "Adds {0} to {1} Lightning Damage"),
            Translation("global-chaos", [GlobalChaosMinimum, GlobalChaosMaximum], "Adds {0} to {1} Chaos Damage"),
            Translation("global-physical", [GlobalPhysicalMinimum, GlobalPhysicalMaximum], "Adds {0} to {1} Physical Damage"),
            Translation("life", [Life], "+{0} to maximum Life"),
            Translation("mana", [Mana], "+{0} to maximum Mana"),
        };
        var semantics = new[]
        {
            Semantic("weapon.physical-damage.increased-percent.local", [PhysicalPercent],
                ItemPropertyTarget.PhysicalDamage, ItemPropertyOperation.IncreasedPercent),
            Semantic("weapon.physical-damage.added.local", [LocalPhysicalMinimum, LocalPhysicalMaximum],
                ItemPropertyTarget.PhysicalDamage, ItemPropertyOperation.Added),
            Semantic("weapon.fire-damage.added.local", [LocalFireMinimum, LocalFireMaximum],
                ItemPropertyTarget.FireDamage, ItemPropertyOperation.Added),
        };
        var localStats = semantics
            .SelectMany(semantic => semantic.OrderedStatIds)
            .Append(Accuracy)
            .ToHashSet(StringComparer.Ordinal);
        var statIds = modifiers
            .SelectMany(modifier => modifier.Stats.Select(stat => stat.StatId!))
            .Concat(localStats)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 1,
                DataVersion = "crafted-keep-one-test",
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
                    "One Hand Axe"),
            ],
            Modifiers = modifiers,
            Stats = statIds.Select(statId => new StatDefinition
            {
                Id = statId,
                IsLocal = localStats.Contains(statId),
            }).ToArray(),
            StatTranslations = translations,
            ItemPropertySemantics = semantics,
        });
    }

    private static ItemBaseRecord WeaponBase(string id, string name, string itemClass)
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
                PhysicalDamageMinimum = 38,
                PhysicalDamageMaximum = 114,
                AttackTimeMilliseconds = 833,
                CriticalStrikeChancePercent = 5.00m,
                Sources = [source],
            },
        };
    }

    private static ModifierDefinition Mod(
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

    private static StatTranslationDefinition Translation(
        string id,
        IReadOnlyList<string> statIds,
        string format)
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
                    ValueFormats = statIds.Select(_ => "#").ToArray(),
                    IndexHandlers = statIds.Select((_, index) =>
                        new StatTranslationIndexHandler { Index = index, Handlers = [] }).ToArray(),
                    FormatLines = [format],
                },
            ],
        };
    }

    private static ItemPropertySemanticDescriptor Semantic(
        string id,
        IReadOnlyList<string> statIds,
        ItemPropertyTarget target,
        ItemPropertyOperation operation)
    {
        return new ItemPropertySemanticDescriptor
        {
            Id = id,
            OrderedStatIds = statIds,
            Contributions =
            [
                new ItemPropertyContribution { Targets = [target], Operation = operation },
            ],
            Applicability = ItemPropertyApplicability.UnconditionalDisplayedLocal,
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
}
