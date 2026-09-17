using System.Reflection;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class ExactMultiStatImplicitProviderExpansionTests
{
    private static readonly MethodInfo InteractionReady = typeof(PriceCheckerSearchController)
        .GetMethod("IsModifierInteractionReady", BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly Lazy<GameDataCatalog> GameData = new(LoadGameData);
    private static readonly Lazy<PathOfExileTradeStatCatalog> OfficialTradeCatalog =
        new(LoadOfficialTradeCatalog);
    private static readonly PathOfExileTradeFilterCatalog FilterCatalog =
        PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    private static readonly PathOfExileTradeItemCatalog TradeItemCatalog = new([]);
    private static readonly PathOfExileTradeSelectedModifierMapper SelectedMapper = new();

    private const string GraspingClipboard = """
        Item Class: Tinctures
        Rarity: Unique
        Grasping Nightshade
        Sporebloom Tincture
        --------
        Item Level: 84
        --------
        { Implicit Modifier }
        25% chance to Blind Enemies on Hit with Melee Weapons
        27(25-35)% increased Effect of Blind from Melee Weapons
        --------
        { Unique Modifier }
        Melee Weapon Attacks apply Withered on Hit for 2 seconds
        { Unique Modifier }
        Melee Weapon Attacks have a chance to create Grasping Vines on Hit
        """;

    [Fact]
    public void GraspingNightshade_BlindImplicit_MapsBothOfficialImplicitTradeStats()
    {
        var providerDraft = ResolveProvider(GraspingClipboard);
        var blinds = providerDraft.ModifierFilters
            .Where(filter => filter.RawCopiedText.Contains("Blind", StringComparison.OrdinalIgnoreCase))
            .OrderBy(filter => filter.SourceLineIndex)
            .ToArray();

        Assert.Equal(2, blinds.Length);
        Assert.All(blinds, component =>
        {
            Assert.False(component.IsBaseImplicit);
            Assert.Equal(
                ParsedUniqueItemResolver.UniqueCatalogImplicitBlockConsumptionReason,
                component.UniqueCatalogImplicitConsumptionReason);
            Assert.True(component.IsSearchable, component.NotSearchableReason);
            Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
            Assert.True((bool)InteractionReady.Invoke(null, [component])!);
            Assert.StartsWith("implicit.", component.ProviderStatId);
        });

        var chance = Assert.Single(
            blinds,
            component => component.ResolvedStatIds.Contains(
                "chance_to_blind_on_hit_%_with_tinctured_weapons"));
        var effect = Assert.Single(
            blinds,
            component => component.ResolvedStatIds.Contains("blind_effect_+%_with_tinctured_weapons"));
        Assert.Equal("implicit.stat_2795267150", chance.ProviderStatId);
        Assert.Equal("implicit.stat_1808507379", effect.ProviderStatId);
        Assert.Equal(25m, chance.RequestedMinimum);
        Assert.Equal(27m, effect.RequestedMinimum);

        var selectedDraft = providerDraft with
        {
            ModifierFilters = providerDraft.ModifierFilters
                .Select(component => component with
                {
                    IsSelected = blinds.Any(blind =>
                        string.Equals(blind.ComponentId, component.ComponentId, StringComparison.Ordinal)),
                })
                .ToArray(),
        };
        var mapping = SelectedMapper.Map(selectedDraft, OfficialTradeCatalog.Value);
        Assert.True(mapping.IsSuccess, string.Join(" | ", mapping.Diagnostics.Select(d => d.Message)));
        Assert.Equal(2, mapping.Filters.Count);
        Assert.Contains(mapping.Filters, filter => filter.StatId == "implicit.stat_2795267150");
        Assert.Contains(mapping.Filters, filter => filter.StatId == "implicit.stat_1808507379");
        var chanceFilter = Assert.Single(
            mapping.Filters,
            filter => filter.StatId == "implicit.stat_2795267150");
        var effectFilter = Assert.Single(
            mapping.Filters,
            filter => filter.StatId == "implicit.stat_1808507379");
        Assert.Equal(25m, chanceFilter.Minimum);
        Assert.Null(chanceFilter.Maximum);
        Assert.Equal(27m, effectFilter.Minimum);
        Assert.Null(effectFilter.Maximum);
    }

    [Fact]
    public void MightbloodIre_StunImplicit_MapsBothOfficialImplicitTradeStatsWhenPresent()
    {
        var providerDraft = ResolveProvider(
            """
            Item Class: Tinctures
            Rarity: Unique
            Mightblood Ire
            Ironwood Tincture
            --------
            Item Level: 84
            --------
            { Implicit Modifier }
            40% reduced Enemy Stun Threshold with Melee Weapons
            20(15-25)% increased Stun Duration with Melee Weapons
            """);
        var implicits = providerDraft.ModifierFilters
            .Where(filter => filter.ParsedKind == ParsedModifierKind.Implicit)
            .ToArray();
        Assert.Equal(2, implicits.Length);
        Assert.All(implicits, component =>
        {
            Assert.False(component.IsBaseImplicit);
            Assert.True(component.IsSearchable, component.NotSearchableReason);
            Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
            Assert.True((bool)InteractionReady.Invoke(null, [component])!);
            Assert.StartsWith("implicit.", component.ProviderStatId);
        });
    }

    [Fact]
    public void WildfirePhloem_IgniteImplicit_MapsBothOfficialImplicitTradeStatsWhenPresent()
    {
        var providerDraft = ResolveProvider(
            """
            Item Class: Tinctures
            Rarity: Unique
            Wildfire Phloem
            Ashbark Tincture
            --------
            Item Level: 84
            --------
            { Implicit Modifier }
            25% chance to Ignite with Melee Weapons
            75(60-90)% increased Damage with Ignite from Melee Weapons
            """);
        var implicits = providerDraft.ModifierFilters
            .Where(filter => filter.ParsedKind == ParsedModifierKind.Implicit)
            .ToArray();
        Assert.Equal(2, implicits.Length);
        Assert.All(implicits, component =>
        {
            Assert.False(component.IsBaseImplicit);
            Assert.True(component.IsSearchable, component.NotSearchableReason);
            Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
            Assert.True((bool)InteractionReady.Invoke(null, [component])!);
            Assert.StartsWith("implicit.", component.ProviderStatId);
        });
    }

    private static TradeSearchDraft ResolveProvider(string clipboard)
    {
        var parsed = new ItemTextParser().Parse(clipboard);
        var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, GameData.Value);
        var sourceResolutions = new ParsedItemModifierCandidateResolver()
            .Resolve(parsed, GameData.Value, baseResolution);
        var draftResult = new TradeSearchDraftMapper().CreateDraft(
            parsed, baseResolution, sourceResolutions, GameData.Value);
        var draft = Assert.IsType<TradeSearchDraft>(draftResult.Draft);
        var identity = new PathOfExileTradeItemIdentityMapper()
            .Map(draft, TradeItemCatalog)
            .Identity;
        var propertyDraft = new PathOfExileTradeItemPropertyResolver()
            .Resolve(draft, FilterCatalog);
        return CreatePriceCheckService(OfficialTradeCatalog.Value).ResolveProviderComponents(
            propertyDraft,
            OfficialTradeCatalog.Value,
            identity,
            FilterCatalog);
    }

    private static PathOfExileTradePriceCheckService CreatePriceCheckService(
        PathOfExileTradeStatCatalog catalog) =>
        new(
            new PathOfExileTradeQueryBuilder(),
            new PathOfExileTradeStatMatcher(),
            new StaticStatProvider(catalog),
            new StaticItemProvider(TradeItemCatalog),
            SelectedMapper,
            new PathOfExileTradeItemIdentityMapper(),
            new NoSearchClient(),
            new NoFetchClient());

    private static GameDataCatalog LoadGameData()
    {
        var path = FindRepoFile("artifacts", "poenhance-game-data.json");
        var result = GameDataPackageLoader.LoadFromFileAsync(path).GetAwaiter().GetResult();
        Assert.True(result.IsSuccess);
        return GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(result.Package));
    }

    private static PathOfExileTradeStatCatalog LoadOfficialTradeCatalog()
    {
        var path = FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Trade",
            "official-stats-2026-08-19.json");
        var result = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(File.ReadAllText(path));
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<PathOfExileTradeStatCatalog>(result.Catalog);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(relativeParts)}");
    }

    private sealed class StaticStatProvider(PathOfExileTradeStatCatalog catalog) :
        IPathOfExileTradeStatCatalogProvider
    {
        public Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PathOfExileTradeStatCatalogProviderResult.Success(catalog));
    }

    private sealed class StaticItemProvider(PathOfExileTradeItemCatalog catalog) :
        IPathOfExileTradeItemCatalogProvider
    {
        public Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PathOfExileTradeItemCatalogProviderResult.Success(catalog));
    }

    private sealed class NoSearchClient : IPathOfExileTradeSearchClient
    {
        public Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
            PathOfExileTradeSearchRequest? request,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PathOfExileTradeSearchExecutionResult());
    }

    private sealed class NoFetchClient : IPathOfExileTradeFetchClient
    {
        public Task<PathOfExileTradeFetchExecutionResult> FetchAsync(
            string? queryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PathOfExileTradeFetchExecutionResult());
    }
}
