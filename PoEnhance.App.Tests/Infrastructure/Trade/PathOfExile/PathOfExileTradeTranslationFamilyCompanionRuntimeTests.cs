using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// TRADE.2.2 Track A — five real Unique phrase→numeric companion regressions via full
/// Core→provider path (no item/provider-id production hardcodes).
/// </summary>
public sealed class PathOfExileTradeTranslationFamilyCompanionRuntimeTests
{
    private static readonly Lazy<GameDataCatalog> GameData = new(LoadGameData);
    private static readonly Lazy<PathOfExileTradeStatCatalog> OfficialTradeCatalog =
        new(LoadOfficialTradeCatalog);
    private static readonly PathOfExileTradeItemCatalog TradeItemCatalog = CreateTradeItemCatalog();
    private static readonly PathOfExileTradeFilterCatalog FilterCatalog =
        PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    private static readonly PathOfExileTradeSelectedModifierMapper SelectedMapper = new();

    public static TheoryData<string> TrackAItemNames =>
    [
        "Cospri's Malice",
        "Fidelitas' Spike",
        "Oro's Sacrifice",
        "Nuro's Harp",
        "Storm Cloud",
    ];

    [Theory]
    [MemberData(nameof(TrackAItemNames))]
    public void Resolve_NoPhysicalDamage_MapsIncreasedPhysicalDamagePresenceOnly(string itemName)
    {
        var capturePath = PreferCapture(itemName);
        Assert.True(File.Exists(capturePath), $"Missing capture for {itemName}: {capturePath}");
        var rawText = ReadClipboard(capturePath);
        Assert.Contains("No Physical Damage", rawText, StringComparison.Ordinal);

        var runtime = Resolve(rawText, OfficialTradeCatalog.Value);
        var component = FindComponent(runtime.ProviderDraft, "No Physical Damage");

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
        Assert.Equal(["local_weapon_no_physical_damage"], component.ResolvedStatIds);
        Assert.Contains(
            "<number>% increased Physical Damage",
            component.ProviderSearchSignatures,
            StringComparer.OrdinalIgnoreCase);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_1509134228", component.ProviderStatId);
        Assert.Equal("#% increased Physical Damage", component.ProviderStatText);
        Assert.False(component.SupportsValueBounds);
        Assert.Equal(ModifierBoundShape.PresenceOnly, component.ValueBoundShape);
        Assert.Null(component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);

        var filter = MapSingle(runtime.ProviderDraft, component, OfficialTradeCatalog.Value);
        Assert.Equal("explicit.stat_1509134228", filter.StatId);
        Assert.Null(filter.Minimum);
        Assert.Null(filter.Maximum);
        Assert.NotEqual("explicit.stat_3900877792", filter.StatId);
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
                (component.ProviderCanonicalSignature?.Contains(textFragment, StringComparison.Ordinal) ?? false) ||
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
            new NoFetchClient());

    private static string PreferCapture(string itemName)
    {
        string[] roots =
        [
            Path.Combine(Path.GetTempPath(), "PoEnhance-TRADE.2-ManualValidation"),
            Path.Combine(Path.GetTempPath(), "PoEnhance-GATE-A-UNIQUE-BigManual"),
        ];
        foreach (var root in roots)
        {
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

        return Path.Combine(roots[^1], $"missing-{itemName}.json");
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

        if (root.TryGetProperty("rawClipboardText", out var rootClipboard))
        {
            return rootClipboard.GetString() ?? string.Empty;
        }

        // Older BigManual captures omit raw clipboard; rebuild a parser-compatible Unique text.
        if (root.TryGetProperty("item", out var item) &&
            root.TryGetProperty("initialModifiers", out var modifiers))
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine($"Item Class: {item.GetProperty("itemClass").GetString()}");
            builder.AppendLine($"Rarity: {item.GetProperty("rarity").GetString()}");
            builder.AppendLine(item.GetProperty("displayName").GetString());
            builder.AppendLine(item.GetProperty("parsedBaseType").GetString());
            builder.AppendLine("--------");
            builder.AppendLine("Item Level: 80");
            builder.AppendLine("--------");
            foreach (var modifier in modifiers.EnumerateArray())
            {
                if (!modifier.TryGetProperty("raw", out var raw))
                {
                    continue;
                }

                if (raw.TryGetProperty("rawMetadataLine", out var meta) &&
                    !string.IsNullOrWhiteSpace(meta.GetString()))
                {
                    builder.AppendLine(meta.GetString());
                }

                if (raw.TryGetProperty("valueLines", out var lines))
                {
                    foreach (var line in lines.EnumerateArray())
                    {
                        builder.AppendLine(line.GetString());
                    }
                }
                else if (raw.TryGetProperty("originalText", out var original))
                {
                    builder.AppendLine(original.GetString());
                }
            }

            return builder.ToString();
        }

        throw new InvalidOperationException($"Capture missing clipboard text: {capturePath}");
    }

    private static PathOfExileTradeItemCatalog CreateTradeItemCatalog() =>
        new(
        [
            Item(0, "Cospri's Malice", "Jewelled Foil", "weapon"),
            Item(1, "Fidelitas' Spike", "Jagged Foil", "weapon"),
            Item(2, "Oro's Sacrifice", "Infernal Sword", "weapon"),
            Item(3, "Nuro's Harp", "Harbinger Bow", "weapon"),
            Item(4, "Storm Cloud", "Long Bow", "weapon"),
        ]);

    private static PathOfExileTradeItemEntry Item(
        int order,
        string name,
        string type,
        string groupId) =>
        new()
        {
            ProviderOrder = order,
            GroupId = groupId,
            GroupLabel = groupId,
            Name = name,
            Type = type,
            IsUnique = true,
        };

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
