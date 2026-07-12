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
        var service = new ParsedItemGameDataDisplayService(resolver, new CountingModifierCandidateResolver());
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

    [Fact]
    public void ResolveModifierCandidates_DoesNotCallResolverWhenCatalogIsUnavailable()
    {
        var resolver = new CountingModifierCandidateResolver();
        var service = new ParsedItemGameDataDisplayService(new CountingResolver(), resolver);
        var item = parser.Parse("""
Item Class: Rings
Rarity: Rare
Dire Loop
Gold Ring
--------
Item Level: 80
--------
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
""");

        var display = service.ResolveModifierCandidates(item, catalog: null);

        Assert.False(display.IsAvailable);
        Assert.Equal("Game data not loaded", display.Diagnostic);
        Assert.Empty(display.Results);
        Assert.Equal(0, resolver.CallCount);
    }

    [Fact]
    public void ResolveModifierCandidates_WithCatalogDisplaysDiagnosticsAndLimitsCandidates()
    {
        var item = parser.Parse("""
Item Class: Rings
Rarity: Rare
Dire Loop
Gold Ring
--------
Item Level: 80
--------
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
""");
        var resolver = new CountingModifierCandidateResolver
        {
            Results =
            [
                new ModifierCandidateResolutionResult(
                    0,
                    item.PrefixModifiers[0],
                    "Hale",
                    ParsedModifierKind.Prefix,
                    ModifierGenerationType.Prefix,
                    ModifierCandidateResolutionStatus.Unknown,
                    [
                        Modifier("mod.one", "Hale", ModifierGenerationType.Prefix),
                        Modifier("mod.two", "Hale", ModifierGenerationType.Prefix),
                        Modifier("mod.three", "Hale", ModifierGenerationType.Prefix),
                        Modifier("mod.four", "Hale", ModifierGenerationType.Prefix),
                        Modifier("mod.five", "Hale", ModifierGenerationType.Prefix),
                        Modifier("mod.six", "Hale", ModifierGenerationType.Prefix),
                    ],
                    [
                        new ModifierCandidateResolutionDiagnostic(
                            ModifierCandidateResolutionDiagnosticCodes.ModifierAmbiguous,
                            "Ambiguous test match."),
                    ]),
            ],
        };
        var service = new ParsedItemGameDataDisplayService(new CountingResolver(), resolver);

        var display = service.ResolveModifierCandidates(item, CreateCatalog());
        var result = Assert.Single(display.Results);

        Assert.True(display.IsAvailable);
        Assert.Equal("Unknown", result.Status);
        Assert.Equal("MODIFIER_AMBIGUOUS: Ambiguous test match.", result.Diagnostic);
        Assert.Equal(6, result.CandidateCount);
        Assert.Equal("0 name -> 0 kind -> 0 eligible", result.CountSummary);
        Assert.Equal(5, result.CandidateLabels.Count);
        Assert.Equal("mod.one (Hale)", result.CandidateLabels[0]);
        Assert.Equal(1, resolver.CallCount);
    }

    [Fact]
    public void ResolveModifierCandidates_DisplaysEachNarrowingStageAndKeepsCompleteResultCollection()
    {
        var item = parser.Parse("""
Item Class: Rings
Rarity: Rare
Dire Loop
Gold Ring
--------
Item Level: 80
--------
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
""");
        var candidates = Enumerable
            .Range(1, 6)
            .Select(index => Modifier($"mod.{index}", "Hale", ModifierGenerationType.Prefix))
            .ToArray();
        var resolver = new CountingModifierCandidateResolver
        {
            Results =
            [
                new ModifierCandidateResolutionResult(
                    0,
                    item.PrefixModifiers[0],
                    "Hale",
                    ParsedModifierKind.Prefix,
                    ModifierGenerationType.Prefix,
                    ModifierCandidateResolutionStatus.Unknown,
                    candidates,
                    [
                        new ModifierCandidateResolutionDiagnostic(
                            ModifierCandidateResolutionDiagnosticCodes.ModifierEligibilityAmbiguous,
                            "Ambiguous after eligibility."),
                    ],
                    NameCandidateCount: 42,
                    GenerationKindCandidateCount: 42,
                    EligibilityCandidateCount: 6),
            ],
        };
        var service = new ParsedItemGameDataDisplayService(new CountingResolver(), resolver);

        var display = service.ResolveModifierCandidates(item, CreateCatalog());
        var result = Assert.Single(display.Results);

        Assert.Equal("42 name -> 42 kind -> 6 eligible", result.CountSummary);
        Assert.Equal(6, result.CandidateCount);
        Assert.Equal(5, result.CandidateLabels.Count);
    }

    [Fact]
    public void ResolveModifierCandidates_PassesExistingItemBaseResolutionToResolver()
    {
        var baseResolution = new ItemBaseResolutionResult
        {
            Status = ItemBaseResolutionStatus.Exact,
            MatchedItemBase = Base("item-base.gold-ring", "Gold Ring", "Rings"),
        };
        var item = parser.Parse("""
Item Class: Rings
Rarity: Rare
Dire Loop
Gold Ring
--------
Item Level: 80
--------
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
""");
        var itemBaseResolver = new CountingResolver();
        var modifierResolver = new CountingModifierCandidateResolver();
        var service = new ParsedItemGameDataDisplayService(itemBaseResolver, modifierResolver);

        _ = service.ResolveModifierCandidates(item, CreateCatalog(), baseResolution);

        Assert.Equal(0, itemBaseResolver.CallCount);
        Assert.Same(baseResolution, modifierResolver.LastBaseResolution);
    }

    [Fact]
    public void ParsedItemStoresDescriptionEvenThoughRegularDisplayDoesNotExposeIt()
    {
        var item = parser.Parse("""
Item Class: Stackable Currency
Rarity: Currency
Orb of Testing
--------
Stack Size: 1/20
--------
Right click this item then left click another item to apply it.
""");

        Assert.NotEmpty(item.DescriptionLines);
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
            Modifiers = [Modifier("mod.prefix.hale", "Hale", ModifierGenerationType.Prefix)],
            Stats = [Stat("test_stat")],
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

    private static ModifierDefinition Modifier(
        string id,
        string name,
        ModifierGenerationType generationType)
    {
        return new ModifierDefinition
        {
            Id = id,
            GroupId = $"group.{id}",
            Name = name,
            GenerationType = generationType,
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = "test_stat",
                    MinValue = 1m,
                    MaxValue = 2m,
                },
            ],
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

    private static StatDefinition Stat(string id)
    {
        return new StatDefinition
        {
            Id = id,
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

    private sealed class CountingModifierCandidateResolver : IParsedItemModifierCandidateResolver
    {
        public int CallCount { get; private set; }

        public ItemBaseResolutionResult? LastBaseResolution { get; private set; }

        public IReadOnlyList<ModifierCandidateResolutionResult> Results { get; init; } = [];

        public IReadOnlyList<ModifierCandidateResolutionResult> Resolve(
            ParsedItem parsedItem,
            GameDataCatalog catalog,
            ItemBaseResolutionResult baseResolution)
        {
            CallCount++;
            LastBaseResolution = baseResolution;
            return Results;
        }
    }
}
