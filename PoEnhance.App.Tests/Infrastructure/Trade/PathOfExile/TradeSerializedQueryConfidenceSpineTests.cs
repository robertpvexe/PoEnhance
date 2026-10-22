using System.Collections.Immutable;
using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// P0 Step B.1 — narrow offline contract: clipboard → Core draft → provider resolve →
/// production <see cref="PathOfExileTradeQueryBuilder"/> serialized official Trade JSON.
/// Does not call Trade Search/Fetch HTTP. Does not accept Foulborn / Rare / live listing quality.
/// </summary>
public sealed class TradeSerializedQueryConfidenceSpineTests
{
    private const string League = "Standard";
    private const string OfficialStatsFixtureRelative =
        "PoEnhance.App.Tests/TestData/Trade/official-stats-2026-08-19.json";

    private static readonly Lazy<GameDataCatalog> GameData = new(LoadGameData);
    private static readonly Lazy<PathOfExileTradeStatCatalog> OfficialTradeCatalog =
        new(LoadOfficialTradeCatalog);
    private static readonly PathOfExileTradeFilterCatalog FilterCatalog =
        PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    private static readonly PathOfExileTradeItemCatalog TradeItemCatalog = CreateTradeItemCatalog();
    private static readonly PathOfExileTradeSelectedModifierMapper SelectedMapper = new();
    private static readonly PathOfExileTradeItemPropertyResolver ItemPropertyResolver = new();
    private static readonly PathOfExileTradeQueryBuilder QueryBuilder = new();

    [Fact]
    public void Lifesprig_DefaultUnchecked_SerializesUniqueIdentityWithoutOptionalStatOrUnavailableDpsFilters()
    {
        // Provenance: committed PreferCapture twin
        // PoEnhance.Core.Tests/TestData/Unique/ReplayReadyCaptures/Lifesprig.clipboard.txt
        // (A.5.36 Unique-NoModId-Capture 20260924-093804-947).
        var raw = ReadClipboard(
            "PoEnhance.Core.Tests",
            "TestData",
            "Unique",
            "ReplayReadyCaptures",
            "Lifesprig.clipboard.txt");
        Assert.Contains("Lifesprig", raw, StringComparison.Ordinal);

        var runtime = Resolve(raw);
        Assert.Equal("Lifesprig", runtime.ProviderDraft.DisplayName, ignoreCase: true);
        Assert.All(runtime.ProviderDraft.ModifierFilters, filter => Assert.False(filter.IsSelected));
        Assert.All(runtime.ProviderDraft.ItemProperties, property => Assert.False(property.IsSelected));

        var physical = Assert.Single(
            runtime.ProviderDraft.ItemProperties,
            property => property.Kind == TradeSearchItemPropertyKind.PhysicalDps);
        var total = Assert.Single(
            runtime.ProviderDraft.ItemProperties,
            property => property.Kind == TradeSearchItemPropertyKind.TotalDps);
        Assert.Equal(10.5m, physical.ObservedValue);
        Assert.Equal(10.5m, total.ObservedValue);
        Assert.Equal(
            TradeSearchItemPropertyQualityNormalizationStatus.Unavailable,
            physical.QualityNormalizationStatus);
        Assert.Equal(
            TradeSearchItemPropertyQualityNormalizationStatus.Unavailable,
            total.QualityNormalizationStatus);
        Assert.False(physical.IsSearchable);
        Assert.False(total.IsSearchable);
        Assert.Null(physical.RequestedMinimum);
        Assert.Null(total.RequestedMinimum);

        // Do not touch Foulborn criteria in this slice; production ExactIdentity non-Foulborn
        // may already resolve Foulborn=No — Foulborn policy is out of B.1 acceptance.
        var json = Serialize(runtime, selectedComponent: null);
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");

        Assert.Equal("Lifesprig", query.GetProperty("name").GetString());
        Assert.Equal("Driftwood Wand", query.GetProperty("type").GetString());

        AssertNoActiveOptionalStatFilters(query);
        Assert.False(
            json.Contains("\"dps\"", StringComparison.Ordinal) &&
            json.Contains("weapon_filters", StringComparison.Ordinal));
        Assert.DoesNotContain("\"pdps\"", json, StringComparison.Ordinal);

        // Observed Core values must remain unchanged after serialization (no Q20 rewrite).
        Assert.Equal(10.5m, physical.ObservedValue);
        Assert.Equal(10.5m, total.ObservedValue);
        Assert.False(
            query.TryGetProperty("filters", out var filters) &&
            filters.TryGetProperty("misc_filters", out var misc) &&
            misc.TryGetProperty("filters", out var miscFilters) &&
            miscFilters.TryGetProperty("mutated", out var mutated) &&
            mutated.TryGetProperty("option", out var option) &&
            option.GetString() == "true",
            "Foulborn must not be auto-selected to Yes for ordinary Lifesprig.");
    }

    [Fact]
    public void AsenathGentleTouch_SelectingOneExactIntelligenceFilter_SerializesOnlyThatCatalogResolvedFilter()
    {
        // Provenance: committed real capture
        // PoEnhance.App.Tests/TestData/Unique/CurseOnHit/Asenath-s-Gentle-Touch.clipboard.txt
        var raw = ReadClipboard(
            "PoEnhance.App.Tests",
            "TestData",
            "Unique",
            "CurseOnHit",
            "Asenath-s-Gentle-Touch.clipboard.txt");
        Assert.Contains("Asenath's Gentle Touch", raw, StringComparison.Ordinal);

        var runtime = Resolve(raw);
        Assert.Equal("Asenath's Gentle Touch", runtime.ProviderDraft.DisplayName, ignoreCase: true);
        Assert.All(runtime.ProviderDraft.ModifierFilters, filter => Assert.False(filter.IsSelected));

        var uncheckedJson = Serialize(runtime, selectedComponent: null);
        using (var uncheckedDocument = JsonDocument.Parse(uncheckedJson))
        {
            var uncheckedQuery = uncheckedDocument.RootElement.GetProperty("query");
            Assert.Equal("Asenath's Gentle Touch", uncheckedQuery.GetProperty("name").GetString());
            Assert.Equal("Silk Gloves", uncheckedQuery.GetProperty("type").GetString());
            AssertNoActiveOptionalStatFilters(uncheckedQuery);
        }

        var intelligence = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains("to Intelligence", StringComparison.Ordinal));
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, intelligence.ResolutionStatus);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, intelligence.ProviderResolutionStatus);
        Assert.True(intelligence.IsSearchable, intelligence.NotSearchableReason);
        Assert.False(string.IsNullOrWhiteSpace(intelligence.ProviderStatId));

        // Stat ID must come from the frozen official catalogue entry, not a test hardcode.
        var catalogEntry = Assert.Single(
            OfficialTradeCatalog.Value.Entries,
            entry => string.Equals(entry.Id, intelligence.ProviderStatId, StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(catalogEntry.Text));

        var mapping = MapSelected(runtime.ProviderDraft, intelligence);
        var expectedFilter = Assert.Single(mapping.Filters);
        Assert.Equal(intelligence.ProviderStatId, expectedFilter.StatId);
        Assert.Equal(catalogEntry.Id, expectedFilter.StatId);

        var selectedJson = Serialize(runtime, intelligence);
        using var selectedDocument = JsonDocument.Parse(selectedJson);
        var selectedQuery = selectedDocument.RootElement.GetProperty("query");
        Assert.Equal("Asenath's Gentle Touch", selectedQuery.GetProperty("name").GetString());

        var serializedFilters = selectedQuery
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray()
            .ToArray();
        var serialized = Assert.Single(serializedFilters);
        Assert.Equal(expectedFilter.StatId, serialized.GetProperty("id").GetString());

        if (expectedFilter.Option is not null ||
            expectedFilter.Minimum.HasValue ||
            expectedFilter.Maximum.HasValue)
        {
            var value = serialized.GetProperty("value");
            if (expectedFilter.Option is not null)
            {
                Assert.Equal(expectedFilter.Option, value.GetProperty("option").GetString());
            }

            if (expectedFilter.Minimum.HasValue)
            {
                Assert.Equal(expectedFilter.Minimum.Value, value.GetProperty("min").GetDecimal());
            }
            else
            {
                Assert.False(value.TryGetProperty("min", out _));
            }

            if (expectedFilter.Maximum.HasValue)
            {
                Assert.Equal(expectedFilter.Maximum.Value, value.GetProperty("max").GetDecimal());
            }
            else
            {
                Assert.False(value.TryGetProperty("max", out _));
            }
        }
        else
        {
            Assert.False(serialized.TryGetProperty("value", out _));
        }

        // Unrelated unchecked optional mods must remain absent.
        foreach (var other in runtime.ProviderDraft.ModifierFilters.Where(component =>
                     component.ComponentId != intelligence.ComponentId &&
                     !string.IsNullOrWhiteSpace(component.ProviderStatId)))
        {
            Assert.DoesNotContain(
                $"\"{other.ProviderStatId}\"",
                selectedJson,
                StringComparison.Ordinal);
        }
    }

    private static void AssertNoActiveOptionalStatFilters(JsonElement query)
    {
        if (!query.TryGetProperty("stats", out var stats) ||
            stats.ValueKind != JsonValueKind.Array ||
            stats.GetArrayLength() == 0)
        {
            return;
        }

        foreach (var group in stats.EnumerateArray())
        {
            if (!group.TryGetProperty("filters", out var filters) ||
                filters.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            Assert.Empty(filters.EnumerateArray());
        }
    }

    private static string Serialize(RuntimeResult runtime, ResolvedSearchComponent? selectedComponent)
    {
        var selectedDraft = runtime.ProviderDraft with
        {
            ModifierFilters = runtime.ProviderDraft.ModifierFilters
                .Select(component => component with
                {
                    IsSelected = selectedComponent is not null &&
                        string.Equals(
                            component.ComponentId,
                            selectedComponent.ComponentId,
                            StringComparison.Ordinal),
                })
                .ToArray(),
            ItemProperties = runtime.ProviderDraft.ItemProperties
                .Select(property => property with { IsSelected = false })
                .ToImmutableArray(),
        };

        var mapping = selectedComponent is null
            ? PathOfExileTradeSelectedModifierMappingResult.Success([])
            : MapSelected(selectedDraft, selectedComponent);

        Assert.True(
            mapping.IsSuccess,
            string.Join(" | ", mapping.Diagnostics.Select(diagnostic => diagnostic.Message)));

        var build = QueryBuilder.Build(
            selectedDraft,
            new TradeSearchDraftValidator().Validate(selectedDraft),
            League,
            mapping.Filters,
            runtime.UniqueIdentity,
            FilterCatalog,
            selectedItemPropertyFilters: []);
        Assert.True(build.IsSuccess, string.Join(" | ", build.Diagnostics.Select(d => d.Message)));
        Assert.False(string.IsNullOrWhiteSpace(build.SerializedJson));
        return build.SerializedJson!;
    }

    private static PathOfExileTradeSelectedModifierMappingResult MapSelected(
        TradeSearchDraft draft,
        ResolvedSearchComponent selected)
    {
        var selectedDraft = draft with
        {
            ModifierFilters = draft.ModifierFilters
                .Select(component => component with
                {
                    IsSelected = string.Equals(
                        component.ComponentId,
                        selected.ComponentId,
                        StringComparison.Ordinal),
                })
                .ToArray(),
        };
        return SelectedMapper.Map(selectedDraft, OfficialTradeCatalog.Value);
    }

    private static RuntimeResult Resolve(string rawText)
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
        Assert.True(
            draftResult.IsSuccess,
            string.Join(" | ", draftResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var draft = Assert.IsType<TradeSearchDraft>(draftResult.Draft);
        var uniqueIdentity = new PathOfExileTradeItemIdentityMapper()
            .Map(draft, TradeItemCatalog)
            .Identity;
        var propertyDraft = ItemPropertyResolver.Resolve(draft, FilterCatalog);
        var providerDraft = CreatePriceCheckService().ResolveProviderComponents(
            propertyDraft,
            OfficialTradeCatalog.Value,
            uniqueIdentity,
            FilterCatalog);
        return new RuntimeResult(providerDraft, uniqueIdentity);
    }

    private static PathOfExileTradePriceCheckService CreatePriceCheckService() =>
        new(
            QueryBuilder,
            new PathOfExileTradeStatMatcher(),
            new StaticStatProvider(OfficialTradeCatalog.Value),
            new StaticItemProvider(TradeItemCatalog),
            SelectedMapper,
            new PathOfExileTradeItemIdentityMapper(),
            new NoSearchClient(),
            new NoFetchClient());

    private static PathOfExileTradeItemCatalog CreateTradeItemCatalog() =>
        new(
        [
            UniqueItem(0, "Lifesprig", "Driftwood Wand", "weapon"),
            UniqueItem(1, "Asenath's Gentle Touch", "Silk Gloves", "armour"),
        ]);

    private static PathOfExileTradeItemEntry UniqueItem(
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
        // Frozen snapshot used by PreferCapture App Trade tests (not live-updated in this task).
        // Identity: official-stats-2026-08-19.json (date in filename; see evidence 02_DATA_AND_TRADE_FRESHNESS).
        var path = FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Trade",
            "official-stats-2026-08-19.json");
        Assert.EndsWith(
            OfficialStatsFixtureRelative.Replace('/', Path.DirectorySeparatorChar),
            path,
            StringComparison.OrdinalIgnoreCase);
        var parsed = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(File.ReadAllText(path));
        Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<PathOfExileTradeStatCatalog>(parsed.Catalog);
    }

    private static string ReadClipboard(params string[] relativeParts) =>
        File.ReadAllText(FindRepoFile(relativeParts));

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
        PathOfExileTradeItemIdentity? UniqueIdentity);

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
