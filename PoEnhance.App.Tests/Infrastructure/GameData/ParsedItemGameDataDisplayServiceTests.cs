using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.GameData;

public sealed class ParsedItemGameDataDisplayServiceTests
{
    private readonly ItemTextParser parser = new();

    [Fact]
    public void ResolveItemBase_DoesNotCallResolverWhenCatalogIsUnavailable()
    {
        var resolver = new CountingResolver();
        var service = new ParsedItemGameDataDisplayService(resolver);
        var item = parser.Parse("""
Item Class: Rings
Rarity: Rare
Dire Loop
Gold Ring
--------
Item Level: 80
""");

        var display = service.ResolveItemBase(item, catalog: null);

        Assert.False(display.IsAvailable);
        Assert.Equal("Unavailable", display.Status);
        Assert.Equal("Game data not loaded", display.Diagnostic);
        Assert.Equal(0, resolver.CallCount);
    }

    [Fact]
    public void ResolveItemBase_WithCatalogCallsResolverAndLeavesParsedItemUnchanged()
    {
        var resolver = new CountingResolver
        {
            Result = new ItemBaseResolutionResult
            {
                Status = ItemBaseResolutionStatus.Exact,
                MatchedItemBase = Base("item-base.gold-ring", "Gold Ring", "Rings"),
                ResolvedBaseId = "item-base.gold-ring",
                ResolvedBaseName = "Gold Ring",
                Diagnostics =
                [
                    new ItemBaseResolutionDiagnostic(
                        ItemBaseResolutionDiagnosticCodes.BaseExactMatch,
                        "Exact test match."),
                ],
            },
        };
        var service = new ParsedItemGameDataDisplayService(resolver);
        var item = parser.Parse("""
Item Class: Rings
Rarity: Rare
Dire Loop
Gold Ring
--------
Item Level: 80
""");
        var originalName = item.Name;
        var originalBaseType = item.BaseType;
        var originalDisplayName = item.DisplayName;
        var originalRawText = item.RawText;

        var display = service.ResolveItemBase(item, CreateCatalog());

        Assert.True(display.IsAvailable);
        Assert.Equal("Exact", display.Status);
        Assert.Equal("Gold Ring", display.ResolvedBaseName);
        Assert.Equal("item-base.gold-ring", display.ResolvedBaseId);
        Assert.Equal("BASE_EXACT_MATCH: Exact test match.", display.Diagnostic);
        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(originalName, item.Name);
        Assert.Equal(originalBaseType, item.BaseType);
        Assert.Equal(originalDisplayName, item.DisplayName);
        Assert.Equal(originalRawText, item.RawText);
    }

    [Fact]
    public void ResolveItemBase_UnknownResultIsDisplayedAsValidResolutionState()
    {
        var resolver = new CountingResolver
        {
            Result = new ItemBaseResolutionResult
            {
                Status = ItemBaseResolutionStatus.Unknown,
                Candidates =
                [
                    Base("item-base.one", "Shared Base", "Maps"),
                    Base("item-base.two", "Shared Base", "Maps"),
                    Base("item-base.three", "Shared Base", "Maps"),
                    Base("item-base.four", "Shared Base", "Maps"),
                    Base("item-base.five", "Shared Base", "Maps"),
                    Base("item-base.six", "Shared Base", "Maps"),
                ],
                Diagnostics =
                [
                    new ItemBaseResolutionDiagnostic(
                        ItemBaseResolutionDiagnosticCodes.BaseAmbiguous,
                        "Ambiguous test match."),
                ],
            },
        };
        var service = new ParsedItemGameDataDisplayService(resolver);
        var item = parser.Parse("""
Item Class: Maps
Rarity: Rare
Ancient Trial
Shared Base
--------
Item Level: 83
""");

        var display = service.ResolveItemBase(item, CreateCatalog());

        Assert.True(display.IsAvailable);
        Assert.Equal("Unknown", display.Status);
        Assert.Equal("BASE_AMBIGUOUS: Ambiguous test match.", display.Diagnostic);
        Assert.Equal(6, display.CandidateCount);
        Assert.Equal(5, display.CandidateNames.Count);
    }

    private static GameDataCatalog CreateCatalog()
    {
        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 1,
                DataVersion = "test-data",
                CreatedAtUtc = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero),
                Sources =
                [
                    new GameDataPackageSource
                    {
                        SourceId = "test",
                        RetrievedAtUtc = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero),
                    },
                ],
            },
            ItemBases = [Base("item-base.gold-ring", "Gold Ring", "Rings")],
            Modifiers = [],
            Stats = [],
            StatTranslations = [],
        });
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

    private sealed class CountingResolver : IParsedItemBaseResolver
    {
        public int CallCount { get; private set; }

        public ItemBaseResolutionResult Result { get; init; } = new()
        {
            Status = ItemBaseResolutionStatus.Unknown,
        };

        public ItemBaseResolutionResult Resolve(ParsedItem parsedItem, GameDataCatalog catalog)
        {
            CallCount++;
            return Result;
        }
    }
}
