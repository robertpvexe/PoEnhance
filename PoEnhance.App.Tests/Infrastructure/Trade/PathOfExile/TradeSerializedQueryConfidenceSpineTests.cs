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

    [Fact]
    public void Echoforge_DefaultUnchecked_SerializesUniqueIdentityOnly()
    {
        var raw = ReadClipboard(
            "PoEnhance.Core.Tests",
            "TestData",
            "Unique",
            "ReplayReadyCaptures",
            "Echoforge.clipboard.txt");
        Assert.Contains("Echoforge", raw, StringComparison.Ordinal);
        Assert.Contains("2% reduced Attack Speed", raw, StringComparison.Ordinal);

        var runtime = Resolve(raw);
        Assert.Equal("Echoforge", runtime.ProviderDraft.DisplayName, ignoreCase: true);
        Assert.All(runtime.ProviderDraft.ModifierFilters, filter => Assert.False(filter.IsSelected));

        var attackSpeed = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "reduced Attack Speed",
                StringComparison.OrdinalIgnoreCase));
        Assert.Equal([2m], attackSpeed.ObservedNumericValues);
        Assert.Equal([-2m], attackSpeed.CanonicalNumericValues);
        Assert.Equal(ModifierBoundDirection.Minimum, attackSpeed.DefaultBoundDirection);
        Assert.Equal(-2m, attackSpeed.RequestedMinimum);
        Assert.Null(attackSpeed.RequestedMaximum);

        var json = Serialize(runtime, selectedComponent: null);
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        Assert.Equal("Echoforge", query.GetProperty("name").GetString());
        Assert.Equal("Infernal Sword", query.GetProperty("type").GetString());
        AssertNoActiveOptionalStatFilters(query);
    }

    [Fact]
    public void Echoforge_SelectingReducedAttackSpeed_SerializesLocalIncreasedMinOnly()
    {
        var raw = ReadClipboard(
            "PoEnhance.Core.Tests",
            "TestData",
            "Unique",
            "ReplayReadyCaptures",
            "Echoforge.clipboard.txt");
        var runtime = Resolve(raw);
        var attackSpeed = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "reduced Attack Speed",
                StringComparison.OrdinalIgnoreCase));

        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, attackSpeed.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_210067635", attackSpeed.ProviderStatId);
        Assert.Contains(
            "increased Attack Speed",
            attackSpeed.ProviderStatText ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal([2m], attackSpeed.ObservedNumericValues);
        Assert.Equal([-2m], attackSpeed.CanonicalNumericValues);
        Assert.Equal(ModifierBoundDirection.Minimum, attackSpeed.DefaultBoundDirection);
        Assert.Equal(-2m, attackSpeed.RequestedMinimum);
        Assert.Null(attackSpeed.RequestedMaximum);

        var json = Serialize(runtime, attackSpeed);
        using var document = JsonDocument.Parse(json);
        var filter = Assert.Single(
            document.RootElement.GetProperty("query").GetProperty("stats")[0].GetProperty("filters").EnumerateArray());
        Assert.Equal("explicit.stat_210067635", filter.GetProperty("id").GetString());
        var value = filter.GetProperty("value");
        Assert.Equal(-2m, value.GetProperty("min").GetDecimal());
        Assert.False(value.TryGetProperty("max", out _));
        Assert.False(value.TryGetProperty("option", out _));
    }

    [Fact]
    public void VentorsGamble_SelectingReducedReservation_SerializesIncreasedMinFromObserved13()
    {
        var raw = ReadClipboard(
            "PoEnhance.Core.Tests",
            "TestData",
            "Unique",
            "ReplayReadyCaptures",
            "Ventors-Gamble.clipboard.txt");
        Assert.Contains("13% reduced Mana Reservation Efficiency of Skills", raw, StringComparison.Ordinal);

        var runtime = Resolve(raw);
        Assert.Equal("Ventor's Gamble", runtime.ProviderDraft.DisplayName, ignoreCase: true);
        var reservation = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "Mana Reservation Efficiency of Skills",
                StringComparison.OrdinalIgnoreCase));

        // Observed roll stays 13; prepared signed threshold defaults to -13 (not a silent -2).
        Assert.Equal([13m], reservation.ObservedNumericValues);
        Assert.Equal([-13m], reservation.CanonicalNumericValues);
        Assert.Equal(ModifierBoundDirection.Minimum, reservation.DefaultBoundDirection);
        Assert.Equal(-13m, reservation.RequestedMinimum);
        Assert.Null(reservation.RequestedMaximum);
        Assert.True(
            reservation.ProviderResolutionStatus is
                SearchComponentProviderResolutionStatus.Exact or
                SearchComponentProviderResolutionStatus.ExactEquivalentSet,
            reservation.ProviderResolutionStatus.ToString());
        Assert.True(reservation.IsSearchable, reservation.NotSearchableReason);
        var resolvedStatIds = ResolvedProviderStatIds(reservation);
        Assert.NotEmpty(resolvedStatIds);
        Assert.Contains(
            "increased Mana Reservation Efficiency of Skills",
            reservation.ProviderStatText ??
                OfficialTradeCatalog.Value.Entries.First(entry =>
                    resolvedStatIds.Contains(entry.Id, StringComparer.Ordinal)).Text,
            StringComparison.OrdinalIgnoreCase);
        Assert.All(
            resolvedStatIds,
            statId => Assert.Contains(
                OfficialTradeCatalog.Value.Entries,
                entry => string.Equals(entry.Id, statId, StringComparison.Ordinal)));

        var uncheckedJson = Serialize(runtime, selectedComponent: null);
        AssertNoActiveOptionalStatFilters(JsonDocument.Parse(uncheckedJson).RootElement.GetProperty("query"));

        var json = Serialize(runtime, reservation);
        AssertSerializedSignedMinimumOnly(json, expectedMinimum: -13m, reservation);
    }

    [Fact]
    public void VentorsGamble_UserEditedPreparedThreshold_PreservesDeliberateMinNegativeTwo()
    {
        var raw = ReadClipboard(
            "PoEnhance.Core.Tests",
            "TestData",
            "Unique",
            "ReplayReadyCaptures",
            "Ventors-Gamble.clipboard.txt");
        var runtime = Resolve(raw);
        var reservation = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "Mana Reservation Efficiency of Skills",
                StringComparison.OrdinalIgnoreCase));
        Assert.Equal([13m], reservation.ObservedNumericValues);
        Assert.Equal(-13m, reservation.RequestedMinimum);

        // Deliberate user edit of the prepared threshold must not rewrite ObservedNumericValues.
        var edited = reservation with
        {
            RequestedMinimum = -2m,
            RequestedMaximum = null,
            DefaultBoundDirection = ModifierBoundDirection.Minimum,
        };
        Assert.Equal([13m], edited.ObservedNumericValues);
        Assert.Equal([-13m], edited.CanonicalNumericValues);
        Assert.Equal(-2m, edited.RequestedMinimum);

        var json = Serialize(runtime, edited);
        AssertSerializedSignedMinimumOnly(json, expectedMinimum: -2m, edited);
    }

    private static void AssertSerializedSignedMinimumOnly(
        string json,
        decimal expectedMinimum,
        ResolvedSearchComponent selected)
    {
        using var document = JsonDocument.Parse(json);
        var filters = document.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray()
            .ToArray();
        Assert.NotEmpty(filters);

        var allowedIds = new HashSet<string>(ResolvedProviderStatIds(selected), StringComparer.Ordinal);
        Assert.NotEmpty(allowedIds);

        Assert.All(filters, filter =>
        {
            var id = filter.GetProperty("id").GetString();
            Assert.False(string.IsNullOrWhiteSpace(id));
            Assert.Contains(id!, allowedIds);
            var catalogEntry = Assert.Single(
                OfficialTradeCatalog.Value.Entries,
                entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
            Assert.Contains(
                "increased Mana Reservation Efficiency of Skills",
                catalogEntry.Text,
                StringComparison.OrdinalIgnoreCase);
            var value = filter.GetProperty("value");
            Assert.Equal(expectedMinimum, value.GetProperty("min").GetDecimal());
            Assert.False(value.TryGetProperty("max", out _));
            Assert.False(value.TryGetProperty("option", out _));
        });
    }

    [Fact]
    public void VentorsGamble_AdvancedSignedRange_FormatsPresentationWithoutAmbiguousHyphen()
    {
        var raw = ReadClipboard(
            "PoEnhance.Core.Tests",
            "TestData",
            "Unique",
            "ReplayReadyCaptures",
            "Ventors-Gamble-AdvancedReservation.clipboard.txt");
        var runtime = Resolve(raw);
        var reservation = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "Mana Reservation Efficiency of Skills",
                StringComparison.OrdinalIgnoreCase));

        Assert.Equal([13m], reservation.ObservedNumericValues);
        Assert.Equal(-13m, reservation.RequestedMinimum);
        Assert.Contains("(-15-15)", reservation.OriginalText, StringComparison.Ordinal);
        Assert.DoesNotContain("15--15", reservation.PresentationText ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("16--16", reservation.PresentationText ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(
            "(-15 to +15)",
            reservation.PresentationText ?? string.Empty,
            StringComparison.Ordinal);
        Assert.Contains(
            "reduced Mana Reservation Efficiency of Skills",
            reservation.PresentationText ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Progenesis_ReducedCharges_KeepsLowerIsBetterMaximumGuard()
    {
        // Opposite polarity / lower-is-better guard: do not globally flip negate → Minimum.
        // Production-shaped capture (same body as PathOfExileTradeRawRuntimeRegressionTests.ProgenesisText).
        const string raw = """
            Item Class: Utility Flasks
            Rarity: Unique
            Progenesis
            Amethyst Flask
            --------
            Quality: +20% (augmented)
            Lasts 10,50 (augmented) Seconds
            Consumes 30 (augmented) of 65 Charges on use
            Currently has 65 Charges
            Intangibility: 8%
            +35% to Chaos Resistance
            --------
            Requirements:
            Level: 60
            --------
            Item Level: 84
            --------
            Used when Charges reach full (enchant)
            --------
            { Unique Modifier }
            14(20-10)% reduced Charges per use
            { Unique Modifier }
            34(-35-35)% increased Duration
            { Unique Modifier }
            When Hit during effect, 25% of Life loss from Damage taken occurs over 4 seconds instead
            --------
            They were bred in a cosmic ocean of raw creation.
            """;
        var runtime = Resolve(raw);
        var charges = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "reduced Charges per use",
                StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ModifierBoundDirection.Maximum, charges.DefaultBoundDirection);
        Assert.Null(charges.RequestedMinimum);
        Assert.Equal(-14m, charges.RequestedMaximum);

        var json = Serialize(runtime, charges);
        using var document = JsonDocument.Parse(json);
        var filter = Assert.Single(
            document.RootElement.GetProperty("query").GetProperty("stats")[0].GetProperty("filters").EnumerateArray());
        Assert.Equal("explicit.stat_388617051", filter.GetProperty("id").GetString());
        var value = filter.GetProperty("value");
        Assert.Equal(-14m, value.GetProperty("max").GetDecimal());
        Assert.False(value.TryGetProperty("min", out _));
    }

    private static IReadOnlyList<string> ResolvedProviderStatIds(ResolvedSearchComponent component)
    {
        var ids = component.ProviderStatAlternativeIds
            .Where(statId => !string.IsNullOrWhiteSpace(statId))
            .Select(statId => statId.Trim())
            .ToList();
        if (!string.IsNullOrWhiteSpace(component.ProviderStatId))
        {
            ids.Insert(0, component.ProviderStatId.Trim());
        }

        return ids.Distinct(StringComparer.Ordinal).ToArray();
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
                .Select(component =>
                {
                    if (selectedComponent is not null &&
                        string.Equals(
                            component.ComponentId,
                            selectedComponent.ComponentId,
                            StringComparison.Ordinal))
                    {
                        // Use the caller-provided instance so deliberate Min/Max edits serialize.
                        return selectedComponent with { IsSelected = true };
                    }

                    return component with { IsSelected = false };
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
            UniqueItem(2, "Echoforge", "Infernal Sword", "weapon"),
            UniqueItem(3, "Ventor's Gamble", "Gold Ring", "accessory"),
            UniqueItem(4, "Progenesis", "Amethyst Flask", "flask"),
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
