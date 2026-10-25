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

    [Fact]
    public void Oskarm_SyntheticChaosResistance_SelectedSerializesOfficialPlusAxisMinOnly()
    {
        // SYNTHETIC fixture (not a committed genuine capture): production-shaped Oskarm line for
        // ChaosResistUnique__8 [-20,-10] with observed -16. Live screenshot motivated the case;
        // clipboard text below is constructed for offline serialization coverage only.
        var runtime = Resolve(OskarmSyntheticClipboard);
        Assert.Equal("Oskarm", runtime.ProviderDraft.DisplayName, ignoreCase: true);

        var chaos = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "Chaos Resistance",
                StringComparison.OrdinalIgnoreCase) &&
                !component.OriginalText.Contains("maximum", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, chaos.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_2923486259", chaos.ProviderStatId);
        Assert.Contains(
            "+#% to Chaos Resistance",
            chaos.ProviderStatText ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal([-16m], chaos.ObservedNumericValues);
        Assert.Equal([-16m], chaos.CanonicalNumericValues);
        Assert.Equal(ModifierBoundDirection.Minimum, chaos.DefaultBoundDirection);
        Assert.Equal(-16m, chaos.RequestedMinimum);
        Assert.Null(chaos.RequestedMaximum);

        var uncheckedJson = Serialize(runtime, selectedComponent: null);
        AssertNoActiveOptionalStatFilters(JsonDocument.Parse(uncheckedJson).RootElement.GetProperty("query"));

        var json = Serialize(runtime, chaos);
        using var document = JsonDocument.Parse(json);
        var filter = Assert.Single(
            document.RootElement.GetProperty("query").GetProperty("stats")[0].GetProperty("filters").EnumerateArray());
        Assert.Equal("explicit.stat_2923486259", filter.GetProperty("id").GetString());
        var value = filter.GetProperty("value");
        Assert.Equal(-16m, value.GetProperty("min").GetDecimal());
        Assert.False(value.TryGetProperty("max", out _));
        Assert.False(value.TryGetProperty("option", out _));
    }

    [Fact]
    public void Oskarm_SyntheticChaosResistance_UserEditedMinPreservedInSerializedJson()
    {
        var runtime = Resolve(OskarmSyntheticClipboard);
        var chaos = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "Chaos Resistance",
                StringComparison.OrdinalIgnoreCase) &&
                !component.OriginalText.Contains("maximum", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(-16m, chaos.RequestedMinimum);

        var edited = chaos with
        {
            RequestedMinimum = -10m,
            RequestedMaximum = null,
            DefaultBoundDirection = ModifierBoundDirection.Minimum,
        };
        Assert.Equal([-16m], edited.ObservedNumericValues);
        Assert.Equal(-10m, edited.RequestedMinimum);

        var json = Serialize(runtime, edited);
        using var document = JsonDocument.Parse(json);
        var filter = Assert.Single(
            document.RootElement.GetProperty("query").GetProperty("stats")[0].GetProperty("filters").EnumerateArray());
        Assert.Equal("explicit.stat_2923486259", filter.GetProperty("id").GetString());
        var value = filter.GetProperty("value");
        Assert.Equal(-10m, value.GetProperty("min").GetDecimal());
        Assert.False(value.TryGetProperty("max", out _));
    }

    [Fact]
    public void RecklessDefence_SyntheticHitsCritAgainstYou_RemainsDeferredMinimumDefault()
    {
        // POST-MVP handoff note (do not "fix" here): Hits have #% increased Critical Strike
        // Chance against you is defensively lower-is-better, but current defaults stay Minimum.
        // SYNTHETIC fixture — not a genuine capture.
        const string raw = """
            Item Class: Jewels
            Rarity: Unique
            Reckless Defence
            Cobalt Jewel
            --------
            Item Level: 72
            --------
            { Unique Modifier }
            +(4-6)% Chance to Block Attack Damage
            { Unique Modifier }
            +(4-6)% Chance to Block Spell Damage
            { Unique Modifier }
            Hits have 170(140-200)% increased Critical Strike Chance against you
            --------
            """;
        var runtime = Resolve(raw);
        var hitsCrit = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "Critical Strike Chance against you",
                StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ModifierBoundDirection.Minimum, hitsCrit.DefaultBoundDirection);
        Assert.Equal(170m, hitsCrit.RequestedMinimum);
        Assert.Null(hitsCrit.RequestedMaximum);
    }

    [Fact]
    public void ReplicaParadoxica_GenuineChosenPhysBlind_IndependentNumericBoundsAndSelectedJson()
    {
        // Genuine user Ctrl+C paste (U.2.4H). Stored once under ReplayReadyCaptures; not a TEMP JSON
        // machine capture. Chosen Prefix phys+blind is one GeneratedCandidate Exact composition;
        // Trade keeps two independent numeric filters (fixture official-stats-2026-08-19.json).
        var raw = ReadClipboard(
            "PoEnhance.Core.Tests",
            "TestData",
            "Unique",
            "ReplayReadyCaptures",
            "Replica-Paradoxica-Chosen-PhysBlind.clipboard.txt");
        Assert.Contains("Replica Paradoxica", raw, StringComparison.Ordinal);

        var runtime = Resolve(raw);
        Assert.Equal("Replica Paradoxica", runtime.ProviderDraft.DisplayName, ignoreCase: true);
        Assert.All(runtime.ProviderDraft.ModifierFilters, filter => Assert.False(filter.IsSelected));

        var phys = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "increased Physical Damage",
                StringComparison.OrdinalIgnoreCase));
        var blind = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "chance to Blind Enemies on hit",
                StringComparison.OrdinalIgnoreCase) &&
                !component.OriginalText.Contains("Being Blinded", StringComparison.OrdinalIgnoreCase));
        var chaos = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "increased Chaos Damage",
                StringComparison.OrdinalIgnoreCase));

        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, phys.UniqueSourceSemantics);
        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, blind.UniqueSourceSemantics);
        Assert.Equal(
            "JunMasterVeiledLocalIncreasedPhysicalDamageAndBlindChanceCrafted_",
            phys.ResolvedModifierId);
        Assert.Equal(phys.ResolvedModifierId, blind.ResolvedModifierId);
        Assert.Equal([134m], phys.ObservedNumericValues);
        Assert.Equal([23m], blind.ObservedNumericValues);
        Assert.True(phys.SupportsValueBounds);
        Assert.True(blind.SupportsValueBounds);
        Assert.Equal(ModifierBoundDirection.Minimum, phys.DefaultBoundDirection);
        Assert.Equal(ModifierBoundDirection.Minimum, blind.DefaultBoundDirection);
        Assert.Equal(134m, phys.RequestedMinimum);
        Assert.Null(phys.RequestedMaximum);
        Assert.Equal(23m, blind.RequestedMinimum);
        Assert.Null(blind.RequestedMaximum);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, phys.ProviderResolutionStatus);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, blind.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_1509134228", phys.ProviderStatId);
        Assert.Equal("explicit.stat_2301191210", blind.ProviderStatId);
        Assert.NotEqual(phys.ProviderStatId, blind.ProviderStatId);

        // Nearby working generated option (chaos duration composite) stays numeric.
        Assert.Equal([60m], chaos.ObservedNumericValues);
        Assert.True(chaos.SupportsValueBounds);
        Assert.Equal(60m, chaos.RequestedMinimum);

        var uncheckedJson = Serialize(runtime, selectedComponent: null);
        AssertNoActiveOptionalStatFilters(JsonDocument.Parse(uncheckedJson).RootElement.GetProperty("query"));

        var physJson = Serialize(runtime, phys);
        using (var document = JsonDocument.Parse(physJson))
        {
            var filter = Assert.Single(
                document.RootElement.GetProperty("query").GetProperty("stats")[0]
                    .GetProperty("filters")
                    .EnumerateArray());
            Assert.Equal("explicit.stat_1509134228", filter.GetProperty("id").GetString());
            var value = filter.GetProperty("value");
            Assert.Equal(134m, value.GetProperty("min").GetDecimal());
            Assert.False(value.TryGetProperty("max", out _));
            Assert.False(value.TryGetProperty("option", out _));
        }

        var blindJson = Serialize(runtime, blind);
        using (var document = JsonDocument.Parse(blindJson))
        {
            var filter = Assert.Single(
                document.RootElement.GetProperty("query").GetProperty("stats")[0]
                    .GetProperty("filters")
                    .EnumerateArray());
            Assert.Equal("explicit.stat_2301191210", filter.GetProperty("id").GetString());
            var value = filter.GetProperty("value");
            Assert.Equal(23m, value.GetProperty("min").GetDecimal());
            Assert.False(value.TryGetProperty("max", out _));
        }

        var editedPhys = phys with { RequestedMinimum = 130m };
        Assert.Equal([134m], editedPhys.ObservedNumericValues);
        var editedJson = Serialize(runtime, editedPhys);
        using (var document = JsonDocument.Parse(editedJson))
        {
            var value = Assert.Single(
                    document.RootElement.GetProperty("query").GetProperty("stats")[0]
                        .GetProperty("filters")
                        .EnumerateArray())
                .GetProperty("value");
            Assert.Equal(130m, value.GetProperty("min").GetDecimal());
        }
    }

    [Fact]
    public void ReplicaParadoxica_GenuineCapture_ThreeSuspectGeneratedOptions_ExactTradeJson()
    {
        // Same genuine Ctrl+D capture as PhysBlind. U.2.4H recovered multi-line phys+blind;
        // single-line GeneratedCandidate siblings (Chaos Penetrate / Crit Multi Nearby / Double
        // Damage) must keep Unique-resolved StatIds when AID Exact also matches — otherwise
        // provider mapping fails closed as Unsupported despite Exact GameData recovery.
        var raw = ReadClipboard(
            "PoEnhance.Core.Tests",
            "TestData",
            "Unique",
            "ReplayReadyCaptures",
            "Replica-Paradoxica-Chosen-PhysBlind.clipboard.txt");
        var runtime = Resolve(raw);

        var penetrate = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "Penetrate",
                StringComparison.OrdinalIgnoreCase) &&
                component.OriginalText.Contains("Chaos Resistance", StringComparison.OrdinalIgnoreCase));
        var critNearby = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "Critical Strike Multiplier while a Rare or Unique Enemy is Nearby",
                StringComparison.OrdinalIgnoreCase));
        var doubleDamage = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "chance to deal Double Damage",
                StringComparison.OrdinalIgnoreCase) &&
                !component.OriginalText.Contains("Focused", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(
            "JunMasterVeiledChaosPenetrationWithAttacks__",
            penetrate.ResolvedModifierId);
        Assert.Equal(
            "JunMasterVeiledCriticalStrikeMultiplierIfRareOrUniqueEnemyNearby1h",
            critNearby.ResolvedModifierId);
        Assert.Equal("JunMasterVeiledDoubleDamageChance1h", doubleDamage.ResolvedModifierId);
        Assert.Equal(["local_chaos_penetration_%"], penetrate.ResolvedStatIds.ToArray());
        Assert.Equal(
            ["critical_strike_multiplier_+_if_rare_or_unique_enemy_nearby"],
            critNearby.ResolvedStatIds.ToArray());
        Assert.Equal(["chance_to_deal_double_damage_%"], doubleDamage.ResolvedStatIds.ToArray());
        Assert.Equal([14m], penetrate.ObservedNumericValues);
        Assert.Equal([40m], critNearby.ObservedNumericValues);
        Assert.Equal([6m], doubleDamage.ObservedNumericValues);
        Assert.True(penetrate.SupportsValueBounds);
        Assert.True(critNearby.SupportsValueBounds);
        Assert.True(doubleDamage.SupportsValueBounds);
        Assert.Equal(14m, penetrate.RequestedMinimum);
        Assert.Equal(40m, critNearby.RequestedMinimum);
        Assert.Equal(6m, doubleDamage.RequestedMinimum);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, penetrate.ProviderResolutionStatus);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, critNearby.ProviderResolutionStatus);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, doubleDamage.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_3762412853", penetrate.ProviderStatId);
        Assert.Equal("explicit.stat_3992439283", critNearby.ProviderStatId);
        Assert.Equal("explicit.stat_1172810729", doubleDamage.ProviderStatId);

        // Phys+blind siblings from the same capture remain Exact with independent Trade filters.
        var phys = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "increased Physical Damage",
                StringComparison.OrdinalIgnoreCase));
        var blind = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "chance to Blind Enemies on hit",
                StringComparison.OrdinalIgnoreCase) &&
                !component.OriginalText.Contains("Being Blinded", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, phys.ProviderResolutionStatus);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, blind.ProviderResolutionStatus);
        Assert.Equal(134m, phys.RequestedMinimum);
        Assert.Equal(23m, blind.RequestedMinimum);
        Assert.Equal("explicit.stat_1509134228", phys.ProviderStatId);
        Assert.Equal("explicit.stat_2301191210", blind.ProviderStatId);

        AssertNoActiveOptionalStatFilters(
            JsonDocument.Parse(Serialize(runtime, selectedComponent: null)).RootElement.GetProperty("query"));

        AssertSelectedScalarMinJson(runtime, penetrate, "explicit.stat_3762412853", 14m);
        AssertSelectedScalarMinJson(runtime, critNearby, "explicit.stat_3992439283", 40m);
        AssertSelectedScalarMinJson(runtime, doubleDamage, "explicit.stat_1172810729", 6m);
    }

    private static void AssertSelectedScalarMinJson(
        RuntimeResult runtime,
        ResolvedSearchComponent component,
        string expectedStatId,
        decimal expectedMin)
    {
        using var document = JsonDocument.Parse(Serialize(runtime, component));
        var filter = Assert.Single(
            document.RootElement.GetProperty("query").GetProperty("stats")[0]
                .GetProperty("filters")
                .EnumerateArray());
        Assert.Equal(expectedStatId, filter.GetProperty("id").GetString());
        var value = filter.GetProperty("value");
        Assert.Equal(expectedMin, value.GetProperty("min").GetDecimal());
        Assert.False(value.TryGetProperty("max", out _));
        Assert.False(value.TryGetProperty("option", out _));
    }

    [Fact]
    public void ReplicaAtzirisAcuity_SyntheticCurrentGlobalCrit_53of40_60_SelectedSerializesOfficialMinOnly()
    {
        // SYNTHETIC fixture (not a committed genuine capture): mirrors the user's observed current
        // Replica Atziri's Acuity Crit roll 53(40-60). Official Trade filter explicit.stat_587431675.
        // Request a real Ctrl+D capture for manual verification — do not treat this as genuine.
        var runtime = Resolve(ReplicaAtzirisAcuitySyntheticCurrentClipboard);
        Assert.Equal("Replica Atziri's Acuity", runtime.ProviderDraft.DisplayName, ignoreCase: true);

        var crit = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "increased Global Critical Strike Chance",
                StringComparison.OrdinalIgnoreCase));
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, crit.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_587431675", crit.ProviderStatId);
        Assert.Equal([53m], crit.ObservedNumericValues);
        Assert.Equal(ModifierBoundDirection.Minimum, crit.DefaultBoundDirection);
        Assert.Equal(53m, crit.RequestedMinimum);
        Assert.Null(crit.RequestedMaximum);
        Assert.False(crit.IsSelected);

        var uncheckedJson = Serialize(runtime, selectedComponent: null);
        AssertNoActiveOptionalStatFilters(JsonDocument.Parse(uncheckedJson).RootElement.GetProperty("query"));

        var json = Serialize(runtime, crit);
        using var document = JsonDocument.Parse(json);
        var filter = Assert.Single(
            document.RootElement.GetProperty("query").GetProperty("stats")[0].GetProperty("filters").EnumerateArray());
        Assert.Equal("explicit.stat_587431675", filter.GetProperty("id").GetString());
        var value = filter.GetProperty("value");
        Assert.Equal(53m, value.GetProperty("min").GetDecimal());
        Assert.False(value.TryGetProperty("max", out _));
        Assert.False(value.TryGetProperty("option", out _));
    }

    // SYNTHETIC current Replica Atziri's Acuity clipboard — 53(40-60) after 3.16 RePoE base reconcile;
    // not ReplayReady / not a genuine capture. Please supply a real capture for manual verification.
    private const string ReplicaAtzirisAcuitySyntheticCurrentClipboard = """
        Item Class: Gloves
        Rarity: Unique
        Replica Atziri's Acuity
        Vaal Gauntlets
        --------
        Quality: +20%
        Armour: 626
        --------
        Requirements:
        Level: 63
        Str: 100
        --------
        Sockets: R-R-R-R
        --------
        Item Level: 84
        --------
        { Unique Modifier }
        +(68-80) to Intelligence
        { Unique Modifier }
        53(40-60)% increased Global Critical Strike Chance
        { Unique Modifier }
        (210-220)% increased Armour
        { Unique Modifier }
        +(70-75) to maximum Life
        { Unique Modifier }
        You have Perfect Agony if you've dealt a Critical Strike recently
        --------
        """;

    // SYNTHETIC Oskarm clipboard — constructed for bound/serialization regression; not ReplayReady.
    private const string OskarmSyntheticClipboard = """
        Item Class: Gloves
        Rarity: Unique
        Oskarm
        Nubuck Gloves
        --------
        Evasion Rating: 109
        --------
        Requirements:
        Level: 52
        Dex: 50
        --------
        Sockets: G-G-G-G
        --------
        Item Level: 75
        --------
        { Unique Modifier }
        Trigger Level 10 Assassin's Mark when you Hit a Rare or Unique Enemy and have no Mark
        { Unique Modifier }
        +(11-12)% chance to Suppress Spell Damage
        { Unique Modifier }
        35(30-40)% increased Global Accuracy Rating
        { Unique Modifier }
        +45(40-50) to maximum Life
        { Unique Modifier }
        -16(-20--10)% to Chaos Resistance
        { Unique Modifier }
        2% increased Attack Critical Strike Chance per 200 Accuracy Rating
        --------
        """;

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
            UniqueItem(5, "Oskarm", "Nubuck Gloves", "armour"),
            UniqueItem(6, "Reckless Defence", "Cobalt Jewel", "jewel"),
            UniqueItem(7, "Replica Atziri's Acuity", "Vaal Gauntlets", "armour"),
            UniqueItem(8, "Replica Paradoxica", "Vaal Rapier", "weapon"),
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
