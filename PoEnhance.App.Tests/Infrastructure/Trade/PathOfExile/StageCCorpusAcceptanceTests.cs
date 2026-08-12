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

public sealed class StageCCorpusAcceptanceTests
{
    [Fact]
    public async Task RealCorpus_ControllerRowsMapAndSerializeWithoutLateRejection()
    {
        var liveDirectory = Environment.GetEnvironmentVariable("POENHANCE_STAGE_C_LIVE_DATA_DIR");
        var reportPath = Environment.GetEnvironmentVariable("POENHANCE_STAGE_C_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(liveDirectory) || string.IsNullOrWhiteSpace(reportPath))
        {
            return;
        }

        var baseline = string.Equals(
            Environment.GetEnvironmentVariable("POENHANCE_STAGE_C_BASELINE"),
            "1",
            StringComparison.Ordinal);
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
        var itemIdentityMapper = new PathOfExileTradeItemIdentityMapper();
        var queryBuilder = new PathOfExileTradeQueryBuilder();
        var priceCheckService = CreatePriceCheckService(statCatalog, itemCatalog);
        var controllerService = new AcceptanceControllerPriceCheckService();
        var controller = new PriceCheckerSearchController(
            controllerService,
            ApplicationLeagueSetting.CreateTransient("Allflame"),
            new TestTradeLeagueResolver());

        var totalRows = 0;
        var selectableRows = 0;
        var propertyRows = 0;
        var selectablePropertyRows = 0;
        var reconstructionFallbackRows = 0;
        var propertyRowsBlockedByReconstruction = 0;
        var unsupportedUniqueRows = 0;
        var tradeSafeUnsupportedRows = 0;
        var lateRejects = 0;
        var queryBuildRejects = 0;
        var providerRepresentationMutations = 0;
        var rareMagicRegressions = 0;
        var remaining = new List<RemainingUnsupported>();
        var contractFailures = new List<string>();

        foreach (var rawText in corpus)
        {
            var parsed = parser.Parse(rawText);
            var baseResolution = displayService.ResolveItemBase(parsed, gameData).Result;
            var modifierResolutions = displayService
                .ResolveModifierCandidates(parsed, gameData, baseResolution)
                .Results
                .Select(display => display.Result)
                .OfType<ModifierCandidateResolutionResult>()
                .ToArray();
            var draftResult = draftMapper.CreateDraft(parsed, baseResolution, modifierResolutions, gameData);
            Assert.True(draftResult.IsSuccess);
            var draft = Assert.IsType<TradeSearchDraft>(draftResult.Draft);
            var identity = itemIdentityMapper.Map(draft, itemCatalog).Identity;
            var numericDraft = propertyResolver.Resolve(draft, filterCatalog);
            var providerDraft = priceCheckService.ResolveProviderComponents(
                numericDraft,
                statCatalog,
                identity,
                filterCatalog);
            if (baseline)
            {
                providerDraft = SimulateStageB(providerDraft, numericDraft);
            }

            controller.UpdateCurrentDraft(providerDraft, validator.Validate(providerDraft));
            var view = controller.CurrentViewState;
            var itemQueryBuildable = queryBuilder.Build(
                providerDraft,
                validator.Validate(providerDraft),
                "Allflame",
                selectedModifierFilters: [],
                identity,
                filterCatalog,
                selectedItemPropertyFilters: []).IsSuccess;
            var modifierRows = view.Modifiers
                .Concat(view.ItemProperties.SelectMany(property => property.Children))
                .GroupBy(row => row.SourceIndex)
                .ToDictionary(group => group.Key, group => group.First());

            totalRows += providerDraft.ItemProperties.Length + providerDraft.ModifierFilters.Count;
            propertyRows += providerDraft.ItemProperties.Length;
            selectablePropertyRows += view.ItemProperties.Count(row => row.IsAvailable);
            reconstructionFallbackRows += providerDraft.ItemProperties.Count(property =>
                !string.IsNullOrWhiteSpace(property.DerivationUnsupportedReason) && property.IsSearchable);
            propertyRowsBlockedByReconstruction += providerDraft.ItemProperties.Count(property =>
                !string.IsNullOrWhiteSpace(property.DerivationUnsupportedReason) && !property.IsSearchable);
            var selectableModifierRows = modifierRows.Values.Count(row => row.IsInteractionEnabled);
            selectableRows += selectableModifierRows + view.ItemProperties.Count(row => row.IsAvailable);

            var itemHadRegression = false;
            foreach (var (component, index) in providerDraft.ModifierFilters.Select(
                         (component, index) => (component, index)))
            {
                var isSelectable = modifierRows.TryGetValue(index, out var row) && row.IsInteractionEnabled;
                if (component.ParsedKind == ParsedModifierKind.Unique && !isSelectable)
                {
                    unsupportedUniqueRows++;
                    if (IsCurrentTradeSafeResidual(component, statCatalog))
                    {
                        tradeSafeUnsupportedRows++;
                    }

                    remaining.Add(new RemainingUnsupported(
                        Classify(component),
                        parsed.DisplayName ?? parsed.BaseType ?? "<unknown>",
                        component.OriginalText,
                        component.NotSearchableReason ?? component.ProviderDiagnosticMessage ??
                            component.UniqueResolutionDiagnosticCode ?? "No explicit reason."));
                }

                if (!isSelectable)
                {
                    continue;
                }

                var selectedDraft = providerDraft with
                {
                    ModifierFilters = providerDraft.ModifierFilters
                        .Select((candidate, candidateIndex) => candidate with
                        {
                            IsSelected = candidateIndex == index,
                        })
                        .ToArray(),
                };
                var mapping = selectedMapper.Map(selectedDraft, statCatalog);
                if (!mapping.IsSuccess)
                {
                    lateRejects++;
                    itemHadRegression = true;
                    contractFailures.Add(
                        $"Mapper | {parsed.DisplayName} | {component.OriginalText} | " +
                        string.Join("; ", mapping.Diagnostics.Select(diagnostic => diagnostic.Message)));
                    continue;
                }

                var build = itemQueryBuildable
                    ? queryBuilder.Build(
                        selectedDraft,
                        validator.Validate(selectedDraft),
                        "Allflame",
                        mapping.Filters,
                        identity,
                        filterCatalog,
                        selectedItemPropertyFilters: [])
                    : null;
                if (build is { IsSuccess: false })
                {
                    queryBuildRejects++;
                    itemHadRegression = true;
                    contractFailures.Add(
                        $"Query | {parsed.DisplayName} | {component.OriginalText} | " +
                        string.Join("; ", build.Diagnostics.Select(diagnostic => diagnostic.Message)));
                }
            }

            foreach (var (property, index) in providerDraft.ItemProperties.Select(
                         (property, index) => (property, index)))
            {
                if (!view.ItemProperties[index].IsAvailable)
                {
                    continue;
                }

                var selectedDraft = providerDraft with
                {
                    ItemProperties = providerDraft.ItemProperties
                        .Select((candidate, candidateIndex) => candidate with
                        {
                            IsSelected = candidateIndex == index,
                        })
                        .ToImmutableArray(),
                };
                var mapping = propertyResolver.MapSelected(selectedDraft, filterCatalog);
                if (!mapping.IsSuccess || mapping.Filters.Count != 1 ||
                    !IsFirstClassPropertyFilter(property.Kind, mapping.Filters[0]))
                {
                    lateRejects++;
                    itemHadRegression = true;
                    contractFailures.Add($"Property mapper | {parsed.DisplayName} | {property.Kind}");
                    continue;
                }

                var build = itemQueryBuildable
                    ? queryBuilder.Build(
                        selectedDraft,
                        validator.Validate(selectedDraft),
                        "Allflame",
                        selectedModifierFilters: [],
                        identity,
                        filterCatalog,
                        mapping.Filters)
                    : null;
                if (build is { IsSuccess: false })
                {
                    queryBuildRejects++;
                    itemHadRegression = true;
                    contractFailures.Add(
                        $"Property query | {parsed.DisplayName} | {property.Kind} | " +
                        string.Join("; ", build.Diagnostics.Select(diagnostic => diagnostic.Message)));
                }
            }

            controllerService.LastCheckedDraft = null;
            var beforeSearch = ProviderFingerprint(providerDraft);
            await controller.SearchAsync();
            if (controllerService.LastCheckedDraft is { } checkedDraft &&
                !string.Equals(beforeSearch, ProviderFingerprint(checkedDraft), StringComparison.Ordinal))
            {
                providerRepresentationMutations++;
                itemHadRegression = true;
            }

            if (itemHadRegression && parsed.Rarity is "Rare" or "Magic")
            {
                rareMagicRegressions++;
            }
        }

        var groupedRemaining = remaining
            .GroupBy(row => row.Reason, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<RemainingUnsupported>)group.ToArray(),
                StringComparer.Ordinal);
        var report = new StageCCorpusReport(
            corpus.Count,
            totalRows,
            selectableRows,
            propertyRows,
            selectablePropertyRows,
            reconstructionFallbackRows,
            propertyRowsBlockedByReconstruction,
            unsupportedUniqueRows,
            tradeSafeUnsupportedRows,
            totalRows - selectableRows,
            lateRejects,
            queryBuildRejects,
            providerRepresentationMutations,
            rareMagicRegressions,
            groupedRemaining);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
        }));

        if (!baseline)
        {
            Assert.Equal(0, propertyRowsBlockedByReconstruction);
            Assert.Equal(0, tradeSafeUnsupportedRows);
            Assert.Equal(0, lateRejects);
            Assert.True(queryBuildRejects == 0, string.Join(Environment.NewLine, contractFailures));
            Assert.Equal(0, providerRepresentationMutations);
            Assert.Equal(0, rareMagicRegressions);
        }
    }

    private static TradeSearchDraft SimulateStageB(
        TradeSearchDraft providerDraft,
        TradeSearchDraft numericDraft)
    {
        return providerDraft with
        {
            ItemProperties = providerDraft.ItemProperties.Select(property =>
                    string.IsNullOrWhiteSpace(property.DerivationUnsupportedReason)
                        ? property
                        : property with
                        {
                            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
                            IsSearchable = false,
                            NotSearchableReason = property.DerivationUnsupportedReason,
                        })
                .ToImmutableArray(),
            ModifierFilters = providerDraft.ModifierFilters.Select((component, index) =>
                string.Equals(
                    component.UniqueResolutionDiagnosticCode,
                    "UNIQUE_MECHANICS_NOT_FOUND",
                    StringComparison.Ordinal) &&
                component.StatMappingProof == ModifierStatMappingProofStatus.ProviderExact
                    ? numericDraft.ModifierFilters[index] with
                    {
                        IsSearchable = false,
                        ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Unsupported,
                        ProviderDiagnosticCode =
                            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
                    }
                    : component).ToArray(),
        };
    }

    private static bool IsCurrentTradeSafeResidual(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog)
    {
        if (component.UniqueOrigin != ParsedUniqueModifierOrigin.Ordinary ||
            !string.Equals(
                component.UniqueResolutionDiagnosticCode,
                "UNIQUE_MECHANICS_NOT_FOUND",
                StringComparison.Ordinal) ||
            component.SourceLineIndex < 0 ||
            component.OriginalText.Contains(Environment.NewLine, StringComparison.Ordinal) ||
            component.UniqueCatalogBlockIds.Count == 0 ||
            component.UniqueSourceObservationIds.Count == 0)
        {
            return false;
        }

        var match = new PathOfExileTradeStatMatcher().Match(component, catalog);
        return match.Status is PathOfExileTradeStatMatchStatus.Exact or
            PathOfExileTradeStatMatchStatus.ExactEquivalentSet;
    }

    private static string Classify(ResolvedSearchComponent component)
    {
        var diagnostic = string.Join(' ',
            component.UniqueResolutionDiagnosticCode,
            component.ProviderDiagnosticCode,
            component.NotSearchableReason,
            component.ProviderDiagnosticMessage);
        if (diagnostic.Contains("FOULBORN_REPLACEMENT", StringComparison.OrdinalIgnoreCase))
        {
            return "Foulborn replacement evidence unavailable";
        }
        if (diagnostic.Contains("AMBIG", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("VERSION", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("INDEPENDENT_DIMENSIONS", StringComparison.OrdinalIgnoreCase))
        {
            return "variant/version ambiguity";
        }
        if (diagnostic.Contains("MECHANIC", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("GameData", StringComparison.OrdinalIgnoreCase))
        {
            return "mechanics unresolved";
        }
        if (component.ProviderResolutionStatus is SearchComponentProviderResolutionStatus.NotFound or
            SearchComponentProviderResolutionStatus.Unsupported &&
            component.ResolutionStatus == ModifierCandidateResolutionStatus.Exact)
        {
            return "provider absent";
        }
        return "other explicit reason";
    }

    private static bool IsFirstClassPropertyFilter(
        TradeSearchItemPropertyKind kind,
        PathOfExileTradeSelectedItemPropertyFilter filter)
    {
        var expected = kind switch
        {
            TradeSearchItemPropertyKind.Armour => ("armour_filters", "ar"),
            TradeSearchItemPropertyKind.EvasionRating => ("armour_filters", "ev"),
            TradeSearchItemPropertyKind.EnergyShield => ("armour_filters", "es"),
            TradeSearchItemPropertyKind.Ward => ("armour_filters", "ward"),
            TradeSearchItemPropertyKind.ChanceToBlock => ("armour_filters", "block"),
            TradeSearchItemPropertyKind.TotalDps => ("weapon_filters", "dps"),
            TradeSearchItemPropertyKind.PhysicalDps => ("weapon_filters", "pdps"),
            TradeSearchItemPropertyKind.ElementalDps => ("weapon_filters", "edps"),
            TradeSearchItemPropertyKind.AttacksPerSecond => ("weapon_filters", "aps"),
            TradeSearchItemPropertyKind.CriticalStrikeChance => ("weapon_filters", "crit"),
            _ => (string.Empty, string.Empty),
        };
        return string.Equals(filter.ProviderGroupId, expected.Item1, StringComparison.Ordinal) &&
            string.Equals(filter.ProviderFilterId, expected.Item2, StringComparison.Ordinal);
    }

    private static string ProviderFingerprint(TradeSearchDraft draft) => JsonSerializer.Serialize(new
    {
        Properties = draft.ItemProperties.Select(property => new
        {
            property.Kind,
            property.ProviderResolutionStatus,
            property.IsSearchable,
            property.RequestedMinimum,
            property.RequestedMaximum,
        }),
        Modifiers = draft.ModifierFilters.Select(component => new
        {
            component.ComponentId,
            component.ProviderResolutionStatus,
            component.ProviderStatId,
            component.ProviderStatAlternativeIds,
            component.SelectedFilterVariantIdentity,
            component.SupportsValueBounds,
            component.CanonicalNumericValues,
            component.RequestedMinimum,
            component.RequestedMaximum,
        }),
    });

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

    private sealed record RemainingUnsupported(
        string Reason,
        string Item,
        string Text,
        string Detail);

    private sealed record StageCCorpusReport(
        int ItemCount,
        int TotalRows,
        int SelectableRows,
        int PropertyRows,
        int SelectablePropertyRows,
        int ReconstructionFallbackRows,
        int PropertyRowsBlockedByReconstruction,
        int UnsupportedUniqueRows,
        int UnsupportedButCurrentTradeSafeRows,
        int GenuinelyUnsupportedRows,
        int SelectedMapperLateRejects,
        int QueryBuildRejects,
        int ProviderRepresentationMutations,
        int RareMagicRegressions,
        IReadOnlyDictionary<string, IReadOnlyList<RemainingUnsupported>> RemainingUnsupported);

    private sealed class AcceptanceControllerPriceCheckService : IPathOfExileTradePriceCheckService
    {
        public TradeSearchDraft? LastCheckedDraft { get; set; }

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
            CancellationToken cancellationToken = default)
        {
            LastCheckedDraft = draft;
            return Task.FromResult(new PathOfExileTradePriceCheckResult
            {
                IsSuccess = true,
                Stage = PathOfExileTradePriceCheckStage.Completed,
                EffectiveDraft = draft,
            });
        }

        public Task<PathOfExileTradePriceCheckResult> FetchMoreAsync(
            string? searchQueryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();
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
