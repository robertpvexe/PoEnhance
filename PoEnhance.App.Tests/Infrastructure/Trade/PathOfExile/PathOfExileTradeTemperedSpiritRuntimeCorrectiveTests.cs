using System.Reflection;
using System.Text.Json;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// TRADE.4e.1 / TRADE.4e.3 — real Ctrl+D Tempered Spirit: signed→unsigned Exact must also satisfy
/// interaction-ready (CanonicalNumericValues populated with the signed Trade query scalar).
/// </summary>
public sealed class PathOfExileTradeTemperedSpiritRuntimeCorrectiveTests
{
    private static readonly Lazy<GameDataCatalog> GameData = new(LoadGameData);
    private static readonly Lazy<PathOfExileTradeStatCatalog> OfficialTradeCatalog =
        new(LoadOfficialTradeCatalog);
    private static readonly PathOfExileTradeItemCatalog TradeItemCatalog = CreateTradeItemCatalog();
    private static readonly PathOfExileTradeFilterCatalog FilterCatalog =
        PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    private static readonly PathOfExileTradeSelectedModifierMapper SelectedMapper = new();

    private const string DexterityLine =
        "-1 Dexterity per 1 Dexterity on Allocated Passives in Radius";

    private const string MovementLine =
        "2% increased Movement Speed per 10 Dexterity on Allocated Passives in Radius";

    [Fact]
    public void Resolve_TemperedSpirit_RealClipboard_SignedUnsignedExactAndInteractionReady()
    {
        var rawText = LoadTemperedClipboard();
        Assert.Contains(DexterityLine, rawText, StringComparison.Ordinal);
        Assert.Contains(MovementLine, rawText, StringComparison.Ordinal);

        var runtime = Resolve(rawText, OfficialTradeCatalog.Value);
        var dexterity = FindComponent(runtime.ProviderDraft, DexterityLine);
        var movement = FindComponent(runtime.ProviderDraft, "Movement Speed");

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, dexterity.ResolutionStatus);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, dexterity.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_172076472", dexterity.ProviderStatId);
        Assert.Equal(
            "# Dexterity per 1 Dexterity on Allocated Passives in Radius",
            dexterity.ProviderStatText);
        Assert.Contains(" per 1 ", dexterity.ProviderStatText!, StringComparison.Ordinal);
        Assert.Equal(1, PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(
            dexterity.ProviderStatText!));

        Assert.Equal(ModifierBoundShape.Scalar, dexterity.ValueBoundShape);
        Assert.True(dexterity.SupportsValueBounds);
        Assert.Equal([-1m, 1m], dexterity.ObservedNumericValues);
        Assert.Equal([-1m], dexterity.CanonicalNumericValues);
        Assert.Equal(-1m, dexterity.RequestedMinimum);
        Assert.Equal(-1m, dexterity.RequestedMaximum);

        Assert.True(
            IsModifierInteractionReady(dexterity),
            "Signed→unsigned Exact must populate CanonicalNumericValues so UI is interaction-ready.");
        Assert.True(IsModifierInteractionReady(movement), "Movement Speed sibling must remain ready.");

        var filter = MapSingle(runtime.ProviderDraft, dexterity, OfficialTradeCatalog.Value);
        Assert.Equal("explicit.stat_172076472", filter.StatId);
        Assert.Equal(-1m, filter.Minimum);
        Assert.Equal(-1m, filter.Maximum);
    }

    [Fact]
    public void Resolve_HungryLoop_RealClipboard_RemainsExactMinMax4()
    {
        var rawText = LoadHungryClipboard();
        Assert.Contains("Can Consume 4 Uncorrupted Support Gems", rawText, StringComparison.Ordinal);

        var runtime = Resolve(rawText, OfficialTradeCatalog.Value);
        var hungry = FindComponent(runtime.ProviderDraft, "Can Consume 4 Uncorrupted Support Gems");

        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, hungry.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_3221550523", hungry.ProviderStatId);
        Assert.Equal(4m, hungry.RequestedMinimum);
        Assert.Equal(4m, hungry.RequestedMaximum);
        Assert.DoesNotContain("Has not Consumed", hungry.ProviderStatText, StringComparison.Ordinal);
        Assert.True(IsModifierInteractionReady(hungry));
    }

    private static bool IsModifierInteractionReady(ResolvedSearchComponent component)
    {
        var method = typeof(PriceCheckerSearchController).GetMethod(
            "IsModifierInteractionReady",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (bool)method!.Invoke(null, [component])!;
    }

    private static string LoadTemperedClipboard()
    {
        return PathOfExileTradeProviderRuntimeCaptureFixtures.ReadClipboard(
            PathOfExileTradeProviderRuntimeCaptureFixtures.PreferCapture("Tempered Spirit"));
    }

    private static string LoadHungryClipboard()
    {
        return PathOfExileTradeProviderRuntimeCaptureFixtures.ReadClipboard(
            PathOfExileTradeProviderRuntimeCaptureFixtures.PreferCapture("The Hungry Loop"));
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

    private static PathOfExileTradeSelectedModifierFilter MapSingle(
        TradeSearchDraft draft,
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog)
    {
        var selectedDraft = draft with
        {
            ModifierFilters = draft.ModifierFilters
                .Select(entry => entry with
                {
                    IsSelected = entry.ComponentId == component.ComponentId,
                })
                .ToArray(),
        };
        var mapping = SelectedMapper.Map(selectedDraft, catalog);
        Assert.True(mapping.IsSuccess, string.Join(" | ", mapping.Diagnostics.Select(d => d.Message)));
        return Assert.Single(mapping.Filters);
    }

    private static ResolvedSearchComponent FindComponent(TradeSearchDraft draft, string textFragment) =>
        Assert.Single(
            draft.ModifierFilters,
            component => component.OriginalText.Contains(textFragment, StringComparison.Ordinal) ||
                component.CanonicalSignature.Contains(textFragment, StringComparison.Ordinal) ||
                component.RawCopiedText.Contains(textFragment, StringComparison.Ordinal));

    private static PathOfExileTradePriceCheckService CreatePriceCheckService(
        PathOfExileTradeStatCatalog tradeCatalog) =>
        new(
            new PathOfExileTradeQueryBuilder(),
            new PathOfExileTradeStatMatcher(),
            new StaticStatProvider(tradeCatalog),
            new StaticItemProvider(TradeItemCatalog),
            SelectedMapper,
            new PathOfExileTradeItemIdentityMapper(),
            new NoSearchClient(),
            new NoFetchClient(),
            gameDataCatalogProvider: static () => GameData.Value);

    private static PathOfExileTradeItemCatalog CreateTradeItemCatalog() =>
        new(
        [
            new PathOfExileTradeItemEntry
            {
                ProviderOrder = 0,
                GroupId = "unique",
                GroupLabel = "Unique",
                Name = "Tempered Spirit",
                Type = "Viridian Jewel",
                IsUnique = true,
            },
            new PathOfExileTradeItemEntry
            {
                ProviderOrder = 1,
                GroupId = "unique",
                GroupLabel = "Unique",
                Name = "The Hungry Loop",
                Type = "Unset Ring",
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
        return GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(result.Package));
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
