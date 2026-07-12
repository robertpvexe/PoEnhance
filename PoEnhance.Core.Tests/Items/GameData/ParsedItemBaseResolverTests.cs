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
