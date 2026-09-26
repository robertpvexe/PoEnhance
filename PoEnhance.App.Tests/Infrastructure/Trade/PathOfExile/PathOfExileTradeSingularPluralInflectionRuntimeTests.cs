using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// TRADE.4c.1 — real Ctrl+D Steelworm payload: provider Exact + query slot 3 (not embedded 4/8).
/// </summary>
public sealed class PathOfExileTradeSingularPluralInflectionRuntimeTests
{
    private static readonly Lazy<GameDataCatalog> GameData = new(LoadGameData);
    private static readonly Lazy<PathOfExileTradeStatCatalog> OfficialTradeCatalog =
        new(LoadOfficialTradeCatalog);
    private static readonly PathOfExileTradeItemCatalog TradeItemCatalog = CreateTradeItemCatalog();
    private static readonly PathOfExileTradeFilterCatalog FilterCatalog =
        PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    private static readonly PathOfExileTradeSelectedModifierMapper SelectedMapper = new();

    private const string SteelwormProjectileFragment =
        "Skills Fire 3 additional Projectiles for 4 seconds after";

    [Fact]
    public void Resolve_Steelworm_RealClipboard_ProjectsProjectileCountNotEmbeddedLiterals()
    {
        var rawText = LoadSteelwormClipboard();
        Assert.Contains(SteelwormProjectileFragment, rawText, StringComparison.Ordinal);
        Assert.Contains("8 Steel Shards", rawText, StringComparison.Ordinal);

        var runtime = Resolve(rawText, OfficialTradeCatalog.Value);
        var component = FindComponent(runtime.ProviderDraft, SteelwormProjectileFragment);

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_1031404836", component.ProviderStatId);
        Assert.Equal(
            "Skills Fire # additional Projectile for 4 seconds after\nyou consume a total of 8 Steel Shards",
            component.ProviderStatText);
        Assert.Equal(ModifierBoundShape.Scalar, component.ValueBoundShape);
        Assert.Equal(3m, component.RequestedMinimum);
        Assert.Equal(3m, component.RequestedMaximum);
        Assert.True(component.SupportsValueBounds);
        Assert.NotEqual(4m, component.RequestedMinimum);
        Assert.NotEqual(8m, component.RequestedMinimum);
        Assert.NotEqual(4m, component.RequestedMaximum);
        Assert.NotEqual(8m, component.RequestedMaximum);
        Assert.Contains("4 seconds", component.ProviderStatText, StringComparison.Ordinal);
        Assert.Contains("8 Steel Shards", component.ProviderStatText, StringComparison.Ordinal);

        var filter = MapSingle(runtime.ProviderDraft, component, OfficialTradeCatalog.Value);
        Assert.Equal("explicit.stat_1031404836", filter.StatId);
        Assert.Equal(3m, filter.Minimum);
        Assert.Equal(3m, filter.Maximum);
    }

    private static string LoadSteelwormClipboard()
    {
        var capturePath = PreferManualCapture("Steelworm");
        if (File.Exists(capturePath))
        {
            return ReadClipboard(capturePath);
        }

        // Embedded real Ctrl+D clipboard from TRADE.4c manual validation capture
        // 20260926-124431-943-Steelworm.json (kept when TEMP capture is absent).
        return """
            Item Class: Quivers
            Rarity: Unique
            Steelworm
            Broadhead Arrow Quiver
            --------
            Requirements:
            Level: 52
            --------
            Item Level: 55
            --------
            { Implicit Modifier — Attack, Speed }
            8(8-10)% increased Attack Speed
            --------
            { Unique Modifier }
            Grants Call of Steel — Unscalable Value
            { Unique Modifier — Defences, Armour, Evasion }
            35(30-60)% increased Evasion Rating and Armour
            { Unique Modifier — Damage }
            Deal no Non-Physical Damage — Unscalable Value
            { Unique Modifier — Attack }
            Attacks that Fire Projectiles Consume up to 1 additional Steel Shard
            { Unique Modifier — Attack }
            Skills Fire 3 additional Projectiles for 4 seconds after
            you consume a total of 8 Steel Shards
            --------
            The dance of metal and flesh never ends.
            """;
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

    private static ResolvedSearchComponent FindComponent(TradeSearchDraft draft, string textFragment)
    {
        return Assert.Single(
            draft.ModifierFilters,
            component => component.OriginalText.Contains(textFragment, StringComparison.Ordinal) ||
                component.CanonicalSignature.Contains(textFragment, StringComparison.Ordinal) ||
                component.RawCopiedText.Contains(textFragment, StringComparison.Ordinal));
    }

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

    private static string PreferManualCapture(string itemName)
    {
        var root = Path.Combine(Path.GetTempPath(), "PoEnhance-TRADE.4c-ManualValidation");
        if (!Directory.Exists(root))
        {
            return Path.Combine(root, $"missing-{itemName}.json");
        }

        return Directory.EnumerateFiles(root, $"*{itemName}*.json", SearchOption.TopDirectoryOnly)
            .Where(path => !path.Contains("-search-", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault()
            ?? Path.Combine(root, $"missing-{itemName}.json");
    }

    private static string ReadClipboard(string capturePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(capturePath));
        var root = document.RootElement;
        if (root.TryGetProperty("replayContext", out var replay) &&
            replay.TryGetProperty("rawClipboardText", out var clipboard))
        {
            return clipboard.GetString() ?? string.Empty;
        }

        throw new InvalidOperationException($"Capture missing clipboard text: {capturePath}");
    }

    private static PathOfExileTradeItemCatalog CreateTradeItemCatalog() =>
        new(
        [
            new PathOfExileTradeItemEntry
            {
                ProviderOrder = 0,
                GroupId = "unique",
                GroupLabel = "Unique",
                Name = "Steelworm",
                Type = "Broadhead Arrow Quiver",
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
