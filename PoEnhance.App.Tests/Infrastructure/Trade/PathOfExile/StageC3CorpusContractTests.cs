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
        var providerService = CreatePriceCheckService(statCatalog, itemCatalog);
        var controller = new PriceCheckerSearchController(
            new AcceptancePriceCheckService(),
            ApplicationLeagueSetting.CreateTransient("Allflame"),
            new TestTradeLeagueResolver());
        var rows = new List<CorpusRow>();

        foreach (var rawText in corpus)
        {
            var parsed = parser.Parse(rawText);
            var baseResolution = displayService.ResolveItemBase(parsed, gameData).Result;
            var modifierResolutions = displayService.ResolveModifierCandidates(parsed, gameData, baseResolution)
                .Results.Select(display => display.Result)
                .OfType<ModifierCandidateResolutionResult>()
                .ToArray();
            var draftResult = draftMapper.CreateDraft(parsed, baseResolution, modifierResolutions, gameData);
            Assert.True(draftResult.IsSuccess);
            var draft = Assert.IsType<TradeSearchDraft>(draftResult.Draft);
            var identity = identityMapper.Map(draft, itemCatalog).Identity;
            var propertyDraft = propertyResolver.Resolve(draft, filterCatalog);
            var providerDraft = providerService.ResolveProviderComponents(
                propertyDraft,
                statCatalog,
                identity,
                filterCatalog);
            controller.UpdateCurrentDraft(providerDraft, validator.Validate(providerDraft));
            var view = controller.CurrentViewState;
            var modifierRows = view.Modifiers
                .Concat(view.ItemProperties.SelectMany(property => property.Children))
                .GroupBy(row => row.SourceIndex)
                .ToDictionary(group => group.Key, group => group.First());
            var itemName = parsed.DisplayName ?? parsed.BaseType ?? "<unknown>";

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
                    build?.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}").ToArray() ?? []));
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
                    mapping.Filters.SelectMany(FilterStatIds)
                        .Any(statId => !ContainsJsonString(build.SerializedJson, statId));
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
                    build?.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}").ToArray() ?? []));
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

        Assert.Equal(0, Count(stateCounts, "DISABLED_WITHOUT_VISIBLE_REASON"));
        Assert.Equal(0, Count(stateCounts, "PARSER_FALSE_ROW"));
        Assert.Equal(0, Count(stateCounts, "SELECTABLE_BUT_LATE_REJECT"));
        Assert.Equal(0, Count(stateCounts, "SELECTABLE_BUT_UNSAFE_QUERY"));
    }

    private static bool HasVisibleDisabledDiagnostic(PriceCheckerModifierViewModel row) =>
        !string.IsNullOrWhiteSpace(row.AvailabilityReason) &&
        !string.IsNullOrWhiteSpace(row.AvailabilityStatus) &&
        (row.SectionLabel.Contains(row.AvailabilityStatus, StringComparison.OrdinalIgnoreCase) ||
            row.ModTypeLabel.Contains(row.AvailabilityStatus, StringComparison.OrdinalIgnoreCase));

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
        PathOfExileTradeItemCatalog itemCatalog) => new(
        new PathOfExileTradeQueryBuilder(),
        new PathOfExileTradeStatMatcher(),
        new StaticStatProvider(statCatalog),
        new StaticItemProvider(itemCatalog),
        new PathOfExileTradeSelectedModifierMapper(),
        new PathOfExileTradeItemIdentityMapper(),
        new NoSearchClient(),
        new NoFetchClient());

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
        IReadOnlyList<string> QueryDiagnostics);

    private sealed class AcceptancePriceCheckService : IPathOfExileTradePriceCheckService
    {
        public Task<PathOfExileTradeFilterCatalogProviderResult> InitializeFilterCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PathOfExileTradeFilterCatalogProviderResult());

        public TradeSearchDraft ResolveEffectiveDraft(TradeSearchDraft draft) => draft;

        public Task<string?> LoadCategoryDisplayLabelAsync(
            TradeSearchDraft draft,
            CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task<PathOfExileTradePriceCheckResult> CheckAsync(
            TradeSearchDraft? draft,
            TradeSearchValidationResult? validationResult,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PathOfExileTradePriceCheckResult
            {
                IsSuccess = true,
                Stage = PathOfExileTradePriceCheckStage.Completed,
                EffectiveDraft = draft,
            });

        public Task<PathOfExileTradePriceCheckResult> FetchMoreAsync(
            string? searchQueryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

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
