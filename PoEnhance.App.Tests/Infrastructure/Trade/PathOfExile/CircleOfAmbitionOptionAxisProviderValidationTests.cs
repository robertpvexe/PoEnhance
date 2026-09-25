using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class CircleOfAmbitionOptionAxisProviderValidationTests
{
    private static readonly Lazy<GameDataCatalog> GameData = new(LoadGameData);
    private static readonly Lazy<PathOfExileTradeStatCatalog> OfficialTradeCatalog =
        new(LoadOfficialTradeCatalog);
    private static readonly PathOfExileTradeItemCatalog TradeItemCatalog = CreateTradeItemCatalog();
    private static readonly PathOfExileTradeFilterCatalog FilterCatalog =
        PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    private static readonly PathOfExileTradeSelectedModifierMapper SelectedMapper = new();

    [Fact]
    public void FreshCircleThreeHeraldChoices_ProviderMapsExactTradeExplicitStats()
    {
        var capture = Path.Combine(
            Path.GetTempPath(),
            "PoEnhance-A.5.36-Unique-NoModId-Capture",
            "20260924-093744-936-Circle of Ambition.json");
        Assert.True(File.Exists(capture), $"Missing A.5.36 Circle capture: {capture}");
        using var document = JsonDocument.Parse(File.ReadAllText(capture));
        var raw = document.RootElement
            .GetProperty("replayContext")
            .GetProperty("rawClipboardText")
            .GetString();
        Assert.False(string.IsNullOrWhiteSpace(raw));

        var runtime = Resolve(raw!, OfficialTradeCatalog.Value);
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, runtime.Unique.Status);

        var expected = new (string Needle, string ModId, string StatId, string TradeId)[]
        {
            (
                "Lightning Damage while affected by Herald of Thunder",
                "HeraldBonusThunderLightningDamage",
                "lightning_damage_+%_while_affected_by_herald_of_thunder",
                "explicit.stat_536957"),
            (
                "Herald of Ash has",
                "HeraldBonusAshReservationEfficiency__",
                "herald_of_ash_mana_reservation_efficiency_+%",
                "explicit.stat_2500442851"),
            (
                "Herald of Purity has",
                "HeraldBonusPurityEffect",
                "herald_of_light_buff_effect_+%",
                "explicit.stat_2126027382"),
        };

        foreach (var (needle, modId, statId, tradeId) in expected)
        {
            var component = Assert.Single(
                runtime.ProviderDraft.ModifierFilters,
                filter => filter.RawCopiedText.Contains(needle, StringComparison.OrdinalIgnoreCase));
            Assert.True(component.HasExactUniqueSourceProvenance);
            Assert.Equal(modId, component.ResolvedModifierId);
            Assert.Equal([statId], component.ResolvedStatIds.ToArray());
            Assert.True(
                component.ProviderResolutionStatus is
                    SearchComponentProviderResolutionStatus.Exact or
                    SearchComponentProviderResolutionStatus.ExactEquivalentSet,
                $"Unexpected provider status {component.ProviderResolutionStatus} for {needle}");
            if (!string.IsNullOrWhiteSpace(component.ProviderStatId))
            {
                Assert.Equal(tradeId, component.ProviderStatId);
            }
            else
            {
                // ExactEquivalentSet may omit a single ProviderStatId while still carrying Exact
                // Unique provenance and searchable Core StatIds for Trade mapping.
                Assert.Equal(
                    SearchComponentProviderResolutionStatus.ExactEquivalentSet,
                    component.ProviderResolutionStatus);
            }
            Assert.True(component.IsSearchable, component.NotSearchableReason);
        }
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
                GroupId = "ring",
                GroupLabel = "ring",
                Name = "Circle of Ambition",
                Type = "Prismatic Ring",
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
        Assert.Equal("3.29.1.2.9-unique-newline-translation-fidelity", result.Package!.Manifest.DataVersion);
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
