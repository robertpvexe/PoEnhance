using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// A.5.48 — Pure Talent Exact component-composite maps to ONE official Trade composite stat.
/// </summary>
public sealed class PureTalentComponentCompositeProviderValidationTests
{
    private static readonly Lazy<GameDataCatalog> GameData = new(LoadGameData);
    private static readonly Lazy<PathOfExileTradeStatCatalog> OfficialTradeCatalog =
        new(LoadOfficialTradeCatalog);
    private static readonly PathOfExileTradeItemCatalog TradeItemCatalog = CreateTradeItemCatalog();
    private static readonly PathOfExileTradeFilterCatalog FilterCatalog =
        PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    private static readonly PathOfExileTradeSelectedModifierMapper SelectedMapper = new();

    private const string PureTalentClipboard = """
        Item Class: Jewels
        Rarity: Unique
        Pure Talent
        Viridian Jewel
        --------
        Item Level: 84
        --------
        { Unique Modifier }
        While your Passive Skill Tree connects to a class' starting location, you gain:
        Marauder: Melee Skills have 25% increased Area of Effect
        Duelist: 1% of Attack Damage Leeched as Life
        Ranger: 7% increased Movement Speed
        Shadow: +0.5% to Critical Strike Chance
        Witch: 0.5% of Mana Regenerated per second
        Templar: Damage Penetrates 5% Elemental Resistances
        Scion: +25 to All Attributes
        """;

    [Fact]
    public void PureTalentComposite_ProviderSelectsSingleOfficialTradeStat()
    {
        var runtime = Resolve(PureTalentClipboard, OfficialTradeCatalog.Value);
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, runtime.Unique.Status);

        var component = Assert.Single(runtime.ProviderDraft.ModifierFilters);
        Assert.True(component.HasExactUniqueSourceProvenance);
        Assert.Null(component.ResolvedModifierId);
        Assert.Equal(7, component.ResolvedStatIds.Count);
        Assert.Equal(
            SearchComponentProviderResolutionStatus.Exact,
            component.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_769192511", component.ProviderStatId);
        Assert.True(component.IsSearchable, component.NotSearchableReason);
        Assert.NotEqual(
            SearchComponentProviderResolutionStatus.ExactConjunctiveSet,
            component.ProviderResolutionStatus);
        Assert.Equal(1, runtime.ProviderDraft.ModifierFilters.Count);

        var matcherSource = File.ReadAllText(
            FindRepoFile(
                "PoEnhance.App",
                "Infrastructure",
                "Trade",
                "PathOfExile",
                "PathOfExileTradeStatMatcher.cs"));
        Assert.DoesNotContain("769192511", matcherSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Pure Talent", matcherSource, StringComparison.Ordinal);
    }

    private static RuntimeResult Resolve(string rawText, PathOfExileTradeStatCatalog tradeCatalog)
    {
        var parsed = new ItemTextParser().Parse(rawText);
        var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, GameData.Value);
        var sourceResolutions = new ParsedItemModifierCandidateResolver().Resolve(
            parsed,
            GameData.Value,
            baseResolution);
        var unique = new ParsedUniqueItemResolver().Resolve(parsed, GameData.Value, baseResolution);
        var draftResult = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            baseResolution,
            sourceResolutions,
            GameData.Value);
        var draft = Assert.IsType<TradeSearchDraft>(draftResult.Draft);
        var uniqueIdentity = new PathOfExileTradeItemIdentityMapper()
            .Map(draft, TradeItemCatalog)
            .Identity;
        var propertyDraft = new PathOfExileTradeItemPropertyResolver().Resolve(draft, FilterCatalog);
        var providerDraft = CreatePriceCheckService(tradeCatalog).ResolveProviderComponents(
            propertyDraft,
            tradeCatalog,
            uniqueIdentity,
            FilterCatalog);
        return new RuntimeResult(providerDraft, unique);
    }

    private static PathOfExileTradePriceCheckService CreatePriceCheckService(
        PathOfExileTradeStatCatalog tradeCatalog)
    {
        return new PathOfExileTradePriceCheckService(
            new PathOfExileTradeQueryBuilder(),
            new PathOfExileTradeStatMatcher(),
            new StaticStatProvider(tradeCatalog),
            new StaticItemProvider(TradeItemCatalog),
            SelectedMapper,
            new PathOfExileTradeItemIdentityMapper(),
            new NoSearchClient(),
            new NoFetchClient());
    }

    private static PathOfExileTradeItemCatalog CreateTradeItemCatalog() =>
        new(
        [
            new PathOfExileTradeItemEntry
            {
                ProviderOrder = 0,
                GroupId = "jewel",
                GroupLabel = "jewel",
                Name = "Pure Talent",
                Type = "Viridian Jewel",
                IsUnique = true,
            },
        ]);

    private static GameDataCatalog LoadGameData()
    {
        var result = GameDataPackageLoader
            .LoadFromFileAsync(FindRepoFile("artifacts", "poenhance-game-data.json"))
            .GetAwaiter()
            .GetResult();
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            "3.29.1.2.10-unique-component-composite-translation",
            result.Package!.Manifest.DataVersion);
        return GameDataCatalog.FromPackage(result.Package);
    }

    private static PathOfExileTradeStatCatalog LoadOfficialTradeCatalog()
    {
        var path = FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Trade",
            "official-stats-2026-08-19.json");
        var parsed = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(File.ReadAllText(path));
        Assert.True(parsed.IsSuccess);
        return Assert.IsType<PathOfExileTradeStatCatalog>(parsed.Catalog);
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

    private sealed record RuntimeResult(
        TradeSearchDraft ProviderDraft,
        UniqueItemResolutionResult Unique);

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
