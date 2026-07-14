using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class ParsedItemBaseResolverTests
{
    private readonly ItemTextParser _parser = new();
    private readonly ParsedItemBaseResolver _resolver = new();

    [Fact]
    public void Resolve_BaseTypeExactMatch_ReturnsCatalogRecordAndPreservesCatalogCasing()
    {
        var catalog = CreateCatalog(
            Base("item-base.leather-belt", "Leather Belt", "Belt"));
        var item = _parser.Parse("""
Item Class: Belts
Rarity: Unique
Screams of the Desiccated
leather belt
--------
Item Level: 85
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Exact, result.Status);
        Assert.Equal("item-base.leather-belt", result.ResolvedBaseId);
        Assert.Equal("Leather Belt", result.ResolvedBaseName);
        Assert.Equal("Leather Belt", result.MatchedItemBase?.Name);
        Assert.Equal(
            ItemBaseResolutionDiagnosticCodes.BaseExactMatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_ExactNameMultipleClasses_NarrowsByParsedItemClass()
    {
        var catalog = CreateCatalog(
            Base("item-base.test-armour", "Shared Base", "Body Armour"),
            Base("item-base.test-map", "Shared Base", "Map"));
        var item = _parser.Parse("""
Item Class: Maps
Rarity: Rare
Ancient Trial
Shared Base
--------
Item Level: 83
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Exact, result.Status);
        Assert.Equal("item-base.test-map", result.ResolvedBaseId);
    }

    [Fact]
    public void Resolve_ExactNameTreatsDisplayAndCatalogItemClassFormsAsCompatible()
    {
        var catalog = CreateCatalog(Base("item-base.gold-ring", "Gold Ring", "Ring"));
        var item = _parser.Parse("""
Item Class: Rings
Rarity: Rare
Dire Loop
Gold Ring
--------
Item Level: 80
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Exact, result.Status);
        Assert.Equal("item-base.gold-ring", result.ResolvedBaseId);
    }

    [Fact]
    public void Resolve_ExactNameClassConflict_ReturnsMismatchWithCandidates()
    {
        var catalog = CreateCatalog(Base("item-base.gold-ring", "Gold Ring", "Ring"));
        var item = _parser.Parse("""
Item Class: Amulets
Rarity: Rare
Dire Beads
Gold Ring
--------
Item Level: 75
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Unknown, result.Status);
        Assert.Null(result.MatchedItemBase);
        Assert.Equal("item-base.gold-ring", Assert.Single(result.Candidates).Id);
        Assert.Equal(
            ItemBaseResolutionDiagnosticCodes.BaseItemClassMismatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_UnknownCatalogBaseType_ReturnsUnknownWithoutMutatingItemOrCatalog()
    {
        var catalog = CreateCatalog(Base("item-base.gold-ring", "Gold Ring", "Ring"));
        var originalItemBases = catalog.ItemBases.ToArray();
        var item = _parser.Parse("""
Item Class: Belts
Rarity: Rare
Dire Buckle
Crystal Belt
--------
Item Level: 84
""");

        var enrichment = _resolver.Enrich(item, catalog);

        Assert.Same(item, enrichment.ParsedItem);
        Assert.Equal("Crystal Belt", item.BaseType);
        Assert.Equal(ItemBaseResolutionStatus.Unknown, enrichment.BaseResolution.Status);
        Assert.Null(enrichment.BaseResolution.MatchedItemBase);
        Assert.Null(enrichment.BaseResolution.ResolvedBaseId);
        Assert.Null(enrichment.BaseResolution.ResolvedBaseName);
        Assert.Empty(enrichment.BaseResolution.Candidates);
        Assert.Equal("Crystal Belt", enrichment.EffectiveBaseName);
        Assert.Equal(originalItemBases, catalog.ItemBases);
        Assert.Equal(
            ItemBaseResolutionDiagnosticCodes.BaseNotFound,
            Assert.Single(enrichment.BaseResolution.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_MagicDisplayName_UsesCompleteTokenBoundarySuffixBeforeOptionalOfSuffix()
    {
        var catalog = CreateCatalog(Base("item-base.granite-flask", "Granite Flask", "UtilityFlask"));
        var item = _parser.Parse("""
Item Class: Utility Flasks
Rarity: Magic
Constant Granite Flask of Tapping
--------
Item Level: 84
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Probable, result.Status);
        Assert.Equal("item-base.granite-flask", result.ResolvedBaseId);
        Assert.Equal("Granite Flask", result.ResolvedBaseName);
        Assert.Equal(
            ItemBaseResolutionDiagnosticCodes.BaseProbableMagicSuffixMatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_MagicDisplayNameWithOnlySuffix_ResolvesReaverAxeAndPreservesCopiedIdentity()
    {
        var catalog = CreateCatalog(Base("item-base.reaver-axe", "Reaver Axe", "One Hand Axe"));
        var item = _parser.Parse("""
Item Class: One Hand Axes
Rarity: Magic
Reaver Axe of Celebration
--------
One Handed Axe
Physical Damage: 38-114
Critical Strike Chance: 5.00%
Attacks per Second: 1.51 (augmented)
Weapon Range: 1.1 metres
--------
Requirements:
Level: 61
Str: 167
Dex: 57
--------
Sockets: B B-R
--------
Item Level: 85
--------
{ Suffix Modifier "of Celebration" (Tier: 1) - Attack, Speed }
26(26-27)% increased Attack Speed
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Probable, result.Status);
        Assert.Equal("item-base.reaver-axe", result.ResolvedBaseId);
        Assert.Equal("Reaver Axe", result.ResolvedBaseName);
        Assert.Equal("Magic", item.Rarity);
        Assert.Equal("Reaver Axe of Celebration", item.DisplayName);
        Assert.Null(item.BaseType);
        Assert.Equal("26(26-27)% increased Attack Speed", Assert.Single(item.Modifiers).Effects.Single().Text);
    }

    [Fact]
    public void Resolve_MagicDisplayNameWithOnlyPrefix_ResolvesBaseByCatalogPhrase()
    {
        var catalog = CreateCatalog(Base("item-base.reaver-axe", "Reaver Axe", "One Hand Axe"));
        var item = _parser.Parse("""
Item Class: One Hand Axes
Rarity: Magic
Flaring Reaver Axe
--------
Item Level: 85
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Probable, result.Status);
        Assert.Equal("item-base.reaver-axe", result.ResolvedBaseId);
        Assert.Equal("Flaring Reaver Axe", item.DisplayName);
        Assert.Equal("Magic", item.Rarity);
    }

    [Fact]
    public void Resolve_MagicDisplayNameWithPrefixAndSuffix_ResolvesSupremeSpikedShieldAndPreservesLogicalEffects()
    {
        var catalog = CreateCatalog(Base("item-base.supreme-spiked-shield", "Supreme Spiked Shield", "Shield"));
        var item = _parser.Parse("""
Item Class: Shields
Rarity: Magic
Wasp's Supreme Spiked Shield of Thick Skin
--------
Chance to Block: 24%
Evasion Rating: 362 (augmented)
Energy Shield: 73 (augmented)
--------
Requirements:
Level: 70
Dex: 85
Int: 85
--------
Sockets: B-B-G
--------
Item Level: 84
--------
{ Implicit Modifier }
+5% chance to Suppress Spell Damage
(40% of Damage from Suppressed Hits and Ailments they inflict is prevented)
--------
{ Prefix Modifier "Wasp's" (Tier: 3) - Defences, Evasion, Energy Shield }
31(27-32)% increased Evasion and Energy Shield
13(12-13)% increased Stun and Block Recovery
{ Suffix Modifier "of Thick Skin" (Tier: 6) }
11(11-13)% increased Stun and Block Recovery
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Probable, result.Status);
        Assert.Equal("item-base.supreme-spiked-shield", result.ResolvedBaseId);
        Assert.Equal("Supreme Spiked Shield", result.ResolvedBaseName);
        Assert.Equal("Magic", item.Rarity);
        Assert.Equal("Wasp's Supreme Spiked Shield of Thick Skin", item.DisplayName);
        Assert.Null(item.BaseType);
        Assert.Equal(
            [
                "+5% chance to Suppress Spell Damage",
                "31(27-32)% increased Evasion and Energy Shield",
                "13(12-13)% increased Stun and Block Recovery",
                "11(11-13)% increased Stun and Block Recovery",
            ],
            item.Modifiers.SelectMany(modifier => modifier.Effects).Select(effect => effect.Text));
    }

    [Fact]
    public void Resolve_MagicDisplayName_DoesNotMatchSubstringInsideToken()
    {
        var catalog = CreateCatalog(Base("item-base.ring", "Ring", "Rings"));
        var item = _parser.Parse("""
Item Class: Rings
Rarity: Magic
Storm Ringmail of Skill
--------
Item Level: 84
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Unknown, result.Status);
        Assert.Empty(result.Candidates);
        Assert.Equal(
            ItemBaseResolutionDiagnosticCodes.BaseNotFound,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_MagicDisplayName_NormalizesWhitespaceAndIgnoresCase()
    {
        var catalog = CreateCatalog(Base("item-base.reaver-axe", "Reaver   Axe", "One Hand Axe"));
        var item = _parser.Parse("""
Item Class: One Hand Axes
Rarity: Magic
Flaring   reaver axe   of Celebration
--------
Item Level: 85
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Probable, result.Status);
        Assert.Equal("item-base.reaver-axe", result.ResolvedBaseId);
    }

    [Fact]
    public void Resolve_MagicDisplayName_PrefersLongestMatchingBaseName()
    {
        var catalog = CreateCatalog(
            Base("item-base.ring", "Ring", "Rings"),
            Base("item-base.iron-ring", "Iron Ring", "Rings"));
        var item = _parser.Parse("""
Item Class: Rings
Rarity: Magic
Gleaming Iron Ring of Skill
--------
Item Level: 84
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Probable, result.Status);
        Assert.Equal("item-base.iron-ring", result.ResolvedBaseId);
    }

    [Fact]
    public void Resolve_MagicDisplayName_NarrowsByItemClassBeforeChoosingLongestBaseName()
    {
        var catalog = CreateCatalog(
            Base("item-base.incompatible", "Viper Iron Ring", "Belt"),
            Base("item-base.iron-ring", "Iron Ring", "Ring"));
        var item = _parser.Parse("""
Item Class: Rings
Rarity: Magic
Viper Iron Ring of Skill
--------
Item Level: 84
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Probable, result.Status);
        Assert.Equal("item-base.iron-ring", result.ResolvedBaseId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_MagicDisplayName_CatalogOrderingDoesNotChangeLongestBaseNameResult(bool reverseCatalogOrder)
    {
        var bases = new[]
        {
            Base("item-base.ring", "Ring", "Ring"),
            Base("item-base.iron-ring", "Iron Ring", "Ring"),
        };
        var catalog = CreateCatalog(reverseCatalogOrder ? bases.Reverse().ToArray() : bases);
        var item = _parser.Parse("""
Item Class: Rings
Rarity: Magic
Gleaming Iron Ring of Skill
--------
Item Level: 84
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Probable, result.Status);
        Assert.Equal("item-base.iron-ring", result.ResolvedBaseId);
    }

    [Fact]
    public void Resolve_MagicDisplayName_EqualLongestCandidatesRemainAmbiguousDeterministically()
    {
        var catalog = CreateCatalog(
            Base("item-base.one", "Granite Flask", "UtilityFlask"),
            Base("item-base.two", "Granite Flask", "UtilityFlask"));
        var item = _parser.Parse("""
Item Class: Utility Flasks
Rarity: Magic
Constant Granite Flask of Tapping
--------
Item Level: 84
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Unknown, result.Status);
        Assert.Equal(
            ["item-base.one", "item-base.two"],
            result.Candidates.Select(candidate => candidate.Id));
        Assert.Equal(
            ItemBaseResolutionDiagnosticCodes.BaseAmbiguous,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_MagicDisplayName_CandidateClassConflictReturnsMismatch()
    {
        var catalog = CreateCatalog(Base("item-base.granite-flask", "Granite Flask", "UtilityFlask"));
        var item = _parser.Parse("""
Item Class: Life Flasks
Rarity: Magic
Constant Granite Flask of Tapping
--------
Item Level: 84
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Unknown, result.Status);
        Assert.Equal("item-base.granite-flask", Assert.Single(result.Candidates).Id);
        Assert.Equal(
            ItemBaseResolutionDiagnosticCodes.BaseItemClassMismatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_WithoutBaseTypeOrMagicName_ReturnsUnknown()
    {
        var catalog = CreateCatalog(Base("item-base.gold-ring", "Gold Ring", "Ring"));
        var item = _parser.Parse("Item Level: 80");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Unknown, result.Status);
        Assert.Null(result.ResolvedBaseName);
        Assert.Equal(
            ItemBaseResolutionDiagnosticCodes.BaseNotFound,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_SkitteringIncubatorWithVerifiedCatalogClass_ReturnsExact()
    {
        var catalog = CreateCatalog(Base(
            "Metadata/Items/Currency/CurrencyIncubationScarabsStackable",
            "Skittering Incubator",
            "IncubatorStackable"));
        var item = _parser.Parse("""
Item Class: Incubators
Rarity: Currency
Skittering Incubator
--------
Item Level: 1
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Exact, result.Status);
        Assert.Equal("Metadata/Items/Currency/CurrencyIncubationScarabsStackable", result.ResolvedBaseId);
    }

    [Fact]
    public void Resolve_RangerBowWithVerifiedCatalogClass_ReturnsExact()
    {
        var catalog = CreateCatalog(Base(
            "Metadata/Items/Weapons/TwoHandWeapons/Bows/Bow18",
            "Ranger Bow",
            "Bow"));
        var item = _parser.Parse("""
Item Class: Bows
Rarity: Rare
Dire Flight
Ranger Bow
--------
Item Level: 84
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Exact, result.Status);
        Assert.Equal("Metadata/Items/Weapons/TwoHandWeapons/Bows/Bow18", result.ResolvedBaseId);
    }

    [Theory]
    [InlineData("One Hand Axes", "Reaver Axe", "One Hand Axe")]
    [InlineData("Sceptres", "Platinum Sceptre", "Sceptre")]
    [InlineData("Wands", "Blasting Wand", "Wand")]
    public void Resolve_CopiedPluralWeaponClassWithVerifiedCatalogClass_ReturnsExact(
        string copiedItemClass,
        string baseName,
        string catalogItemClass)
    {
        var catalog = CreateCatalog(Base($"item-base.{baseName}", baseName, catalogItemClass));
        var item = _parser.Parse($"""
Item Class: {copiedItemClass}
Rarity: Rare
Dire Test
{baseName}
--------
Item Level: 84
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Exact, result.Status);
        Assert.Equal(baseName, result.ResolvedBaseName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_AgonyRamPlatinumSceptre_ReturnsExactRegardlessOfCandidateOrdering(
        bool reverseCatalogOrder)
    {
        var bases = new[]
        {
            Base("item-base.irrelevant", "Platinum Sceptre", "Rune Dagger"),
            Base("item-base.platinum-sceptre", "Platinum Sceptre", "Sceptre"),
        };
        var catalog = CreateCatalog(reverseCatalogOrder ? bases.Reverse().ToArray() : bases);
        var item = _parser.Parse(AgonyRamText);

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Exact, result.Status);
        Assert.Equal("item-base.platinum-sceptre", result.ResolvedBaseId);
        Assert.Equal("Platinum Sceptre", result.ResolvedBaseName);
        Assert.Equal("Rare", item.Rarity);
        Assert.Equal("Agony Ram", item.DisplayName);
        Assert.Equal("Platinum Sceptre", item.BaseType);
        Assert.Equal(5, item.Modifiers.SelectMany(modifier => modifier.Effects).Count());
        Assert.Equal(
            [
                "30% increased Elemental Damage",
                "Adds 2 to 28(25-29) Lightning Damage",
                "+28(25-29)% to Global Critical Strike Multiplier",
                "+23(20-23)% to Damage over Time Multiplier",
                "15(13-15)% increased Cold Damage",
            ],
            item.Modifiers.SelectMany(modifier => modifier.Effects).Select(effect => effect.Text));
    }

    [Fact]
    public void Resolve_PlatinumSceptreWithIncompatibleCopiedClass_DoesNotResolve()
    {
        var catalog = CreateCatalog(Base("item-base.platinum-sceptre", "Platinum Sceptre", "Sceptre"));
        var item = _parser.Parse(AgonyRamText.Replace("Item Class: Sceptres", "Item Class: Wands", StringComparison.Ordinal));

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Unknown, result.Status);
        Assert.Equal(
            ItemBaseResolutionDiagnosticCodes.BaseItemClassMismatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_ManifoldRingWhenCatalogContainsBase_ReturnsExact()
    {
        var catalog = CreateCatalog(Base("item-base.manifold-ring", "Manifold Ring", "Ring"));
        var item = _parser.Parse("""
Item Class: Rings
Rarity: Rare
Havoc Loop
Manifold Ring
--------
Item Level: 84
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Exact, result.Status);
        Assert.Equal("item-base.manifold-ring", result.ResolvedBaseId);
    }

    [Fact]
    public void Resolve_GenericCurrencyBaseTypeMayUseDisplayNameForExplicitCurrencyPath()
    {
        var catalog = CreateCatalog(Base("item-base.coin-restoration", "Coin of Restoration", "StackableCurrency"));
        var item = CreateParsedItem(
            itemClass: "Stackable Currency",
            rarity: "Currency",
            name: "Coin of Restoration",
            baseType: "Currency");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Exact, result.Status);
        Assert.Equal("item-base.coin-restoration", result.ResolvedBaseId);
        Assert.Equal("Currency", item.BaseType);
    }

    [Fact]
    public void Resolve_GenericCurrencyBaseTypeWhenDisplayNameAbsentFromCatalog_ReturnsUnknown()
    {
        var catalog = CreateCatalog(Base("item-base.chaos", "Chaos Orb", "StackableCurrency"));
        var item = CreateParsedItem(
            itemClass: "Stackable Currency",
            rarity: "Currency",
            name: "Coin of Restoration",
            baseType: "Currency");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Unknown, result.Status);
        Assert.Equal(
            ItemBaseResolutionDiagnosticCodes.BaseNotFound,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_RareDisplayNameIsNotUsedAsUnrestrictedFallback()
    {
        var catalog = CreateCatalog(Base("item-base.rare-name", "Lost Choice", "Map"));
        var item = _parser.Parse("""
Item Class: Maps
Rarity: Rare
Lost Choice
Map (Tier 16)
--------
Item Level: 83
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Unknown, result.Status);
        Assert.Empty(result.Candidates);
        Assert.Equal(
            ItemBaseResolutionDiagnosticCodes.BaseNotFound,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_UniqueDisplayNameIsNotUsedAsUnrestrictedFallback()
    {
        var catalog = CreateCatalog(Base("item-base.unique-name", "The Tempest", "Bow"));
        var item = _parser.Parse("""
Item Class: Bows
Rarity: Unique
The Tempest
Missing Bow Base
--------
Item Level: 84
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Unknown, result.Status);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void Resolve_SynthesisedReaverHelmetWithConfirmedState_ReturnsProbable()
    {
        var catalog = CreateCatalog(Base(
            "Metadata/Items/Armours/Helmets/HelmetStr6",
            "Reaver Helmet",
            "Helmet"));
        var item = _parser.Parse("""
Item Class: Helmets
Rarity: Rare
Synthesised Item
Gale Dome
Synthesised Reaver Helmet
--------
Item Level: 84
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Probable, result.Status);
        Assert.Equal("Metadata/Items/Armours/Helmets/HelmetStr6", result.ResolvedBaseId);
        Assert.Equal("Synthesised Reaver Helmet", item.BaseType);
        Assert.Equal(
            ItemBaseResolutionDiagnosticCodes.BaseProbableStateDecorationMatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_SynthesisedPrefixWithoutConfirmedState_ReturnsUnknown()
    {
        var catalog = CreateCatalog(Base(
            "Metadata/Items/Armours/Helmets/HelmetStr6",
            "Reaver Helmet",
            "Helmet"));
        var item = _parser.Parse("""
Item Class: Helmets
Rarity: Rare
Gale Dome
Synthesised Reaver Helmet
--------
Item Level: 84
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Unknown, result.Status);
        Assert.Empty(result.Candidates);
        Assert.Equal(
            ItemBaseResolutionDiagnosticCodes.BaseNotFound,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_MapTierRareItem_RemainsUnknown()
    {
        var catalog = CreateCatalog(
            Base("item-base.lost-choice", "Lost Choice", "Map"),
            Base("item-base.crimson-temple", "Crimson Temple Map", "Map"));
        var item = _parser.Parse("""
Item Class: Maps
Rarity: Rare
Lost Choice
Map (Tier 16)
--------
Item Level: 83
""");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Unknown, result.Status);
        Assert.Empty(result.Candidates);
        Assert.Equal(
            ItemBaseResolutionDiagnosticCodes.BaseNotFound,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_CurrentRuntimePathsNeverReturnGeneric()
    {
        var catalog = CreateCatalog(
            Base("item-base.gold-ring", "Gold Ring", "Ring"),
            Base("item-base.granite-flask", "Granite Flask", "UtilityFlask"),
            Base("item-base.reaver-helmet", "Reaver Helmet", "Helmet"));
        var exactItem = _parser.Parse("""
Item Class: Rings
Rarity: Rare
Dire Loop
Gold Ring
--------
Item Level: 80
""");
        var magicItem = _parser.Parse("""
Item Class: Utility Flasks
Rarity: Magic
Constant Granite Flask of Tapping
--------
Item Level: 84
""");
        var stateDecoratedItem = _parser.Parse("""
Item Class: Helmets
Rarity: Rare
Synthesised Item
Gale Dome
Synthesised Reaver Helmet
--------
Item Level: 84
""");
        var unknownItem = _parser.Parse("""
Item Class: Rings
Rarity: Rare
Dire Loop
Missing Ring
--------
Item Level: 80
""");

        var statuses = new[]
        {
            _resolver.Resolve(exactItem, catalog).Status,
            _resolver.Resolve(magicItem, catalog).Status,
            _resolver.Resolve(stateDecoratedItem, catalog).Status,
            _resolver.Resolve(unknownItem, catalog).Status,
        };

        Assert.DoesNotContain(ItemBaseResolutionStatus.Generic, statuses);
    }

    private static GameDataCatalog CreateCatalog(params ItemBaseRecord[] itemBases)
    {
        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = CreateManifest(),
            ItemBases = itemBases,
            Modifiers = [],
            Stats = [],
            StatTranslations = [],
        });
    }

    private const string AgonyRamText = """
Item Class: Sceptres
Rarity: Rare
Agony Ram
Platinum Sceptre
--------
Sceptre
Physical Damage: 51-76
Elemental Damage: 2-28 (augmented)
Critical Strike Chance: 7.00%
Attacks per Second: 1.25
Weapon Range: 1.1 metres
--------
Requirements:
Level: 62
Str: 113
Int: 113
--------
Sockets: B-R-R
--------
Item Level: 85
--------
{ Implicit Modifier - Damage, Elemental }
30% increased Elemental Damage
--------
{ Prefix Modifier "Buzzing" (Tier: 9) - Damage, Elemental, Lightning, Attack }
Adds 2 to 28(25-29) Lightning Damage
{ Suffix Modifier "of Fury" (Tier: 3) - Damage, Critical }
+28(25-29)% to Global Critical Strike Multiplier
{ Suffix Modifier "of Melting" (Tier: 2) - Damage }
+23(20-23)% to Damage over Time Multiplier
{ Suffix Modifier "of Sleet" (Tier: 5) - Damage, Elemental, Cold }
15(13-15)% increased Cold Damage
""";

    private static GameDataPackageManifest CreateManifest()
    {
        return new GameDataPackageManifest
        {
            SchemaVersion = 1,
            DataVersion = "test",
            CreatedAtUtc = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero),
            League = "test",
            Patch = "test",
            Sources =
            [
                new GameDataPackageSource
                {
                    SourceId = "test",
                    RetrievedAtUtc = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero),
                    SourceVersion = "test",
                },
            ],
        };
    }

    private static ItemBaseRecord Base(string id, string name, string itemClass)
    {
        return new ItemBaseRecord
        {
            Id = id,
            Name = name,
            ItemClass = itemClass,
            Sources =
            [
                new GameDataSourceReference
                {
                    SourceId = "test",
                    ExternalId = id,
                },
            ],
        };
    }

    private static ParsedItem CreateParsedItem(
        string? itemClass,
        string? rarity,
        string? name,
        string? baseType,
        IReadOnlyList<string>? itemStates = null)
    {
        return new ParsedItem(
            RawText: string.Empty,
            InputFormat: ParsedItemInputFormat.Unknown,
            ItemClass: itemClass,
            Rarity: rarity,
            Name: name,
            BaseType: baseType,
            ItemTypeDescriptor: null,
            ItemStates: itemStates ?? [],
            NoteLines: [],
            ListingNote: null,
            TraditionalInfluences: [],
            EldritchInfluences: [],
            IsCorrupted: false,
            ItemLevel: null,
            PropertyLines: [],
            Modifiers: [],
            ImplicitModifiers: [],
            PrefixModifiers: [],
            SuffixModifiers: [],
            UniqueModifiers: [],
            ExplicitModifiersWithUnknownKind: [],
            ModifierLines: [],
            FlavourTextLines: [],
            Enchantments: [],
            DescriptionLines: [],
            UnclassifiedLines: []);
    }
}
