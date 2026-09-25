using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class BlightTowerEnchantProviderValidationTests
{
    private static readonly Lazy<GameDataCatalog> GameData = new(LoadGameData);
    private static readonly Lazy<PathOfExileTradeStatCatalog> OfficialTradeCatalog =
        new(LoadOfficialTradeCatalog);
    private static readonly PathOfExileTradeItemCatalog TradeItemCatalog = CreateTradeItemCatalog();
    private static readonly PathOfExileTradeFilterCatalog FilterCatalog =
        PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    private static readonly PathOfExileTradeSelectedModifierMapper SelectedMapper = new();

    [Theory]
    [InlineData("20260919-184022-181-Berek's Grip.json")]
    [InlineData("20260919-184430-728-Mokou's Embrace.json")]
    public async Task ReplayReadyShockNova_ProviderMapsExactTradeEnchantStat(string fileName)
    {
        var raw = await ReadCaptureClipboardAsync(fileName);
        var runtime = Resolve(raw, OfficialTradeCatalog.Value);
        var component = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            filter => filter.RawCopiedText.Contains("Shock Nova Towers", StringComparison.Ordinal));

        Assert.Equal(ParsedModifierKind.Enchantment, component.ParsedKind);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
        Assert.Equal("BlightEnchantmentShockNovaTowerDamage", component.ResolvedModifierId);
        Assert.Equal(["blight_shocking_tower_damage_+%"], component.ResolvedStatIds.ToArray());
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal("enchant.stat_2882048906", component.ProviderStatId);
        Assert.True(component.IsSearchable, component.NotSearchableReason);
        Assert.Equal(25m, component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);

        var selectedDraft = runtime.ProviderDraft with
        {
            ModifierFilters = runtime.ProviderDraft.ModifierFilters
                .Select(candidate => candidate with
                {
                    IsSelected = string.Equals(
                        candidate.ComponentId,
                        component.ComponentId,
                        StringComparison.Ordinal),
                })
                .ToArray(),
        };
        var mapping = SelectedMapper.Map(selectedDraft, OfficialTradeCatalog.Value);
        Assert.True(mapping.IsSuccess, string.Join(" | ", mapping.Diagnostics.Select(d => d.Message)));
        var filter = Assert.Single(mapping.Filters);
        Assert.Equal("enchant.stat_2882048906", filter.StatId);
        Assert.Equal(25m, filter.Minimum);
        Assert.Null(filter.Maximum);
    }

    private static RuntimeResult Resolve(string rawText, PathOfExileTradeStatCatalog tradeCatalog)
    {
        var parsed = new ItemTextParser().Parse(rawText);
        var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, GameData.Value);
        var sourceResolutions = new ParsedItemModifierCandidateResolver().Resolve(
            parsed,
            GameData.Value,
            baseResolution);
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
        return new RuntimeResult(providerDraft);
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

    private static PathOfExileTradeItemCatalog CreateTradeItemCatalog()
    {
        return new PathOfExileTradeItemCatalog(
        [
            new PathOfExileTradeItemEntry
            {
                ProviderOrder = 0,
                GroupId = "accessory",
                GroupLabel = "accessory",
                Name = "Berek's Grip",
                Type = "Two-Stone Ring",
                IsUnique = true,
            },
            new PathOfExileTradeItemEntry
            {
                ProviderOrder = 1,
                GroupId = "accessory",
                GroupLabel = "accessory",
                Name = "Mokou's Embrace",
                Type = "Ruby Ring",
                IsUnique = true,
            },
        ]);
    }

    private static GameDataCatalog LoadGameData()
    {
        var result = GameDataPackageLoader
            .LoadFromFileAsync(FindRepoFile("artifacts", "poenhance-game-data.json"))
            .GetAwaiter()
            .GetResult();
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("3.29.1.2.10-unique-component-composite-translation", result.Package!.Manifest.DataVersion);
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

    private static async Task<string> ReadCaptureClipboardAsync(string fileName)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "PoEnhance-A.5.20-ManualValidation",
            fileName);
        Assert.True(File.Exists(path), $"Missing ReplayReady capture: {path}");
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream);
        var raw = document.RootElement
            .GetProperty("replayContext")
            .GetProperty("rawClipboardText")
            .GetString();
        Assert.False(string.IsNullOrWhiteSpace(raw));
        return raw!;
    }

    private sealed record RuntimeResult(TradeSearchDraft ProviderDraft);

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
