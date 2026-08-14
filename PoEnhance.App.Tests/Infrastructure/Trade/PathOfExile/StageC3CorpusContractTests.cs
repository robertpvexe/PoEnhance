using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.App.Infrastructure.Settings;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class StageC3CorpusContractTests
{
    [Fact]
    public async Task UpdatedRealCorpus_EveryVisibleRowSatisfiesRuntimeContract()
    {
        var liveDirectory = Environment.GetEnvironmentVariable("POENHANCE_STAGE_C_LIVE_DATA_DIR");
        var reportPath = Environment.GetEnvironmentVariable("POENHANCE_STAGE_C3_REPORT_PATH");
        var doublePassReportPath = Environment.GetEnvironmentVariable(
            "POENHANCE_STAGE_C3_DOUBLE_PASS_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(liveDirectory) || string.IsNullOrWhiteSpace(reportPath))
        {
            return;
        }

        var corpusPath = Environment.GetEnvironmentVariable("POENHANCE_STAGE_C_CORPUS_PATH") ??
            @"D:\Projects\zfortests_v2.txt";
        var gameDataPath = Environment.GetEnvironmentVariable("POENHANCE_STAGE_C_GAME_DATA_PATH") ??
            FindRepoFile("artifacts", "poenhance-game-data.json");
        var gameDataLoad = await GameDataPackageLoader.LoadFromFileAsync(gameDataPath);
        Assert.True(gameDataLoad.IsSuccess);
        var gameData = GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(gameDataLoad.Package));
        var statCatalog = ParseStats(Path.Combine(liveDirectory, "stats.json"));
        var filterCatalog = ParseFilters(Path.Combine(liveDirectory, "filters.json"));
        var itemCatalog = ParseItems(Path.Combine(liveDirectory, "items.json"));
        var corpus = SplitCorpus(File.ReadAllText(corpusPath));

        var parser = new ItemTextParser();
        var displayService = new ParsedItemGameDataDisplayService();
        var draftMapper = new TradeSearchDraftMapper();
        var validator = new TradeSearchDraftValidator();
        var propertyResolver = new PathOfExileTradeItemPropertyResolver();
        var selectedMapper = new PathOfExileTradeSelectedModifierMapper();
        var identityMapper = new PathOfExileTradeItemIdentityMapper();
        var queryBuilder = new PathOfExileTradeQueryBuilder();
        var providerService = CreatePriceCheckService(statCatalog, itemCatalog, filterCatalog);
        var controllerService = new RecordingPriceCheckService(providerService);
        var controller = new PriceCheckerSearchController(
            controllerService,
            ApplicationLeagueSetting.CreateTransient("Allflame"),
            new TestTradeLeagueResolver());
        var rows = new List<CorpusRow>();
        var doublePassTransitions = new List<DoublePassTransition>();

        foreach (var rawText in corpus)
        {
            var parsed = parser.Parse(UseTrueAdvancedPresenceShapeForTargetedRegression(rawText));
            var baseResolution = displayService.ResolveItemBase(parsed, gameData).Result;
            var modifierResolutions = displayService.ResolveModifierCandidates(parsed, gameData, baseResolution)
                .Results.Select(display => display.Result)
                .OfType<ModifierCandidateResolutionResult>()
                .ToArray();
            var draftResult = draftMapper.CreateDraft(parsed, baseResolution, modifierResolutions, gameData);
            Assert.True(draftResult.IsSuccess);
            var draft = Assert.IsType<TradeSearchDraft>(draftResult.Draft);
            if (string.Equals(parsed.DisplayName, "Megalomaniac", StringComparison.Ordinal))
            {
                var literalNotables = draft.ModifierFilters
                    .Where(component => component.OriginalText.StartsWith(
                        "1 Added Passive Skill is ",
                        StringComparison.Ordinal))
                    .ToArray();
                Assert.Equal(3, literalNotables.Length);
                Assert.All(literalNotables, component =>
                {
                    Assert.Equal(ModifierBoundShape.PresenceOnly, component.ValueBoundShape);
                    Assert.False(component.SupportsValueBounds);
                    Assert.Null(component.RequestedMinimum);
                    Assert.Null(component.RequestedMaximum);
                });
            }
            var identity = identityMapper.Map(draft, itemCatalog).Identity;
            var firstPassDraft = await controller.PrepareDraftAsync(draft);
            controller.UpdateCurrentDraft(firstPassDraft, validator.Validate(firstPassDraft));
            var providerDraft = Assert.IsType<TradeSearchDraft>(controllerService.LastResolvedDraft);
            var itemName = parsed.DisplayName ?? parsed.BaseType ?? "<unknown>";
            for (var index = 0; index < firstPassDraft.ModifierFilters.Count; index++)
            {
                var first = DoublePassComponentSnapshot.From(firstPassDraft.ModifierFilters[index]);
                var second = DoublePassComponentSnapshot.From(providerDraft.ModifierFilters[index]);
                if (first != second)
                {
                    doublePassTransitions.Add(new DoublePassTransition(
                        itemName,
                        firstPassDraft.ModifierFilters[index].OriginalText,
                        first,
                        second,
                        first.ProviderStatus == SearchComponentProviderResolutionStatus.Exact.ToString() &&
                            first.StatMappingProof == ModifierStatMappingProofStatus.ProviderExact.ToString()
                            ? "UnsafeExactProofMutation"
                            : second.ProviderStatus == SearchComponentProviderResolutionStatus.Exact.ToString()
                                ? "ReverseTransition"
                                : "OtherMutation"));
                }
            }
            var view = controller.CurrentViewState;
            var modifierRows = view.Modifiers
                .Concat(view.ItemProperties.SelectMany(property => property.Children))
                .GroupBy(row => row.SourceIndex)
                .ToDictionary(group => group.Key, group => group.First());
            for (var index = 0; index < providerDraft.ItemProperties.Length; index++)
            {
                var property = providerDraft.ItemProperties[index];
                var row = view.ItemProperties[index];
                var selectedDraft = providerDraft with
                {
                    ItemProperties = providerDraft.ItemProperties
                        .Select((candidate, candidateIndex) => candidate with
                        {
                            IsSelected = candidateIndex == index,
                        })
                        .ToImmutableArray(),
                    ModifierFilters = providerDraft.ModifierFilters
                        .Select(component => component with { IsSelected = false })
                        .ToArray(),
                };
                var mapping = propertyResolver.MapSelected(selectedDraft, filterCatalog);
                var build = mapping.IsSuccess
                    ? queryBuilder.Build(
                        selectedDraft,
                        validator.Validate(selectedDraft),
                        "Allflame",
                        [],
                        identity,
                        filterCatalog,
                        mapping.Filters)
                    : null;
                var unsafeQuery = mapping.IsSuccess && build?.IsSuccess == true &&
                    mapping.Filters.Any(filter =>
                        !ContainsJsonString(build.SerializedJson, filter.ProviderGroupId) ||
                        !ContainsJsonString(build.SerializedJson, filter.ProviderFilterId));
                var state = row.IsAvailable
                    ? !mapping.IsSuccess || build?.IsSuccess != true
                        ? "SELECTABLE_BUT_LATE_REJECT"
                        : unsafeQuery ? "SELECTABLE_BUT_UNSAFE_QUERY" : "SELECTABLE_SAFE"
                    : string.IsNullOrWhiteSpace(row.AvailabilityReason)
                        ? "DISABLED_WITHOUT_VISIBLE_REASON"
                        : property.ProviderResolutionStatus ==
                            TradeSearchItemPropertyProviderResolutionStatus.Ambiguous
                            ? "DISABLED_EXPLICIT_AMBIGUOUS"
                            : "DISABLED_EXPLICIT_UNSUPPORTED";
                rows.Add(new CorpusRow(
                    itemName,
                    parsed.BaseType,
                    "property",
                    property.Label,
                    null,
                    null,
                    null,
                    [],
                    [],
                    state,
                    property.ProviderResolutionStatus.ToString(),
                    null,
                    property.NotSearchableReason ?? property.DerivationUnsupportedReason,
                    row.IsAvailable ? null : property.ProviderResolutionStatus ==
                        TradeSearchItemPropertyProviderResolutionStatus.Ambiguous ? "Ambiguous" : "Unsupported",
                    row.AvailabilityReason,
                    mapping.Filters.FirstOrDefault()?.ProviderFilterId,
                    [],
                    [],
                    null,
                    identity?.CanonicalName,
                    identity?.CanonicalType,
                    mapping.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}").ToArray(),
                    build?.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}").ToArray() ?? [],
                    null,
                    [],
                    []));
            }

            for (var index = 0; index < providerDraft.ModifierFilters.Count; index++)
            {
                var component = providerDraft.ModifierFilters[index];
                modifierRows.TryGetValue(index, out var row);
                var parserFalse = IsParserFalseRow(parsed, component);
                var selectedDraft = providerDraft with
                {
                    ItemProperties = providerDraft.ItemProperties
                        .Select(property => property with { IsSelected = false })
                        .ToImmutableArray(),
                    ModifierFilters = providerDraft.ModifierFilters
                        .Select((candidate, candidateIndex) => candidate with
                        {
                            IsSelected = candidateIndex == index,
                        })
                        .ToArray(),
                };
                var mapping = selectedMapper.Map(selectedDraft, statCatalog);
                var build = mapping.IsSuccess
                    ? queryBuilder.Build(
                        selectedDraft,
                        validator.Validate(selectedDraft),
                        "Allflame",
                        mapping.Filters,
                        identity,
                        filterCatalog,
                        [])
                    : null;
                var unsafeQuery = mapping.IsSuccess && build?.IsSuccess == true &&
                    (mapping.Filters.SelectMany(FilterStatIds)
                        .Any(statId => !ContainsJsonString(build.SerializedJson, statId)) ||
                    component.ValueBoundShape == ModifierBoundShape.PresenceOnly &&
                    (mapping.Filters.Any(filter =>
                        filter.Minimum is not null ||
                        filter.Maximum is not null ||
                        filter.Alternatives.Any(alternative =>
                            alternative.Minimum is not null || alternative.Maximum is not null)) ||
                    mapping.Filters.SelectMany(FilterStatIds)
                        .Any(statId => QueryStatHasValue(build.SerializedJson, statId))));
                var selectable = row?.IsInteractionEnabled == true;
                var state = parserFalse
                    ? "PARSER_FALSE_ROW"
                    : row is null
                        ? "HIDDEN"
                        : selectable
                            ? !mapping.IsSuccess || build?.IsSuccess != true
                                ? "SELECTABLE_BUT_LATE_REJECT"
                                : unsafeQuery ? "SELECTABLE_BUT_UNSAFE_QUERY" : "SELECTABLE_SAFE"
                            : HasVisibleDisabledDiagnostic(row)
                                ? string.Equals(row.AvailabilityStatus, "Ambiguous", StringComparison.Ordinal)
                                    ? "DISABLED_EXPLICIT_AMBIGUOUS"
                                    : "DISABLED_EXPLICIT_UNSUPPORTED"
                                : "DISABLED_WITHOUT_VISIBLE_REASON";
                rows.Add(new CorpusRow(
                    itemName,
                    parsed.BaseType,
                    "modifier",
                    component.OriginalText,
                    component.PresentationText,
                    component.CanonicalSignature,
                    component.ProviderCanonicalSignature,
                    component.ObservedNumericValues,
                    component.CanonicalNumericValues,
                    state,
                    component.ProviderResolutionStatus.ToString(),
                    component.ProviderDiagnosticCode ?? component.UniqueResolutionDiagnosticCode,
                    component.ProviderDiagnosticMessage ?? component.NotSearchableReason,
                    row?.AvailabilityStatus,
                    row?.AvailabilityReason,
                    component.ProviderStatId,
                    component.ProviderStatAlternativeIds,
                    component.FilterVariants.Select(variant => variant.Label).ToArray(),
                    component.ValueBoundShape.ToString(),
                    identity?.CanonicalName,
                    identity?.CanonicalType,
                    mapping.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}").ToArray(),
                    build?.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}").ToArray() ?? [],
                    component.UniqueOrigin.ToString(),
                    component.UniqueFoulbornRelationshipIds,
                    component.UniqueNormalCounterpartModifierIds));
            }
        }

        var visibleRows = rows.Where(row => row.State != "HIDDEN").ToArray();
        var stateCounts = visibleRows
            .GroupBy(row => row.State, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var unsupportedReasons = visibleRows
            .Where(row => row.State is "DISABLED_EXPLICIT_UNSUPPORTED" or "DISABLED_EXPLICIT_AMBIGUOUS")
            .GroupBy(ClassifyUnsupported, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var report = new CorpusReport(corpus.Count, visibleRows.Length, stateCounts, unsupportedReasons, visibleRows);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
        if (!string.IsNullOrWhiteSpace(doublePassReportPath))
        {
            File.WriteAllText(
                doublePassReportPath,
                JsonSerializer.Serialize(doublePassTransitions, new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));
        }

        Assert.Equal(0, Count(stateCounts, "DISABLED_WITHOUT_VISIBLE_REASON"));
        Assert.Equal(0, Count(stateCounts, "PARSER_FALSE_ROW"));
        Assert.Equal(0, Count(stateCounts, "SELECTABLE_BUT_LATE_REJECT"));
        Assert.Equal(0, Count(stateCounts, "SELECTABLE_BUT_UNSAFE_QUERY"));
        Assert.DoesNotContain(doublePassTransitions, transition =>
            transition.Classification == "UnsafeExactProofMutation");
        var reverseTransition = Assert.Single(doublePassTransitions, transition =>
            transition.Classification == "ReverseTransition");
        Assert.Equal("Skull Band", reverseTransition.Item);
        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported.ToString(),
            reverseTransition.FirstPass.ProviderStatus);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact.ToString(),
            reverseTransition.SecondPass.ProviderStatus);
        Assert.Equal("fractured", reverseTransition.SecondPass.RequestedVariantKind);
        var diagnosticCleanup = Assert.Single(doublePassTransitions, transition =>
            transition.Classification == "OtherMutation");
        Assert.Equal("Corruption Bond", diagnosticCleanup.Item);
        Assert.Equal(diagnosticCleanup.FirstPass.ProviderStatus, diagnosticCleanup.SecondPass.ProviderStatus);
        Assert.Equal(diagnosticCleanup.FirstPass.ProviderStatId, diagnosticCleanup.SecondPass.ProviderStatId);
        Assert.NotNull(diagnosticCleanup.FirstPass.DiagnosticCode);
        Assert.Null(diagnosticCleanup.SecondPass.DiagnosticCode);
        AssertItemStateCounts(rows, "Megalomaniac", ("SELECTABLE_SAFE", 5));
        Assert.Equal(3, rows.Count(row =>
            row.Item == "Megalomaniac" &&
            row.Text.StartsWith("1 Added Passive Skill is ", StringComparison.Ordinal) &&
            row.State == "SELECTABLE_SAFE" &&
            row.ProviderStatus == SearchComponentProviderResolutionStatus.Exact.ToString() &&
            row.ValueShape == ModifierBoundShape.PresenceOnly.ToString()));
        foreach (var (item, text) in new[]
        {
            ("Hypnotic Shine", "1 Added Passive Skill is Vile Reinvigoration"),
            ("Hypnotic Shine", "1 Added Passive Skill is Exposure Therapy"),
            ("Kraken Star", "1 Added Passive Skill is Burden Projection"),
        })
        {
            var literalPresence = Assert.Single(rows, row =>
                row.Item == item && string.Equals(row.Text, text, StringComparison.Ordinal));
            Assert.Equal("SELECTABLE_SAFE", literalPresence.State);
            Assert.Equal(
                SearchComponentProviderResolutionStatus.Exact.ToString(),
                literalPresence.ProviderStatus);
            Assert.Equal(ModifierBoundShape.PresenceOnly.ToString(), literalPresence.ValueShape);
            Assert.NotNull(literalPresence.ProviderStatId);
            Assert.Null(literalPresence.DiagnosticCode);
        }
        AssertItemStateCounts(
            rows,
            "Yriel's Fostering",
            ("SELECTABLE_SAFE", 6),
            ("DISABLED_EXPLICIT_AMBIGUOUS", 2));
        Assert.All(
            rows.Where(row => row.Item is "The Squire" or "Progenesis"),
            row => Assert.Equal("SELECTABLE_SAFE", row.State));
        AssertItemStateCounts(rows, "Replica Alberon's Warpath", ("SELECTABLE_SAFE", 8));
        AssertItemStateCounts(rows, "Replica Dragonfang's Flight", ("SELECTABLE_SAFE", 5));
        AssertItemStateCounts(rows, "Last Resort", ("SELECTABLE_SAFE", 10));
        var bringerReplacement = Assert.Single(rows, row =>
            row.Item == "Foulborn The Bringer of Rain" &&
            row.Text.Contains("Sadism", StringComparison.Ordinal));
        Assert.Equal("SELECTABLE_SAFE", bringerReplacement.State);
        Assert.Equal(
            SearchComponentProviderResolutionStatus.ExactEquivalentSet.ToString(),
            bringerReplacement.ProviderStatus);
        Assert.Null(bringerReplacement.DiagnosticCode);
        var greenDreamReplacement = Assert.Single(rows, row =>
            row.Item == "Foulborn The Green Dream" &&
            row.Text.Contains("Lucky", StringComparison.Ordinal));
        Assert.Equal("SELECTABLE_SAFE", greenDreamReplacement.State);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact.ToString(),
            greenDreamReplacement.ProviderStatus);
        Assert.Null(greenDreamReplacement.DiagnosticCode);
        Assert.All(rows.Where(row => row.Item.StartsWith("Foulborn ", StringComparison.Ordinal)), row =>
        {
            Assert.DoesNotContain("Foulborn ", row.CanonicalItemName ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(row.State, new[]
            {
                "DISABLED_WITHOUT_VISIBLE_REASON",
                "SELECTABLE_BUT_LATE_REJECT",
                "SELECTABLE_BUT_UNSAFE_QUERY",
            });
        });
    }

    private static bool HasVisibleDisabledDiagnostic(PriceCheckerModifierViewModel row) =>
        !string.IsNullOrWhiteSpace(row.AvailabilityReason) &&
        !string.IsNullOrWhiteSpace(row.AvailabilityStatus) &&
        (row.SectionLabel.Contains(row.AvailabilityStatus, StringComparison.OrdinalIgnoreCase) ||
            row.ModTypeLabel.Contains(row.AvailabilityStatus, StringComparison.OrdinalIgnoreCase));

    private static string UseTrueAdvancedPresenceShapeForTargetedRegression(string rawText)
    {
        if (!Regex.IsMatch(rawText, @"(?m)^Megalomaniac\r?$"))
        {
            return rawText;
        }

        foreach (var notable in new[] { "Antifreeze", "Overshock", "Wound Aggravation" })
        {
            var text = $"1 Added Passive Skill is {notable}";
            if (!rawText.Contains($"{text} — Unscalable Value", StringComparison.Ordinal))
            {
                rawText = rawText.Replace(
                    text,
                    $"{text} — Unscalable Value",
                    StringComparison.Ordinal);
            }
        }

        return rawText;
    }

    private static bool IsParserFalseRow(ParsedItem parsed, ResolvedSearchComponent component)
    {
        var indexes = component.Sources.Count == 0
            ? [component.SourceModifierIndex]
            : component.Sources.Select(source => source.SourceModifierIndex).ToArray();
        return parsed.InputFormat == ParsedItemInputFormat.Advanced && indexes.Any(index =>
            index >= 0 && index < parsed.Modifiers.Count &&
            string.IsNullOrWhiteSpace(parsed.Modifiers[index].RawMetadataLine));
    }

    private static IEnumerable<string> FilterStatIds(PathOfExileTradeSelectedModifierFilter filter) =>
        filter.Alternatives.Count > 0
            ? filter.Alternatives.Select(alternative => alternative.StatId)
            : [filter.StatId];

    private static bool ContainsJsonString(string? json, string value) =>
        json?.Contains($"\"{value}\"", StringComparison.Ordinal) == true;

    private static bool QueryStatHasValue(string? json, string statId)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement
            .GetProperty("query")
            .GetProperty("stats")
            .EnumerateArray()
            .SelectMany(group => group.GetProperty("filters").EnumerateArray())
            .Any(filter =>
                string.Equals(filter.GetProperty("id").GetString(), statId, StringComparison.Ordinal) &&
                filter.TryGetProperty("value", out _));
    }

    private static void AssertItemStateCounts(
        IReadOnlyList<CorpusRow> rows,
        string item,
        params (string State, int Count)[] expected)
    {
        var itemRows = rows.Where(row => row.Item == item).ToArray();
        Assert.Equal(expected.Sum(entry => entry.Count), itemRows.Length);
        foreach (var (state, count) in expected)
        {
            Assert.Equal(count, itemRows.Count(row => row.State == state));
        }
    }

    private static int Count(IReadOnlyDictionary<string, int> counts, string state) =>
        counts.TryGetValue(state, out var count) ? count : 0;

    private static string ClassifyUnsupported(CorpusRow row)
    {
        var diagnostic = $"{row.DiagnosticCode} {row.DiagnosticReason}";
        if (diagnostic.Contains("FOULBORN_REPLACEMENT", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("Foulborn replacement", StringComparison.OrdinalIgnoreCase))
        {
            return "FOULBORN_REPLACEMENT_EVIDENCE_MISSING";
        }
        if (diagnostic.Contains("generated", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("support-gem", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("annotation", StringComparison.OrdinalIgnoreCase))
        {
            return "GENERATED_EVIDENCE_GAP";
        }
        if (diagnostic.Contains("not present in every retained compatible version", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("VERSION", StringComparison.OrdinalIgnoreCase))
        {
            return "VERSION_AMBIGUOUS";
        }
        if (diagnostic.Contains("INDEPENDENT_DIMENSIONS", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("one editable Trade bound", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("faithful numeric projection", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("VALUE", StringComparison.OrdinalIgnoreCase) &&
                (diagnostic.Contains("projection", StringComparison.OrdinalIgnoreCase) ||
                    diagnostic.Contains("numeric", StringComparison.OrdinalIgnoreCase) ||
                    diagnostic.Contains("bound", StringComparison.OrdinalIgnoreCase)))
        {
            return "VALUE_PROJECTION_GAP";
        }
        if (row.ProviderStatus == "Ambiguous" ||
            diagnostic.Contains("STAT_MATCH_AMBIGUOUS", StringComparison.OrdinalIgnoreCase))
        {
            return "PROVIDER_AMBIGUOUS_OR_UNPROVEN";
        }
        if (diagnostic.Contains("STAT_MATCH_NO_CANDIDATE", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("MODIFIER_KIND_MISMATCH", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("VARIANT_UNAVAILABLE", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("does not expose", StringComparison.OrdinalIgnoreCase))
        {
            return "PROVIDER_ABSENT_OR_UNPROVEN";
        }
        if (diagnostic.Contains("mechanic", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("GameData", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase))
        {
            return "MECHANICS_UNRESOLVED";
        }
        if (row.ProviderStatus is "NotFound" or "Unsupported")
        {
            return "PROVIDER_ABSENT_OR_UNPROVEN";
        }
        return "OTHER_EXPLICIT";
    }

    private static PathOfExileTradeStatCatalog ParseStats(string path)
    {
        var result = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(File.ReadAllText(path));
        Assert.True(result.IsSuccess);
        return Assert.IsType<PathOfExileTradeStatCatalog>(result.Catalog);
    }

    private static PathOfExileTradeFilterCatalog ParseFilters(string path)
    {
        var result = new PathOfExileTradeFiltersResponseParser().ParseFiltersResponse(File.ReadAllText(path));
        Assert.True(result.IsSuccess);
        return Assert.IsType<PathOfExileTradeFilterCatalog>(result.Catalog);
    }

    private static PathOfExileTradeItemCatalog ParseItems(string path)
    {
        var result = new PathOfExileTradeItemsResponseParser().ParseItemsResponse(File.ReadAllText(path));
        Assert.True(result.IsSuccess);
        return Assert.IsType<PathOfExileTradeItemCatalog>(result.Catalog);
    }

    private static IReadOnlyList<string> SplitCorpus(string text) => new Regex(
            @"\r?\n\s*\r?\n(?=Item Class:)",
            RegexOptions.CultureInvariant)
        .Split(text.TrimEnd('\r', '\n'))
        .Where(block => !string.IsNullOrWhiteSpace(block))
        .ToArray();

    private static PathOfExileTradePriceCheckService CreatePriceCheckService(
        PathOfExileTradeStatCatalog statCatalog,
        PathOfExileTradeItemCatalog itemCatalog,
        PathOfExileTradeFilterCatalog filterCatalog) => new(
        new PathOfExileTradeQueryBuilder(),
        new PathOfExileTradeStatMatcher(),
        new StaticStatProvider(statCatalog),
        new StaticItemProvider(itemCatalog),
        new PathOfExileTradeSelectedModifierMapper(),
        new PathOfExileTradeItemIdentityMapper(),
        new NoSearchClient(),
        new NoFetchClient(),
        new StaticFilterProvider(filterCatalog));

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
        throw new FileNotFoundException(Path.Combine(relativeParts));
    }

    private sealed record CorpusReport(
        int ItemCount,
        int VisibleRows,
        IReadOnlyDictionary<string, int> StateCounts,
        IReadOnlyDictionary<string, int> UnsupportedReasons,
        IReadOnlyList<CorpusRow> Rows);

    private sealed record CorpusRow(
        string Item,
        string? BaseType,
        string RowType,
        string Text,
        string? PresentationText,
        string? CanonicalSignature,
        string? ProviderCanonicalSignature,
        IReadOnlyList<decimal> ObservedNumericValues,
        IReadOnlyList<decimal> CanonicalNumericValues,
        string State,
        string ProviderStatus,
        string? DiagnosticCode,
        string? DiagnosticReason,
        string? VisibleStatus,
        string? VisibleReason,
        string? ProviderStatId,
        IReadOnlyList<string> ProviderAlternativeIds,
        IReadOnlyList<string> VariantLabels,
        string? ValueShape,
        string? CanonicalItemName,
        string? CanonicalItemType,
        IReadOnlyList<string> MapperDiagnostics,
        IReadOnlyList<string> QueryDiagnostics,
        string? UniqueOrigin,
        IReadOnlyList<string> FoulbornRelationshipIds,
        IReadOnlyList<string> NormalCounterpartModifierIds);

    private sealed record DoublePassTransition(
        string Item,
        string Text,
        DoublePassComponentSnapshot FirstPass,
        DoublePassComponentSnapshot SecondPass,
        string Classification);

    private sealed record DoublePassComponentSnapshot(
        string ProviderStatus,
        string? ProviderStatId,
        string StatMappingProof,
        string? SelectedVariantIdentity,
        string? RequestedVariantIdentity,
        string? RequestedVariantKind,
        string? SelectedProviderKind,
        string ProviderDomains,
        string ValueBoundShape,
        bool SupportsValueBounds,
        decimal? RequestedMinimum,
        decimal? RequestedMaximum,
        bool IsSearchable,
        string? DiagnosticCode,
        string? DiagnosticMessage)
    {
        public static DoublePassComponentSnapshot From(ResolvedSearchComponent component) => new(
            component.ProviderResolutionStatus.ToString(),
            component.ProviderStatId,
            component.StatMappingProof.ToString(),
            component.SelectedFilterVariantIdentity,
            component.RequestedFilterVariantIdentity,
            component.RequestedFilterVariantKind,
            component.FilterVariants.FirstOrDefault(variant => string.Equals(
                variant.Identity,
                component.SelectedFilterVariantIdentity,
                StringComparison.Ordinal))?.ProviderKind,
            string.Join(",", component.Sources
                .Select(source => source.ProviderDomain)
                .Where(domain => !string.IsNullOrWhiteSpace(domain))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase)),
            component.ValueBoundShape.ToString(),
            component.SupportsValueBounds,
            component.RequestedMinimum,
            component.RequestedMaximum,
            component.IsSearchable,
            component.ProviderDiagnosticCode,
            component.ProviderDiagnosticMessage);
    }

    private sealed class RecordingPriceCheckService(
        PathOfExileTradePriceCheckService inner) : IPathOfExileTradePriceCheckService
    {
        public TradeSearchDraft? LastResolvedDraft { get; private set; }

        public Task<PathOfExileTradeFilterCatalogProviderResult> InitializeFilterCatalogAsync(
            CancellationToken cancellationToken = default) =>
            inner.InitializeFilterCatalogAsync(cancellationToken);

        public TradeSearchDraft ResolveEffectiveDraft(TradeSearchDraft draft)
        {
            LastResolvedDraft = inner.ResolveEffectiveDraft(draft);
            return LastResolvedDraft;
        }

        public Task<TradeSearchDraft> PrepareEffectiveDraftAsync(
            TradeSearchDraft draft,
            CancellationToken cancellationToken = default) =>
            inner.PrepareEffectiveDraftAsync(draft, cancellationToken);

        public Task<string?> LoadCategoryDisplayLabelAsync(
            TradeSearchDraft draft,
            CancellationToken cancellationToken = default) =>
            inner.LoadCategoryDisplayLabelAsync(draft, cancellationToken);

        public Task<PathOfExileTradePriceCheckResult> CheckAsync(
            TradeSearchDraft? draft,
            TradeSearchValidationResult? validationResult,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default) =>
            inner.CheckAsync(draft, validationResult, leagueIdentifier, cancellationToken);

        public Task<PathOfExileTradePriceCheckResult> FetchMoreAsync(
            string? searchQueryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default) =>
            inner.FetchMoreAsync(searchQueryId, resultIds, cancellationToken);
    }

    private sealed class StaticStatProvider(PathOfExileTradeStatCatalog catalog) :
        IPathOfExileTradeStatCatalogProvider
    {
        public bool TryGetCachedCatalog(out PathOfExileTradeStatCatalog cachedCatalog)
        {
            cachedCatalog = catalog;
            return true;
        }

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

    private sealed class StaticFilterProvider(PathOfExileTradeFilterCatalog catalog) :
        IPathOfExileTradeFilterCatalogProvider
    {
        public bool TryGetCachedCatalog(out PathOfExileTradeFilterCatalog cachedCatalog)
        {
            cachedCatalog = catalog;
            return true;
        }

        public Task<PathOfExileTradeFilterCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PathOfExileTradeFilterCatalogProviderResult.Success(catalog));
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
