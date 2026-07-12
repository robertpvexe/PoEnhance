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
            Base("item-base.leather-belt", "Leather Belt", "Belts"));
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
            Base("item-base.test-armour", "Shared Base", "Body Armours"),
            Base("item-base.test-map", "Shared Base", "Maps"));
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
    public void Resolve_ExactNameClassConflict_ReturnsMismatchWithCandidates()
    {
        var catalog = CreateCatalog(Base("item-base.gold-ring", "Gold Ring", "Rings"));
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
        var catalog = CreateCatalog(Base("item-base.gold-ring", "Gold Ring", "Rings"));
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
        var catalog = CreateCatalog(Base("item-base.granite-flask", "Granite Flask", "Utility Flasks"));
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
            Base("item-base.one", "Granite Flask", "Utility Flasks"),
            Base("item-base.two", "Granite Flask", "Utility Flasks"));
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
        var catalog = CreateCatalog(Base("item-base.granite-flask", "Granite Flask", "Utility Flasks"));
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
        var catalog = CreateCatalog(Base("item-base.gold-ring", "Gold Ring", "Rings"));
        var item = _parser.Parse("Item Level: 80");

        var result = _resolver.Resolve(item, catalog);

        Assert.Equal(ItemBaseResolutionStatus.Unknown, result.Status);
        Assert.Null(result.ResolvedBaseName);
        Assert.Equal(
            ItemBaseResolutionDiagnosticCodes.BaseNotFound,
            Assert.Single(result.Diagnostics).Code);
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
}
