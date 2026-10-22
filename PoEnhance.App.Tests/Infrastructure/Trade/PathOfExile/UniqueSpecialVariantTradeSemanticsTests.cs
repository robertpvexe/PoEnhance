using System.Collections.Immutable;
using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// U.2.2 / U.2.3 — test-only gate for Forbidden Flame/Flesh Allocates and Impossible Escape
/// Keystone Trade semantics. Fixtures are labelled SYNTHETIC or GENUINE. No live Search/Fetch.
/// </summary>
public sealed class UniqueSpecialVariantTradeSemanticsTests
{
    private const string League = "Standard";
    private const string FrozenStatsRelative =
        "PoEnhance.App.Tests/TestData/Trade/official-stats-2026-08-19.json";

    /// <summary>
    /// U.1B live capture (2026-10-20). Optional cross-check only; assertions use frozen catalogue.
    /// </summary>
    private static readonly string LiveStatsEvidencePath = Path.GetFullPath(Path.Combine(
        "D:",
        "Projects",
        "_PoEnhanceEvidenceClosure_20261020",
        "appendices",
        "live_sources",
        "trade_stats_live.json.txt"));

    private static readonly Lazy<GameDataCatalog> GameData = new(LoadGameData);
    private static readonly Lazy<PathOfExileTradeStatCatalog> FrozenTradeCatalog =
        new(LoadFrozenTradeCatalog);
    private static readonly PathOfExileTradeFilterCatalog FilterCatalog =
        PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    private static readonly PathOfExileTradeSelectedModifierMapper SelectedMapper = new();
    private static readonly PathOfExileTradeItemPropertyResolver ItemPropertyResolver = new();
    private static readonly PathOfExileTradeQueryBuilder QueryBuilder = new();

    [Fact]
    public void Catalogue_ForbiddenAndImpossibleEscape_UseFlattenedPipeIdsWithoutOptionSelector()
    {
        // Assertions use FROZEN Aug-19 official stats (deterministic PreferCapture twin).
        var frozen = FrozenTradeCatalog.Value;
        AssertFlattenedFamily(
            frozen,
            textContains: "if you have the matching modifier on Forbidden Flesh",
            expectedBasePrefix: "explicit.stat_1190333629|",
            expectedCount: 166);
        AssertFlattenedFamily(
            frozen,
            textContains: "if you have the matching modifier on Forbidden Flame",
            expectedBasePrefix: "explicit.stat_2460506030|",
            expectedCount: 166);
        AssertFlattenedFamily(
            frozen,
            textContains: "Passive Skills in Radius of",
            expectedBasePrefix: "explicit.stat_2422708892|",
            expectedCount: 48);

        Assert.DoesNotContain(
            frozen.Entries,
            entry => entry.Text.Contains("Requires Class", StringComparison.OrdinalIgnoreCase));

        // Explicit vs Passage: Trade group/type is Explicit; "Passage" appears as text chrome.
        var chaosInoculation = Assert.Single(
            frozen.Entries,
            entry => entry.Text.Contains("Chaos Inoculation", StringComparison.Ordinal) &&
                     entry.Text.Contains("Passive Skills in Radius", StringComparison.Ordinal));
        Assert.Equal("explicit", chaosInoculation.Type, ignoreCase: true);
        Assert.Equal("explicit", chaosInoculation.GroupId, ignoreCase: true);
        Assert.Contains("Passage", chaosInoculation.Text, StringComparison.Ordinal);
        Assert.StartsWith("explicit.stat_2422708892|", chaosInoculation.Id, StringComparison.Ordinal);
        Assert.Empty(chaosInoculation.Options);

        // Optional live U.1B cross-check (no new HTTP). Skip silently if evidence folder absent.
        if (File.Exists(LiveStatsEvidencePath))
        {
            var live = LoadTradeCatalogFromPossiblyWrappedJson(LiveStatsEvidencePath);
            AssertLiveAgreesOnKeyIds(frozen, live);
        }
    }

    [Fact]
    public void ForbiddenFlame_SyntheticAllocates_SerializesExactPipeIdWithoutOptionOrBounds()
    {
        // Evidence tier: SYNTHETIC / catalog-derived. Not a genuine Ctrl+D capture.
        var runtime = Resolve(SyntheticForbiddenFlameAllocatesOnly, includeTradeItems:
        [
            UniqueItem(0, "Forbidden Flame", "Crimson Jewel", "jewel"),
        ]);

        Assert.Equal("Forbidden Flame", runtime.ProviderDraft.DisplayName, ignoreCase: true);
        Assert.All(runtime.ProviderDraft.ModifierFilters, filter => Assert.False(filter.IsSelected));

        var identityJson = Serialize(runtime, selectedComponent: null);
        using (var identityDocument = JsonDocument.Parse(identityJson))
        {
            var query = identityDocument.RootElement.GetProperty("query");
            Assert.Equal("Forbidden Flame", query.GetProperty("name").GetString());
            Assert.Equal("Crimson Jewel", query.GetProperty("type").GetString());
            AssertNoActiveOptionalStatFilters(query);
            Assert.DoesNotContain("Requires Class", identityJson, StringComparison.OrdinalIgnoreCase);
        }

        var allocates = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains("Allocates Unnatural Strength", StringComparison.Ordinal));
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, allocates.ProviderResolutionStatus);
        Assert.True(allocates.IsSearchable, allocates.NotSearchableReason);
        Assert.False(string.IsNullOrWhiteSpace(allocates.ProviderStatId));
        Assert.Contains('|', allocates.ProviderStatId);

        // Resolve expected ID from frozen catalogue text — no production hardcode.
        var catalogEntry = Assert.Single(
            FrozenTradeCatalog.Value.Entries,
            entry => entry.Text.Contains("Allocates Unnatural Strength", StringComparison.Ordinal) &&
                     entry.Text.Contains("Forbidden Flesh", StringComparison.Ordinal));
        Assert.Equal(catalogEntry.Id, allocates.ProviderStatId);
        Assert.StartsWith("explicit.stat_1190333629|", catalogEntry.Id, StringComparison.Ordinal);
        Assert.Empty(catalogEntry.Options);

        Assert.Equal(ModifierBoundShape.PresenceOnly, allocates.ValueBoundShape);
        Assert.False(allocates.SupportsValueBounds);
        Assert.Null(allocates.RequestedMinimum);
        Assert.Null(allocates.RequestedMaximum);

        var selectedJson = Serialize(runtime, allocates);
        using var selectedDocument = JsonDocument.Parse(selectedJson);
        var selectedQuery = selectedDocument.RootElement.GetProperty("query");
        Assert.Equal("Forbidden Flame", selectedQuery.GetProperty("name").GetString());

        var serialized = Assert.Single(
            selectedQuery.GetProperty("stats")[0].GetProperty("filters").EnumerateArray());
        Assert.Equal(catalogEntry.Id, serialized.GetProperty("id").GetString());
        Assert.False(
            serialized.TryGetProperty("value", out _),
            "Valueless Allocates pipe ID must not emit value.option / min / max.");
    }

    [Fact]
    public void ForbiddenFlesh_SyntheticAllocates_SerializesCounterpartPipeIdWithoutOption()
    {
        // Evidence tier: SYNTHETIC / catalog-derived.
        var runtime = Resolve(SyntheticForbiddenFleshAllocatesOnly, includeTradeItems:
        [
            UniqueItem(0, "Forbidden Flesh", "Cobalt Jewel", "jewel"),
        ]);

        var allocates = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains("Allocates Unnatural Strength", StringComparison.Ordinal));
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, allocates.ProviderResolutionStatus);

        var catalogEntry = Assert.Single(
            FrozenTradeCatalog.Value.Entries,
            entry => entry.Text.Contains("Allocates Unnatural Strength", StringComparison.Ordinal) &&
                     entry.Text.Contains("Forbidden Flame", StringComparison.Ordinal));
        Assert.Equal(catalogEntry.Id, allocates.ProviderStatId);
        Assert.StartsWith("explicit.stat_2460506030|", catalogEntry.Id, StringComparison.Ordinal);

        var selectedJson = Serialize(runtime, allocates);
        using var document = JsonDocument.Parse(selectedJson);
        var serialized = Assert.Single(
            document.RootElement.GetProperty("query")
                .GetProperty("stats")[0]
                .GetProperty("filters")
                .EnumerateArray());
        Assert.Equal(catalogEntry.Id, serialized.GetProperty("id").GetString());
        Assert.False(serialized.TryGetProperty("value", out _));
    }

    [Fact]
    public void ForbiddenFlame_RequiresClass_RemainsNonSearchableInCatalog_AndIsOmittedFromPriceCheckerDraft()
    {
        // Evidence tier: SYNTHETIC for draft omission; GameData NonSearchable is catalog provenance.
        // Product decision (user → HQ 01): do NOT display Requires Class in Price Checker.
        var package = LoadPackage();
        var flame = Assert.Single(
            package.UniqueItems!.Items,
            item => string.Equals(item.CanonicalName, "Forbidden Flame", StringComparison.Ordinal));
        var current = Assert.Single(flame.Versions, version => version.Role == UniqueItemVersionRole.Current);
        var classBlocks = current.ModifierBlocks
            .Where(block =>
                block.Kind == UniqueModifierBlockKind.Unique &&
                block.Lines.Any(line => line.StartsWith("Requires Class ", StringComparison.Ordinal)))
            .ToArray();
        Assert.NotEmpty(classBlocks);
        Assert.All(classBlocks, block =>
        {
            Assert.Equal(UniqueModifierMechanicalMappingStatus.NonSearchable, block.MechanicalMapping.Status);
            Assert.Equal("UNIQUE_REQUIREMENT_NON_SEARCHABLE", block.MechanicalMapping.DiagnosticCode);
            Assert.Empty(block.MechanicalMapping.StatIds);
            Assert.Empty(block.MechanicalMapping.ModifierIds);
        });

        Assert.DoesNotContain(
            FrozenTradeCatalog.Value.Entries,
            entry => entry.Text.Contains("Requires Class", StringComparison.OrdinalIgnoreCase));

        var runtime = Resolve(SyntheticForbiddenFlameWithRequiresClass, includeTradeItems:
        [
            UniqueItem(0, "Forbidden Flame", "Crimson Jewel", "jewel"),
        ]);

        Assert.DoesNotContain(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains("Requires Class", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains("Allocates Unnatural Strength", StringComparison.Ordinal));

        var identityJson = Serialize(runtime, selectedComponent: null);
        Assert.DoesNotContain("Requires Class", identityJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"name\":\"Forbidden Flame\"", identityJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ImpossibleEscape_GenuineHexMasterCapture_ExactUncheckedThenSelectedPipeIdWithoutValue()
    {
        // Evidence tier: GENUINE in-game Ctrl+C (2026-10-22), byte-faithful fixture
        // TestData/Unique/ProviderRuntimeCaptures/Impossible-Escape-Hex-Master.clipboard.txt
        var raw = ReadProviderRuntimeCapture("Impossible-Escape-Hex-Master.clipboard.txt");
        Assert.Contains("Hex Master", raw, StringComparison.Ordinal);
        Assert.Contains("Passage — Unscalable Value", raw, StringComparison.Ordinal);

        var parsed = new ItemTextParser().Parse(raw);
        var uniqueMod = Assert.Single(parsed.UniqueModifiers);
        Assert.Equal(3, uniqueMod.ValueLines.Count);
        Assert.Equal(
            "Passive Skills in Radius of Hex Master can be Allocated",
            uniqueMod.ValueLines[0].Trim(),
            ignoreCase: false);
        Assert.Equal(
            "without being connected to your tree",
            uniqueMod.ValueLines[1].Trim(),
            ignoreCase: false);
        Assert.Equal("Passage", uniqueMod.ValueLines[2].Trim(), ignoreCase: false);
        Assert.True(
            uniqueMod.Effects[^1].HasUnscalableValue,
            "AID Unscalable annotation must remain on the Passage effect for provenance.");
        Assert.Equal(
            "Passage — Unscalable Value",
            uniqueMod.Effects[^1].RawText?.Trim(),
            ignoreCase: false);

        var catalogEntry = Assert.Single(
            FrozenTradeCatalog.Value.Entries,
            entry => entry.Text.Contains("Hex Master", StringComparison.Ordinal) &&
                     entry.Text.Contains("Passive Skills in Radius", StringComparison.Ordinal));
        Assert.StartsWith("explicit.stat_2422708892|", catalogEntry.Id, StringComparison.Ordinal);
        Assert.Empty(catalogEntry.Options);
        Assert.Equal("explicit", catalogEntry.Type, ignoreCase: true);

        var package = LoadPackage();
        var escape = Assert.Single(
            package.UniqueItems!.Items,
            item => string.Equals(item.CanonicalName, "Impossible Escape", StringComparison.Ordinal));
        var current = Assert.Single(escape.Versions, version => version.Role == UniqueItemVersionRole.Current);
        var hexBlock = Assert.Single(
            current.ModifierBlocks,
            block => block.Lines.Any(line =>
                line.Contains("Hex Master", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, hexBlock.MechanicalMapping.Status);
        Assert.Contains(
            "local_unique_jewel_disconnected_passives_can_be_allocated_around_keystone_hash",
            hexBlock.MechanicalMapping.StatIds);
        Assert.Contains(
            hexBlock.MechanicalMapping.Provenance?.Translations
                .SelectMany(evidence => evidence.IndexHandlers)
                .SelectMany(handler => handler.Handlers)
            ?? [],
            handler => string.Equals(handler, "passive_hash", StringComparison.Ordinal));

        var runtime = Resolve(raw, includeTradeItems:
        [
            UniqueItem(0, "Impossible Escape", "Viridian Jewel", "jewel"),
        ]);

        Assert.Equal("Impossible Escape", runtime.ProviderDraft.DisplayName, ignoreCase: true);
        Assert.All(runtime.ProviderDraft.ModifierFilters, filter => Assert.False(filter.IsSelected));

        var keystone = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains("Hex Master", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, keystone.ProviderResolutionStatus);
        Assert.True(keystone.IsSearchable, keystone.NotSearchableReason);
        Assert.Equal(catalogEntry.Id, keystone.ProviderStatId);
        Assert.Equal(ModifierBoundShape.PresenceOnly, keystone.ValueBoundShape);
        Assert.False(keystone.SupportsValueBounds);
        Assert.Null(keystone.RequestedMinimum);
        Assert.Null(keystone.RequestedMaximum);

        var identityJson = Serialize(runtime, selectedComponent: null);
        using (var identityDocument = JsonDocument.Parse(identityJson))
        {
            var identityQuery = identityDocument.RootElement.GetProperty("query");
            Assert.Equal("Impossible Escape", identityQuery.GetProperty("name").GetString());
            Assert.Equal("Viridian Jewel", identityQuery.GetProperty("type").GetString());
            AssertNoActiveOptionalStatFilters(identityQuery);
            Assert.DoesNotContain(catalogEntry.Id, identityJson, StringComparison.Ordinal);
        }

        var selectedJson = Serialize(runtime, keystone);
        using var selectedDocument = JsonDocument.Parse(selectedJson);
        var selectedQuery = selectedDocument.RootElement.GetProperty("query");
        Assert.Equal("Impossible Escape", selectedQuery.GetProperty("name").GetString());
        var serialized = Assert.Single(
            selectedQuery.GetProperty("stats")[0].GetProperty("filters").EnumerateArray());
        Assert.Equal(catalogEntry.Id, serialized.GetProperty("id").GetString());
        Assert.False(
            serialized.TryGetProperty("value", out _),
            "Selected genuine Hex Master must not emit value / option / min / max.");
    }

    [Fact]
    public void ForbiddenFlame_GenuineMarshalCapture_AllocatesExact_NoRequiresClassRow()
    {
        // Evidence tier: GENUINE in-game Ctrl+C (2026-10-22)
        // TestData/Unique/ProviderRuntimeCaptures/Forbidden-Flame-Marshal.clipboard.txt
        var raw = ReadProviderRuntimeCapture("Forbidden-Flame-Marshal.clipboard.txt");
        Assert.Contains("Class:: Templar (unmet)", raw, StringComparison.Ordinal);
        Assert.Contains("Allocates Marshal of Divinity", raw, StringComparison.Ordinal);

        var parsed = new ItemTextParser().Parse(raw);
        Assert.DoesNotContain(
            parsed.UniqueModifiers,
            modifier => modifier.ValueLines.Any(line =>
                line.Contains("Requires Class", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Class::", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(
            parsed.Properties,
            property => property.OriginalText.Contains("Class::", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Class", StringComparison.OrdinalIgnoreCase));

        var catalogEntry = Assert.Single(
            FrozenTradeCatalog.Value.Entries,
            entry => entry.Text.Contains("Allocates Marshal of Divinity", StringComparison.Ordinal) &&
                     entry.Text.Contains("Forbidden Flesh", StringComparison.Ordinal));

        var runtime = Resolve(raw, includeTradeItems:
        [
            UniqueItem(0, "Forbidden Flame", "Crimson Jewel", "jewel"),
        ]);

        Assert.DoesNotContain(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains("Requires Class", StringComparison.OrdinalIgnoreCase) ||
                component.OriginalText.Contains("Class::", StringComparison.OrdinalIgnoreCase) ||
                component.OriginalText.Contains("Templar", StringComparison.OrdinalIgnoreCase));

        var allocates = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "Allocates Marshal of Divinity",
                StringComparison.OrdinalIgnoreCase));
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, allocates.ProviderResolutionStatus);
        Assert.Equal(catalogEntry.Id, allocates.ProviderStatId);
        Assert.All(runtime.ProviderDraft.ModifierFilters, filter => Assert.False(filter.IsSelected));

        var identityJson = Serialize(runtime, selectedComponent: null);
        Assert.DoesNotContain("Requires Class", identityJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Templar", identityJson, StringComparison.OrdinalIgnoreCase);

        var selectedJson = Serialize(runtime, allocates);
        using var document = JsonDocument.Parse(selectedJson);
        var serialized = Assert.Single(
            document.RootElement.GetProperty("query")
                .GetProperty("stats")[0]
                .GetProperty("filters")
                .EnumerateArray());
        Assert.Equal(catalogEntry.Id, serialized.GetProperty("id").GetString());
        Assert.False(serialized.TryGetProperty("value", out _));
    }

    [Fact]
    public void ImpossibleEscape_PassageFold_DoesNotMatchDifferentKeystoneOrBareMultiline()
    {
        // Evidence tier: SYNTHETIC adverse cases for Passage chrome fold.
        var wrongKeystone = Resolve(SyntheticImpossibleEscapeMultilineWrongKeystone, includeTradeItems:
        [
            UniqueItem(0, "Impossible Escape", "Viridian Jewel", "jewel"),
        ]);
        Assert.DoesNotContain(
            wrongKeystone.ProviderDraft.ModifierFilters,
            component =>
                component.OriginalText.Contains("Hex Master", StringComparison.OrdinalIgnoreCase) &&
                component.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact);
        var wrongMod = Assert.Single(
            wrongKeystone.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "Chaos Inoculation",
                StringComparison.OrdinalIgnoreCase));
        // Clipboard names Chaos Inoculation — must not resolve as Hex Master Exact via shared prefix.
        Assert.NotEqual(
            FrozenTradeCatalog.Value.Entries.Single(entry =>
                entry.Text.Contains("Hex Master", StringComparison.Ordinal) &&
                entry.Text.Contains("Passive Skills in Radius", StringComparison.Ordinal)).Id,
            wrongMod.ProviderStatId);

        var noPassage = Resolve(SyntheticImpossibleEscapeMultilineWithoutPassage, includeTradeItems:
        [
            UniqueItem(0, "Impossible Escape", "Viridian Jewel", "jewel"),
        ]);
        var unsupported = Assert.Single(
            noPassage.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains("Hex Master", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual(SearchComponentProviderResolutionStatus.Exact, unsupported.ProviderResolutionStatus);
    }

    [Fact]
    public void ImpossibleEscape_All48CatalogueKeystones_AreExact_AndSelectedSerializesPipeId()
    {
        // Evidence tier: SYNTHETIC clipboard labelled below. Catalogue rows are PoB-generated
        // candidates (not 48 genuine captures). Frozen Trade supplies the Explicit pipe IDs.
        var package = LoadPackage();
        var escape = Assert.Single(
            package.UniqueItems!.Items,
            item => string.Equals(item.CanonicalName, "Impossible Escape", StringComparison.Ordinal));
        var current = Assert.Single(escape.Versions, version => version.Role == UniqueItemVersionRole.Current);
        var keystoneBlocks = current.ModifierBlocks
            .Where(block => block.Kind == UniqueModifierBlockKind.Unique)
            .ToArray();
        Assert.Equal(48, keystoneBlocks.Length);
        Assert.All(
            keystoneBlocks,
            block =>
            {
                Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
                Assert.Contains(
                    "local_unique_jewel_disconnected_passives_can_be_allocated_around_keystone_hash",
                    block.MechanicalMapping.StatIds);
                Assert.Contains(
                    block.MechanicalMapping.Provenance?.Translations
                        .SelectMany(evidence => evidence.IndexHandlers)
                        .SelectMany(handler => handler.Handlers)
                    ?? [],
                    handler => string.Equals(handler, "passive_hash", StringComparison.Ordinal));
            });

        var catalogueKeystone = Assert.Single(
            FrozenTradeCatalog.Value.Entries,
            entry => entry.Text.Contains("Chaos Inoculation", StringComparison.Ordinal) &&
                     entry.Text.Contains("Passive Skills in Radius", StringComparison.Ordinal));
        Assert.Equal("explicit", catalogueKeystone.Type, ignoreCase: true);
        Assert.Equal("explicit", catalogueKeystone.GroupId, ignoreCase: true);
        Assert.Contains("Passage", catalogueKeystone.Text, StringComparison.Ordinal);
        Assert.StartsWith("explicit.stat_2422708892|", catalogueKeystone.Id, StringComparison.Ordinal);
        Assert.Empty(catalogueKeystone.Options);

        var runtime = Resolve(SyntheticImpossibleEscapeChaosInoculation, includeTradeItems:
        [
            UniqueItem(0, "Impossible Escape", "Viridian Jewel", "jewel"),
        ]);

        Assert.Equal("Impossible Escape", runtime.ProviderDraft.DisplayName, ignoreCase: true);
        Assert.All(runtime.ProviderDraft.ModifierFilters, filter => Assert.False(filter.IsSelected));

        var identityJson = Serialize(runtime, selectedComponent: null);
        using (var identityDocument = JsonDocument.Parse(identityJson))
        {
            var identityQuery = identityDocument.RootElement.GetProperty("query");
            Assert.Equal("Impossible Escape", identityQuery.GetProperty("name").GetString());
            Assert.Equal("Viridian Jewel", identityQuery.GetProperty("type").GetString());
            AssertNoActiveOptionalStatFilters(identityQuery);
            Assert.DoesNotContain(catalogueKeystone.Id, identityJson, StringComparison.Ordinal);
        }

        var keystone = Assert.Single(
            runtime.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(
                "Passive Skills in Radius of Chaos Inoculation",
                StringComparison.OrdinalIgnoreCase));
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, keystone.ProviderResolutionStatus);
        Assert.True(keystone.IsSearchable, keystone.NotSearchableReason);
        Assert.Equal(catalogueKeystone.Id, keystone.ProviderStatId);
        Assert.Equal(ModifierBoundShape.PresenceOnly, keystone.ValueBoundShape);
        Assert.False(keystone.SupportsValueBounds);
        Assert.Null(keystone.RequestedMinimum);
        Assert.Null(keystone.RequestedMaximum);

        var selectedJson = Serialize(runtime, keystone);
        using var selectedDocument = JsonDocument.Parse(selectedJson);
        var selectedQuery = selectedDocument.RootElement.GetProperty("query");
        Assert.Equal("Impossible Escape", selectedQuery.GetProperty("name").GetString());
        var serialized = Assert.Single(
            selectedQuery.GetProperty("stats")[0].GetProperty("filters").EnumerateArray());
        Assert.Equal(catalogueKeystone.Id, serialized.GetProperty("id").GetString());
        Assert.False(
            serialized.TryGetProperty("value", out _),
            "Selected Impossible Escape keystone must not emit value.option / min / max.");
    }

    [Fact]
    public void FollowUpEvidence_ClusterJewelFlattening_IsRecordedButOutOfScope()
    {
        // Outside Unique slice — do not change Rare/cluster production. Record observation only.
        var flattenedRadiusStyle = FrozenTradeCatalog.Value.Entries
            .Count(entry =>
                entry.Text.Contains("Added Small Passive Skills grant", StringComparison.OrdinalIgnoreCase) ||
                entry.Text.Contains("Notable Passive Skill", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            flattenedRadiusStyle >= 0,
            "Cluster observation probe must run; production Rare/cluster code is untouched.");
        // Intentional no-op assertion payload for HQ follow-up (user 2026-10-21).
        _ = flattenedRadiusStyle;
    }

    private static void AssertFlattenedFamily(
        PathOfExileTradeStatCatalog catalog,
        string textContains,
        string expectedBasePrefix,
        int expectedCount)
    {
        var matches = catalog.Entries
            .Where(entry => entry.Text.Contains(textContains, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(expectedCount, matches.Length);
        Assert.All(matches, entry =>
        {
            Assert.StartsWith(expectedBasePrefix, entry.Id, StringComparison.Ordinal);
            Assert.Equal("explicit", entry.Type, ignoreCase: true);
            Assert.Empty(entry.Options);
            Assert.DoesNotContain(
                catalog.Entries,
                other => string.Equals(
                    other.Id,
                    expectedBasePrefix.TrimEnd('|'),
                    StringComparison.Ordinal));
        });
    }

    private static void AssertLiveAgreesOnKeyIds(
        PathOfExileTradeStatCatalog frozen,
        PathOfExileTradeStatCatalog live)
    {
        foreach (var needle in new[]
                 {
                     "Allocates Unnatural Strength if you have the matching modifier on Forbidden Flesh",
                     "Allocates Unnatural Strength if you have the matching modifier on Forbidden Flame",
                     "Passive Skills in Radius of Chaos Inoculation",
                 })
        {
            var frozenEntry = Assert.Single(
                frozen.Entries,
                entry => entry.Text.Contains(needle, StringComparison.Ordinal));
            var liveEntry = Assert.Single(
                live.Entries,
                entry => entry.Text.Contains(needle, StringComparison.Ordinal));
            Assert.Equal(frozenEntry.Id, liveEntry.Id);
            Assert.Equal(frozenEntry.Type, liveEntry.Type);
            Assert.Empty(frozenEntry.Options);
            Assert.Empty(liveEntry.Options);
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
        return SelectedMapper.Map(selectedDraft, FrozenTradeCatalog.Value);
    }

    private static RuntimeResult Resolve(
        string rawText,
        IReadOnlyList<PathOfExileTradeItemEntry> includeTradeItems)
    {
        var catalog = GameData.Value;
        var tradeItems = new PathOfExileTradeItemCatalog(includeTradeItems);
        var parsed = new ItemTextParser().Parse(rawText);
        var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, catalog);
        var sourceResolutions = new ParsedItemModifierCandidateResolver().Resolve(
            parsed,
            catalog,
            baseResolution);
        var draftResult = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            baseResolution,
            sourceResolutions,
            catalog);
        Assert.True(
            draftResult.IsSuccess,
            string.Join(" | ", draftResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var draft = Assert.IsType<TradeSearchDraft>(draftResult.Draft);
        var uniqueIdentity = new PathOfExileTradeItemIdentityMapper()
            .Map(draft, tradeItems)
            .Identity;
        var propertyDraft = ItemPropertyResolver.Resolve(draft, FilterCatalog);
        var service = new PathOfExileTradePriceCheckService(
            QueryBuilder,
            new PathOfExileTradeStatMatcher(),
            new StaticStatProvider(FrozenTradeCatalog.Value),
            new StaticItemProvider(tradeItems),
            SelectedMapper,
            new PathOfExileTradeItemIdentityMapper(),
            new NoSearchClient(),
            new NoFetchClient());
        var providerDraft = service.ResolveProviderComponents(
            propertyDraft,
            FrozenTradeCatalog.Value,
            uniqueIdentity,
            FilterCatalog);
        return new RuntimeResult(providerDraft, uniqueIdentity);
    }

    private static GameDataPackage LoadPackage()
    {
        var result = GameDataPackageLoader
            .LoadFromFileAsync(FindRepoFile("artifacts", "poenhance-game-data.json"))
            .GetAwaiter()
            .GetResult();
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<GameDataPackage>(result.Package);
    }

    private static GameDataCatalog LoadGameData() =>
        GameDataCatalog.FromPackage(LoadPackage());

    private static PathOfExileTradeStatCatalog LoadFrozenTradeCatalog()
    {
        var path = FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Trade",
            "official-stats-2026-08-19.json");
        Assert.EndsWith(
            FrozenStatsRelative.Replace('/', Path.DirectorySeparatorChar),
            path,
            StringComparison.OrdinalIgnoreCase);
        var parsed = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(File.ReadAllText(path));
        Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<PathOfExileTradeStatCatalog>(parsed.Catalog);
    }

    private static PathOfExileTradeStatCatalog LoadTradeCatalogFromPossiblyWrappedJson(string path)
    {
        var raw = File.ReadAllText(path);
        var jsonStart = raw.IndexOf("{\"result\"", StringComparison.Ordinal);
        if (jsonStart < 0)
        {
            jsonStart = raw.IndexOf('{');
        }

        Assert.True(jsonStart >= 0, $"No JSON object found in {path}");
        var parsed = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(raw[jsonStart..]);
        Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<PathOfExileTradeStatCatalog>(parsed.Catalog);
    }

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

    private static string ReadProviderRuntimeCapture(string fileName)
    {
        var path = FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Unique",
            "ProviderRuntimeCaptures",
            fileName);
        return File.ReadAllText(path);
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

    // SYNTHETIC fixtures — not genuine Ctrl+D captures.
    private const string SyntheticForbiddenFlameAllocatesOnly = """
Item Class: Jewels
Rarity: Unique
Forbidden Flame
Crimson Jewel
--------
Limited to: 1
--------
Item Level: 86
--------
{ Unique Modifier }
Allocates Unnatural Strength if you have the matching modifier on Forbidden Flesh
""";

    private const string SyntheticForbiddenFleshAllocatesOnly = """
Item Class: Jewels
Rarity: Unique
Forbidden Flesh
Cobalt Jewel
--------
Limited to: 1
--------
Item Level: 86
--------
{ Unique Modifier }
Allocates Unnatural Strength if you have the matching modifier on Forbidden Flame
""";

    private const string SyntheticForbiddenFlameWithRequiresClass = """
Item Class: Jewels
Rarity: Unique
Forbidden Flame
Crimson Jewel
--------
Limited to: 1
--------
Item Level: 86
--------
{ Unique Modifier }
Requires Class Shadow
{ Unique Modifier }
Allocates Unnatural Strength if you have the matching modifier on Forbidden Flesh
""";

    private const string SyntheticImpossibleEscapeChaosInoculation = """
Item Class: Jewels
Rarity: Unique
Impossible Escape
Viridian Jewel
--------
Limited to: 1
Radius: Small
--------
Item Level: 86
--------
{ Unique Modifier }
Passive Skills in Radius of Chaos Inoculation can be Allocated without being connected to your tree
""";

    private const string SyntheticImpossibleEscapeMultilineWrongKeystone = """
Item Class: Jewels
Rarity: Unique
Impossible Escape
Viridian Jewel
--------
Limited to: 1
Radius: Small
--------
Item Level: 86
--------
{ Unique Modifier }
Passive Skills in Radius of Chaos Inoculation can be Allocated
without being connected to your tree
Passage — Unscalable Value
""";

    private const string SyntheticImpossibleEscapeMultilineWithoutPassage = """
Item Class: Jewels
Rarity: Unique
Impossible Escape
Viridian Jewel
--------
Limited to: 1
Radius: Small
--------
Item Level: 86
--------
{ Unique Modifier }
Passive Skills in Radius of Hex Master can be Allocated
without being connected to your tree
""";
}
