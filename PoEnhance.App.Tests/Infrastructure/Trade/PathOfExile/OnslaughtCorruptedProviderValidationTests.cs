using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class OnslaughtCorruptedProviderValidationTests
{
    private static readonly Lazy<GameDataCatalog> GameData = new(LoadGameData);
    private static readonly Lazy<PathOfExileTradeStatCatalog> OfficialTradeCatalog =
        new(LoadOfficialTradeCatalog);
    private static readonly PathOfExileTradeItemCatalog TradeItemCatalog = CreateTradeItemCatalog();
    private static readonly PathOfExileTradeFilterCatalog FilterCatalog =
        PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    private static readonly PathOfExileTradeSelectedModifierMapper SelectedMapper = new();

    [Fact]
    public async Task RealMjolnerOnslaught_ProviderMapsExactTradeImplicitWithoutPhysDamageLeakage()
    {
        var raw = await ReadCaptureClipboardAsync();
        var runtime = Resolve(raw, OfficialTradeCatalog.Value);
        var component = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            filter => filter.RawCopiedText.Contains("Onslaught", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(ParsedModifierKind.Implicit, component.ParsedKind);
        Assert.Equal(ParsedImplicitModifierOrigin.Corrupted, component.ImplicitOrigin);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
        Assert.Equal("V2ChanceToGainOnslaughtOnKillCorrupted_", component.ResolvedModifierId);
        Assert.Equal(["chance_to_gain_onslaught_on_kill_%"], component.ResolvedStatIds.ToArray());
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal("implicit.stat_3023957681", component.ProviderStatId);
        Assert.True(component.IsSearchable, component.NotSearchableReason);
        Assert.Equal(13m, component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);
        Assert.DoesNotContain(
            runtime.ProviderDraft.ModifierFilters,
            filter => filter.ResolvedModifierId == "V2LocalIncreasedPhysicalDamageCorrupted1");

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
        Assert.Equal(component.ProviderStatId, filter.StatId);
        Assert.Equal(13m, filter.Minimum);
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
                GroupId = "weapon",
                GroupLabel = "weapon",
                Name = "Mjölner",
                Type = "Gavel",
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

    private static async Task<string> ReadCaptureClipboardAsync()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "PoEnhance-A.5.32-Corrupted-Onslaught-Capture");
        var path = Directory.EnumerateFiles(directory, "*.json")
            .First(file =>
                Path.GetFileName(file).Contains("103002", StringComparison.Ordinal) &&
                !Path.GetFileName(file).Contains("search", StringComparison.OrdinalIgnoreCase) &&
                !Path.GetFileName(file).Contains("request", StringComparison.OrdinalIgnoreCase));
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
