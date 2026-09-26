using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// TRADE.4c.2 — Chains of Command melee-splash pair: zero-slot presence + #% less positive magnitude.
/// </summary>
public sealed class PathOfExileTradeChainsSplashBoundAlignmentRuntimeTests
{
    private static readonly Lazy<GameDataCatalog> GameData = new(LoadGameData);
    private static readonly Lazy<PathOfExileTradeStatCatalog> OfficialTradeCatalog =
        new(LoadOfficialTradeCatalog);
    private static readonly PathOfExileTradeItemCatalog TradeItemCatalog = CreateTradeItemCatalog();
    private static readonly PathOfExileTradeFilterCatalog FilterCatalog =
        PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    private static readonly PathOfExileTradeSelectedModifierMapper SelectedMapper = new();

    private const string SplashPresenceFragment =
        "Animated and Manifested Minions' Melee Strikes deal Splash";
    private const string SplashLessFragment =
        "Animated and Manifested Minions' Melee Strikes deal 50% less Splash Damage";

    [Fact]
    public void Resolve_ChainsSplashPair_ZeroSlotPresenceAndPositiveLessMagnitude()
    {
        var rawText = LoadChainsClipboard();
        Assert.Contains(SplashPresenceFragment, rawText, StringComparison.Ordinal);
        Assert.Contains(SplashLessFragment, rawText, StringComparison.Ordinal);

        var runtime = Resolve(rawText, OfficialTradeCatalog.Value);
        var presence = FindComponent(runtime.ProviderDraft, SplashPresenceFragment);
        var less = FindComponent(runtime.ProviderDraft, "50% less Splash Damage");

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, presence.ResolutionStatus);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, presence.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_91242932", presence.ProviderStatId);
        Assert.Equal(0, PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(
            presence.ProviderStatText!));
        Assert.Equal(ModifierBoundShape.PresenceOnly, presence.ValueBoundShape);
        Assert.Null(presence.RequestedMinimum);
        Assert.Null(presence.RequestedMaximum);
        Assert.False(presence.SupportsValueBounds);

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, less.ResolutionStatus);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, less.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_478698670", less.ProviderStatId);
        Assert.Equal(
            "Animated and Manifested Minions' Melee Strikes deal #% less Splash Damage",
            less.ProviderStatText);
        Assert.Equal(1, PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(
            less.ProviderStatText!));
        Assert.Equal(ModifierBoundShape.Scalar, less.ValueBoundShape);
        Assert.Equal(50m, less.RequestedMinimum);
        Assert.Null(less.RequestedMaximum);
        Assert.True(less.SupportsValueBounds);
        Assert.NotEqual(-50m, less.RequestedMinimum);
        Assert.NotEqual(-50m, less.RequestedMaximum);

        var presenceFilter = MapSingle(runtime.ProviderDraft, presence, OfficialTradeCatalog.Value);
        Assert.Equal("explicit.stat_91242932", presenceFilter.StatId);
        Assert.Null(presenceFilter.Minimum);
        Assert.Null(presenceFilter.Maximum);

        var lessFilter = MapSingle(runtime.ProviderDraft, less, OfficialTradeCatalog.Value);
        Assert.Equal("explicit.stat_478698670", lessFilter.StatId);
        Assert.Equal(50m, lessFilter.Minimum);
        Assert.Null(lessFilter.Maximum);

        // Selecting both together must keep identities and not leak -50 onto the zero-slot filter.
        var both = MapSelected(
            runtime.ProviderDraft,
            [presence.ComponentId, less.ComponentId],
            OfficialTradeCatalog.Value);
        Assert.Equal(2, both.Count);
        var byId = both.ToDictionary(filter => filter.StatId, StringComparer.Ordinal);
        Assert.Null(byId["explicit.stat_91242932"].Minimum);
        Assert.Null(byId["explicit.stat_91242932"].Maximum);
        Assert.Equal(50m, byId["explicit.stat_478698670"].Minimum);
        Assert.Null(byId["explicit.stat_478698670"].Maximum);
    }

    [Fact]
    public void Resolve_ChainsSplashPair_SelectionOrderDoesNotSwapBounds()
    {
        var runtime = Resolve(LoadChainsClipboard(), OfficialTradeCatalog.Value);
        var presence = FindComponent(runtime.ProviderDraft, SplashPresenceFragment);
        var less = FindComponent(runtime.ProviderDraft, "50% less Splash Damage");

        var forward = MapSelected(
            runtime.ProviderDraft,
            [presence.ComponentId, less.ComponentId],
            OfficialTradeCatalog.Value);
        var reverse = MapSelected(
            runtime.ProviderDraft,
            [less.ComponentId, presence.ComponentId],
            OfficialTradeCatalog.Value);

        Assert.Equal(
            forward.Select(filter => (filter.StatId, filter.Minimum, filter.Maximum)).OrderBy(x => x.StatId),
            reverse.Select(filter => (filter.StatId, filter.Minimum, filter.Maximum)).OrderBy(x => x.StatId));
    }

    private static string LoadChainsClipboard()
    {
        var capturePath = PreferManualCapture("Chains of Command");
        if (File.Exists(capturePath))
        {
            return ReadClipboard(capturePath);
        }

        return """
            Item Class: Body Armours
            Rarity: Unique
            Chains of Command
            Saintly Chainmail
            --------
            Armour: 1035 (augmented)
            Energy Shield: 248 (augmented)
            --------
            Requirements:
            Level: 70
            Str: 99
            Int: 115
            --------
            Sockets: W W R 
            --------
            Item Level: 84
            --------
            { Unique Modifier }
            Trigger Level 20 Animate Guardian's Weapon when Animated Guardian Kills an Enemy — Unscalable Value
            { Unique Modifier — Minion }
            You cannot have Non-Animated, Non-Manifested Minions — Unscalable Value
            { Unique Modifier — Minion }
            Animated Guardian deals 5% increased Damage per Animated Weapon
            { Unique Modifier — Minion }
            Animated and Manifested Minions' Melee Strikes deal Splash
            Damage to surrounding targets — Unscalable Value
            { Unique Modifier — Minion }
            Animated and Manifested Minions' Melee Strikes deal 50% less Splash Damage
            { Unique Modifier — Defences, Armour, Energy Shield }
            158(150-190)% increased Armour and Energy Shield
            { Unique Modifier — Life }
            +80(60-90) to maximum Life
            { Unique Modifier }
            10% chance to Trigger Level 18 Animate Guardian's Weapon when Animated Weapon Kills an Enemy — Unscalable Value
            --------
            A general may carry his men to greatness, 
            or be dragged beneath the mire by their burden.
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
        PathOfExileTradeStatCatalog catalog) =>
        Assert.Single(MapSelected(draft, [component.ComponentId], catalog));

    private static IReadOnlyList<PathOfExileTradeSelectedModifierFilter> MapSelected(
        TradeSearchDraft draft,
        IReadOnlyList<string> componentIds,
        PathOfExileTradeStatCatalog catalog)
    {
        var selected = new HashSet<string>(componentIds, StringComparer.Ordinal);
        var selectedDraft = draft with
        {
            ModifierFilters = draft.ModifierFilters
                .Select(entry => entry with
                {
                    IsSelected = selected.Contains(entry.ComponentId),
                })
                .ToArray(),
        };
        var mapping = SelectedMapper.Map(selectedDraft, catalog);
        Assert.True(mapping.IsSuccess, string.Join(" | ", mapping.Diagnostics.Select(d => d.Message)));
        return mapping.Filters;
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
        foreach (var rootName in new[]
                 {
                     "PoEnhance-TRADE.4c.1-ManualValidation",
                     "PoEnhance-TRADE.4c-ManualValidation",
                     "PoEnhance-TRADE.4a-ManualValidation",
                 })
        {
            var root = Path.Combine(Path.GetTempPath(), rootName);
            if (!Directory.Exists(root))
            {
                continue;
            }

            var match = Directory.EnumerateFiles(root, $"*{itemName}*.json", SearchOption.TopDirectoryOnly)
                .Where(path => !path.Contains("-search-", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (match is not null)
            {
                return match;
            }
        }

        return Path.Combine(Path.GetTempPath(), "missing-Chains of Command.json");
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
                Name = "Chains of Command",
                Type = "Saintly Chainmail",
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
