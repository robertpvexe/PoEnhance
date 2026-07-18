using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class OrdinaryItemCorpusLoaderTests
{
    [Fact]
    public void OrdinaryItemCorpus_LoadsAllSourceBlocksAndDetectsOrganicRingDuplicate()
    {
        var corpus = OrdinaryItemCorpus.Load();

        Assert.Equal(37, corpus.Blocks.Count);
        Assert.Equal(36, corpus.Blocks.Select(block => block.NormalizedFullTextHash).Distinct(StringComparer.Ordinal).Count());

        var duplicate = Assert.Single(corpus.Duplicates);
        Assert.Equal([7, 32], duplicate.SourceIndexes);
        Assert.Equal("Eagle Spiral", duplicate.DisplayName);
        Assert.Equal("Organic Ring", duplicate.BaseName);
    }
}

public sealed class OrdinaryItemCorpusOfflineAuditTests
{
    [Fact]
    public async Task OrdinaryItemCorpus_DeterministicOfflineStructureAudit()
    {
        var audit = await OrdinaryItemCorpusAuditRunner.Run(includeLiveCatalog: false);

        OrdinaryItemCorpusReportWriter.Write(audit);

        Assert.Equal(37, audit.Corpus.InputBlockCount);
        Assert.Equal(36, audit.Corpus.UniqueHashCount);
        Assert.DoesNotContain(audit.Failures, failure => failure.Stage == "CorpusInput");
        Assert.Empty(audit.AnchorFailures);
        OrdinaryItemCorpusAuditAssertions.AssertReportConsistency(audit);
        OrdinaryItemCorpusAuditAssertions.AssertMarkdownAndJsonAgree();
    }

    [Fact]
    public async Task OrdinaryItemCorpus_OfflineAuditRetainsDistinctSourceEffectsAfterProductionAggregation()
    {
        var audit = await OrdinaryItemCorpusAuditRunner.Run(includeLiveCatalog: false);

        var brigandine = SingleItem(audit, 11);
        Assert.Equal(10, brigandine.LogicalEffectCount);
        Assert.Contains(brigandine.Effects, effect =>
            effect.SourceModifierIndex == 3 &&
            effect.CleanedText == "37(33-38)% increased Armour and Evasion");
        Assert.Contains(brigandine.Effects, effect =>
            effect.SourceModifierIndex == 4 &&
            effect.CleanedText == "68(68-79)% increased Armour and Evasion");

        var shield = SingleItem(audit, 13);
        Assert.Equal(8, shield.LogicalEffectCount);
        Assert.Contains(shield.Effects, effect =>
            effect.SourceModifierIndex == 1 && effect.SourceLineIndex == 0 &&
            effect.CleanedText == "+15(11-15) to maximum Energy Shield");
        Assert.Contains(shield.Effects, effect =>
            effect.SourceModifierIndex == 3 && effect.SourceLineIndex == 1 &&
            effect.CleanedText == "+26(23-28) to maximum Energy Shield");

        var helmet = SingleItem(audit, 24);
        Assert.Equal(5, helmet.LogicalEffectCount);
        Assert.Equal(
            [0, 3],
            helmet.Effects
                .Where(effect => effect.CleanedText.Contains("increased Stun and Block Recovery", StringComparison.Ordinal))
                .Select(effect => effect.SourceModifierIndex)
                .Order());

        var boots = SingleItem(audit, 33);
        Assert.Equal(5, boots.LogicalEffectCount);
        Assert.Equal(
            [0, 3],
            boots.Effects
                .Where(effect => effect.CleanedText.Contains("increased Stun and Block Recovery", StringComparison.Ordinal))
                .Select(effect => effect.SourceModifierIndex)
                .Order());
        Assert.Empty(audit.Failures);
    }

    private static AuditItem SingleItem(OrdinaryItemAuditReport audit, int sourceIndex) =>
        Assert.Single(audit.Items, item => item.SourceIndex == sourceIndex);
}

public sealed class OrdinaryItemCorpusLiveCatalogTests
{
    [Fact]
    public async Task OrdinaryItemCorpusLiveCatalog_OptInStatsCatalogAudit()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("POENHANCE_RUN_LIVE_TRADE_CATALOG_AUDIT"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var audit = await OrdinaryItemCorpusAuditRunner.Run(includeLiveCatalog: true);

        OrdinaryItemCorpusReportWriter.Write(audit);

        Assert.True(audit.LiveCatalog?.WasRun);
        Assert.DoesNotContain(audit.Failures, failure =>
            failure.Stage is "QueryBuild" &&
            failure.Message.Contains("silently", StringComparison.OrdinalIgnoreCase));
        OrdinaryItemCorpusAuditAssertions.AssertReportConsistency(audit);
        OrdinaryItemCorpusAuditAssertions.AssertMarkdownAndJsonAgree();
    }
}

internal static partial class OrdinaryItemCorpusAuditRunner
{
    private const string LiveLeague = "Mirage";

    public static async Task<OrdinaryItemAuditReport> Run(bool includeLiveCatalog)
    {
        var corpus = OrdinaryItemCorpus.Load();
        var catalog = LoadGameDataCatalog();
        var displayService = new ParsedItemGameDataDisplayService();
        var parser = new ItemTextParser();
        var draftMapper = new TradeSearchDraftMapper();
        var validator = new TradeSearchDraftValidator();
        var failures = new List<AuditFailure>();
        var items = new List<AuditItem>();

        PathOfExileTradeStatCatalog? tradeStatCatalog = null;
        LiveCatalogAuditSummary? liveSummary = null;
        if (includeLiveCatalog)
        {
            using var httpClient = new HttpClient();
            var catalogResult = await new PathOfExileTradeStatCatalogProvider(
                    new PathOfExileTradeStatsClient(httpClient))
                .GetCatalogAsync();
            liveSummary = new LiveCatalogAuditSummary
            {
                WasRun = true,
                CatalogLoaded = catalogResult.IsSuccess && catalogResult.Catalog is not null,
                DiagnosticCodes = catalogResult.Diagnostics
                    .Select(diagnostic => diagnostic.Code)
                    .Concat(catalogResult.ParserDiagnostics.Select(diagnostic => diagnostic.Code))
                    .Concat(catalogResult.RateLimitDiagnostics.Select(diagnostic => diagnostic.Code))
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(code => code, StringComparer.Ordinal)
                    .ToArray(),
            };

            if (catalogResult.IsSuccess && catalogResult.Catalog is not null)
            {
                tradeStatCatalog = catalogResult.Catalog;
            }
            else
            {
                failures.Add(new AuditFailure(
                    "CorpusInput",
                    null,
                    null,
                    "The opt-in Trade stats catalog could not be loaded.",
                    string.Join(", ", liveSummary.DiagnosticCodes)));
            }
        }

        foreach (var block in corpus.Blocks)
        {
            var itemFailures = new List<AuditFailure>();
            ParsedItem? parsed = null;
            ItemBaseResolutionResult? baseResolution = null;
            IReadOnlyList<ModifierCandidateResolutionResult> modifierResolutions = [];
            TradeSearchDraft? draft = null;
            TradeSearchValidationResult? validation = null;

            try
            {
                parsed = parser.Parse(block.RawText);
            }
            catch (Exception exception)
            {
                itemFailures.Add(new AuditFailure("Parse", block.SourceIndex, null, "Parser threw.", exception.Message));
            }

            if (parsed is not null)
            {
                baseResolution = displayService.ResolveItemBase(parsed, catalog).Result;
                modifierResolutions = displayService
                    .ResolveModifierCandidates(parsed, catalog, baseResolution)
                    .Results
                    .Select(display => display.Result)
                    .OfType<ModifierCandidateResolutionResult>()
                    .ToArray();

                var draftResult = draftMapper.CreateDraft(parsed, baseResolution, modifierResolutions, catalog);
                if (draftResult.IsSuccess && draftResult.Draft is not null)
                {
                    draft = draftResult.Draft;
                    validation = validator.Validate(draft);
                }
                else
                {
                    itemFailures.AddRange(draftResult.Diagnostics.Select(diagnostic =>
                        new AuditFailure("TradeSearchDraft", block.SourceIndex, null, diagnostic.Message, diagnostic.Code)));
                }
            }

            var item = CreateItemAudit(
                block,
                parsed,
                baseResolution,
                modifierResolutions,
                draft,
                validation,
                includeLiveCatalog && tradeStatCatalog is not null
                    ? RunLiveAudit(block, draft, validation, tradeStatCatalog)
                    : null,
                itemFailures);

            var itemStageFailures = itemFailures
                .Concat(FindGenericInvariantFailures(item))
                .Concat(item.Live?.Failures ?? [])
                .ToArray();
            item = item with { Failures = itemStageFailures };
            items.Add(item);
            failures.AddRange(itemStageFailures);
        }

        var anchorFailures = AnchorExpectations.Evaluate(items).ToArray();
        failures.AddRange(anchorFailures);
        var requiredStageFailures = items.SelectMany(FindRequiredStageFailures).ToArray();
        failures.AddRange(requiredStageFailures);
        items = items
            .Select(item => item with
            {
                Failures = failures
                    .Where(failure => failure.SourceIndex == item.SourceIndex)
                    .ToArray(),
            })
            .ToList();
        var classification = BuildClassificationSummary(items);

        var fullReport = new OrdinaryItemAuditReport
        {
            Corpus = new CorpusAuditSummary
            {
                InputBlockCount = corpus.Blocks.Count,
                UniqueHashCount = corpus.Blocks
                    .Select(block => block.NormalizedFullTextHash)
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                Duplicates = corpus.Duplicates,
            },
            CountsByClass = items
                .GroupBy(item => item.ItemClass ?? "<unknown>", StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            CountsByRarity = items
                .GroupBy(item => item.Rarity ?? "<unknown>", StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            Items = items,
            Failures = failures,
            FailureCountsByStage = failures
                .GroupBy(failure => failure.Stage, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            AnchorFailures = anchorFailures,
            LiveCatalog = liveSummary,
            Classification = classification,
            CompletePipelinePassedCases = classification.CompleteOrdinaryPipelinePass
                .Select(item => item.Label)
                .ToArray(),
        };
        return fullReport;
    }

    private static AuditItem CreateItemAudit(
        CorpusBlock block,
        ParsedItem? parsed,
        ItemBaseResolutionResult? baseResolution,
        IReadOnlyList<ModifierCandidateResolutionResult> modifierResolutions,
        TradeSearchDraft? draft,
        TradeSearchValidationResult? validation,
        LiveItemAudit? live,
        IReadOnlyList<AuditFailure> itemFailures)
    {
        var effects = new List<AuditEffect>();
        if (draft is not null)
        {
            foreach (var component in draft.ModifierFilters)
            {
                if (component.Sources.Count > 1)
                {
                    effects.AddRange(component.Sources.Select(source => CreateSourceAuditEffect(source, parsed)));
                    continue;
                }

                var sourceModifier = component.SourceModifierIndex >= 0 &&
                    parsed is not null &&
                    component.SourceModifierIndex < parsed.Modifiers.Count
                    ? parsed.Modifiers[component.SourceModifierIndex]
                    : null;
                var sourceEffect = sourceModifier is not null &&
                    component.SourceLineIndex >= 0 &&
                    component.SourceLineIndex < sourceModifier.Effects.Count
                    ? sourceModifier.Effects[component.SourceLineIndex]
                    : null;
                var normalization = PathOfExileTradeStatTemplateNormalizer.NormalizeModifierText(component.OriginalText);
                effects.Add(new AuditEffect
                {
                    ComponentId = component.ComponentId,
                    SourceModifierIndex = component.SourceModifierIndex,
                    SourceLineIndex = component.SourceLineIndex,
                    CleanedText = component.OriginalText,
                    NormalizedTemplate = normalization.NormalizedTemplate,
                    ExtractedNumericValues = normalization.ExtractedNumericValues,
                    SourceKind = SourceKindLabel(component, sourceModifier),
                    ParsedKind = component.ParsedKind.ToString(),
                    SourceModifierName = sourceModifier?.Name ?? component.ParsedModifierName,
                    Tier = sourceModifier?.Tier,
                    Rank = sourceModifier?.Rank,
                    IsCrafted = sourceModifier?.IsCrafted ?? component.IsCrafted,
                    Locality = component.Locality == ModifierLocality.Global
                        ? "Unmarked"
                        : component.Locality.ToString(),
                    ReminderTextExcluded = sourceEffect?.ReminderLines.Count > 0 &&
                        !sourceEffect.ReminderLines.Any(line => component.OriginalText.Contains(line, StringComparison.Ordinal)),
                    UnscalableValueExcluded = sourceEffect?.HasUnscalableValue == true &&
                        !component.OriginalText.Contains("Unscalable Value", StringComparison.Ordinal),
                    FlavourTextExcluded = parsed is null ||
                        !parsed.FlavourTextLines.Any(line => component.OriginalText.Contains(line, StringComparison.Ordinal)),
                    ResolutionStatus = component.ResolutionStatus?.ToString(),
                    ResolvedModifierId = component.ResolvedModifierId,
                    ResolvedModifierName = component.ResolvedModifierName,
                    ResolvedStatIds = component.ResolvedStatIds,
                    IsSearchable = component.IsSearchable,
                    NotSearchableReason = component.NotSearchableReason,
                });
            }
        }

        return new AuditItem
        {
            SourceIndex = block.SourceIndex,
            Hash = block.NormalizedFullTextHash,
            Label = Label(parsed, block, baseResolution),
            RawDisplayName = block.DisplayName,
            RawBaseName = block.BaseName,
            ParseSucceeded = parsed is not null,
            ItemClass = parsed?.ItemClass,
            Rarity = parsed?.Rarity,
            DisplayName = parsed?.DisplayName,
            ParserBaseText = parsed?.BaseType,
            ItemStates = parsed?.ItemStates ?? [],
            Unidentified = parsed?.ItemStates.Any(state => state.Contains("Unidentified", StringComparison.OrdinalIgnoreCase)) == true,
            BaseRecognitionStatus = baseResolution?.Status.ToString(),
            ResolvedBaseName = baseResolution?.ResolvedBaseName,
            DefaultTradeCategory = draft?.Base.ActiveCriterion?.Category ?? draft?.Base.Category,
            ExactBaseSerializedByDefault = draft?.Base.ActiveCriterion?.Mode == BaseSearchMode.ExactBase,
            LogicalEffectCount = effects.Count,
            Effects = effects,
            ModifierResolutionSummaries = modifierResolutions
                .Select(result => new ModifierResolutionSummary
                {
                    ParsedModifierIndex = result.ParsedModifierIndex,
                    Status = result.Status.ToString(),
                    CandidateCount = result.Candidates.Count,
                    GenerationType = result.GenerationType?.ToString(),
                    Locality = result.Locality.ToString(),
                    ParsedModifierName = result.ParsedModifierName,
                })
                .ToArray(),
            ValidationDiagnosticCodes = validation?.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray() ?? [],
            Live = live,
            Failures = itemFailures,
        };
    }

    private static AuditEffect CreateSourceAuditEffect(
        SearchComponentSourceProvenance source,
        ParsedItem? parsed)
    {
        var sourceModifier = source.SourceModifierIndex >= 0 &&
            parsed is not null &&
            source.SourceModifierIndex < parsed.Modifiers.Count
            ? parsed.Modifiers[source.SourceModifierIndex]
            : null;
        var sourceEffect = sourceModifier is not null &&
            source.SourceLineIndex >= 0 &&
            source.SourceLineIndex < sourceModifier.Effects.Count
            ? sourceModifier.Effects[source.SourceLineIndex]
            : null;
        var normalization = PathOfExileTradeStatTemplateNormalizer.NormalizeModifierText(source.OriginalText);
        return new AuditEffect
        {
            ComponentId = source.ComponentId,
            SourceModifierIndex = source.SourceModifierIndex,
            SourceLineIndex = source.SourceLineIndex,
            CleanedText = source.OriginalText,
            NormalizedTemplate = normalization.NormalizedTemplate,
            ExtractedNumericValues = normalization.ExtractedNumericValues,
            SourceKind = SourceKindLabel(source, sourceModifier),
            ParsedKind = source.ParsedKind.ToString(),
            SourceModifierName = sourceModifier?.Name ?? source.ParsedModifierName,
            Tier = sourceModifier?.Tier,
            Rank = sourceModifier?.Rank,
            IsCrafted = sourceModifier?.IsCrafted ?? source.IsCrafted,
            Locality = source.Locality == ModifierLocality.Global
                ? "Unmarked"
                : source.Locality.ToString(),
            ReminderTextExcluded = sourceEffect?.ReminderLines.Count > 0 &&
                !sourceEffect.ReminderLines.Any(line => source.OriginalText.Contains(line, StringComparison.Ordinal)),
            UnscalableValueExcluded = sourceEffect?.HasUnscalableValue == true &&
                !source.OriginalText.Contains("Unscalable Value", StringComparison.Ordinal),
            FlavourTextExcluded = parsed is null ||
                !parsed.FlavourTextLines.Any(line => source.OriginalText.Contains(line, StringComparison.Ordinal)),
            ResolutionStatus = source.ProviderResolutionStatus.ToString(),
            ResolvedModifierId = source.ResolvedModifierId,
            ResolvedModifierName = source.ResolvedModifierName,
            ResolvedStatIds = source.ResolvedStatIds,
            IsSearchable = source.ProviderResolutionStatus is SearchComponentProviderResolutionStatus.Exact or
                SearchComponentProviderResolutionStatus.BaseGuaranteed,
        };
    }

    private static LiveItemAudit RunLiveAudit(
        CorpusBlock block,
        TradeSearchDraft? draft,
        TradeSearchValidationResult? validation,
        PathOfExileTradeStatCatalog tradeStatCatalog)
    {
        var failures = new List<AuditFailure>();
        var effects = new List<LiveEffectAudit>();
        if (draft is null || validation is null)
        {
            return new LiveItemAudit { Failures = failures };
        }

        var statMatcher = new PathOfExileTradeStatMatcher();
        var selectedMapper = new PathOfExileTradeSelectedModifierMapper();
        var queryBuilder = new PathOfExileTradeQueryBuilder();
        var service = new PathOfExileTradePriceCheckService(
            queryBuilder,
            statMatcher,
            new StaticStatCatalogProvider(tradeStatCatalog),
            new ThrowingItemCatalogProvider(),
            selectedMapper,
            new ThrowingItemIdentityMapper(),
            new ThrowingSearchClient(),
            new ThrowingFetchClient(),
            new StaticFilterCatalogProvider(TestFilterCatalog()));
        var resolvedDraft = service.ResolveProviderComponents(draft, tradeStatCatalog);

        for (var index = 0; index < draft.ModifierFilters.Count; index++)
        {
            var original = draft.ModifierFilters[index];
            var singleResolvedDraft = SelectAndResolveDraft(
                draft,
                [index],
                service,
                tradeStatCatalog);
            var resolved = singleResolvedDraft.ModifierFilters[index];
            var match = CanResolveProviderComponent(original)
                ? statMatcher.Match(original, tradeStatCatalog, CreateMatchContext(draft, original))
                : new PathOfExileTradeStatMatchResult
                {
                    Status = PathOfExileTradeStatMatchStatus.InvalidInput,
                    NormalizedItemTemplate = ToProviderTemplate(original.CanonicalSignature),
                    Diagnostics =
                    [
                        new PathOfExileTradeStatMatchDiagnostic(
                            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
                            "The component is outside the currently serializable ordinary-item provider slice."),
                    ],
                };
            var single = BuildSelectedQuery(
                singleResolvedDraft,
                validation,
                selectedEffectCount: 1,
                selectedMapper,
                queryBuilder);
            var result = ResultLabel(match, original, resolved);

            if (block.SourceIndex != 11 &&
                match.Status == PathOfExileTradeStatMatchStatus.Exact &&
                single.EnabledFilterCount != 1)
            {
                failures.Add(new AuditFailure(
                    "QueryBuild",
                    block.SourceIndex,
                    index,
                    "A uniquely matched selected effect did not serialize exactly one enabled stat filter.",
                    $"Serialized {single.EnabledFilterCount}."));
            }

            if (block.SourceIndex != 11 && result is "Missing" or "Ambiguous")
            {
                failures.Add(new AuditFailure(
                    "ProviderResolution",
                    block.SourceIndex,
                    index,
                    result == "Missing"
                        ? "Provider stat match is missing for a required ordinary effect."
                        : "Provider stat match is ambiguous for a required ordinary effect.",
                    match.Diagnostics.FirstOrDefault()?.Code));
            }

            effects.Add(new LiveEffectAudit
            {
                ComponentId = original.ComponentId,
                SourceIndex = index,
                CleanedMatchingTemplate = match.NormalizedItemTemplate,
                SourceProvenance = $"{original.ParsedKind}; {original.ParsedModifierName}; {original.ResolvedModifierId}",
                Locality = original.Locality == ModifierLocality.Global ? "Unmarked" : original.Locality.ToString(),
                CompatibleCandidates = (match.Trace?.CompatibleProviderCandidates ?? match.Candidates)
                    .Select(ToCandidateAudit)
                    .ToArray(),
                RejectedCandidates = (match.Trace?.Rejections ?? [])
                    .Select(rejection => new ProviderCandidateRejectionAudit
                    {
                        Candidate = ToCandidateAudit(rejection.Candidate),
                        Reason = rejection.Reason,
                    })
                    .ToArray(),
                Result = result,
                SelectedStatId = match.ExactCandidate?.StatId,
                ProviderResolutionStatus = resolved.ProviderResolutionStatus.ToString(),
                ProviderDiagnosticCode = resolved.ProviderDiagnosticCode,
                SingleSelectedQuery = single,
            });
        }

        var uniqueIndexes = resolvedDraft.ModifierFilters
            .Select((effect, index) => new { effect, index })
            .Where(item => item.effect.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact)
            .Select(item => item.index)
            .ToArray();
        var combined = BuildSelectedQuery(
            SelectAndResolveDraft(draft, uniqueIndexes, service, tradeStatCatalog),
            validation,
            uniqueIndexes.Length,
            selectedMapper,
            queryBuilder);
        if (block.SourceIndex != 11 &&
            combined.MappingSucceeded && combined.QuerySucceeded &&
            !uniqueIndexes.Order().SequenceEqual(combined.MappingSourceIndexes.Order()))
        {
            failures.Add(new AuditFailure(
                "QueryBuild",
                block.SourceIndex,
                null,
                "Selected component source coverage does not match the provider filter mapping.",
                $"Selected [{string.Join(", ", uniqueIndexes)}]; mapped [{string.Join(", ", combined.MappingSourceIndexes)}]."));
        }

        return new LiveItemAudit
        {
            Effects = effects,
            CombinedUniqueQuery = combined,
            Failures = failures,
        };
    }

    private static SelectedQueryAudit BuildSelectedQuery(
        TradeSearchDraft selectedResolvedDraft,
        TradeSearchValidationResult validation,
        int selectedEffectCount,
        PathOfExileTradeSelectedModifierMapper mapper,
        PathOfExileTradeQueryBuilder builder)
    {
        var mapping = mapper.Map(selectedResolvedDraft);
        var build = builder.Build(
            selectedResolvedDraft,
            validation,
            LiveLeague,
            mapping.IsSuccess ? mapping.Filters : [],
            providerFilterCatalog: TestFilterCatalog());
        var filters = build.Request?.Query.Stats.SelectMany(group => group.Filters).ToArray() ?? [];
        var mappingSourceIndexes = mapping.Filters
            .SelectMany(filter => filter.SourceIndexes.Count > 0
                ? filter.SourceIndexes
                : [filter.SourceIndex])
            .OrderBy(index => index)
            .ToArray();
        var baseGuaranteedCriterionCount = build.Request?.Query.Type is not null &&
            selectedResolvedDraft.ModifierFilters.Any(effect =>
                effect.IsSelected &&
                effect.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.BaseGuaranteed)
            ? 1
            : 0;
        return new SelectedQueryAudit
        {
            SelectedEffectCount = selectedEffectCount,
            MappingSucceeded = mapping.IsSuccess,
            MappingDiagnosticCodes = mapping.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
            MappingSourceIndexes = mappingSourceIndexes,
            QuerySucceeded = build.IsSuccess,
            QueryDiagnosticCodes = build.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
            EnabledFilterCount = filters.Length + baseGuaranteedCriterionCount,
            SerializedStatIds = filters.Select(filter => filter.Id).ToArray(),
            SerializedMinValues = [],
            SerializedMaxValues = [],
            DuplicatedProviderIds = filters
                .GroupBy(filter => filter.Id, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray(),
            StatsGroupOperator = build.Request?.Query.Stats.SingleOrDefault()?.Type,
            ExactBaseSerialized = build.Request?.Query.Type is not null,
            SerializedJson = build.SerializedJson,
            OmittedEffects = mapping.Diagnostics.Select(diagnostic => diagnostic.SourceIndex?.ToString() ?? "<unknown>").ToArray(),
        };
    }

    private static TradeSearchDraft SelectAndResolveDraft(
        TradeSearchDraft draft,
        IReadOnlyCollection<int> selectedIndexes,
        PathOfExileTradePriceCheckService service,
        PathOfExileTradeStatCatalog tradeStatCatalog)
    {
        var selectedSet = selectedIndexes.ToHashSet();
        var selectedDraft = draft with
        {
            ModifierFilters = draft.ModifierFilters
                .Select((effect, index) => effect with { IsSelected = selectedSet.Contains(index) })
                .ToArray(),
        };
        return service.ResolveProviderComponents(selectedDraft, tradeStatCatalog);
    }

    private static IReadOnlyList<AuditFailure> FindRequiredStageFailures(AuditItem item)
    {
        var failures = new List<AuditFailure>();
        if (item.ParseSucceeded &&
            string.Equals(item.BaseRecognitionStatus, "Unknown", StringComparison.Ordinal))
        {
            failures.Add(new AuditFailure(
                "BaseRecognition",
                item.SourceIndex,
                null,
                "Base recognition is Unknown.",
                item.ResolvedBaseName));
        }

        if (item.Live is not null && !item.OutsideCurrentOrdinaryTradeSuccessSlice)
        {
            foreach (var effect in item.Live.Effects.Where(effect => effect.Result == "UnsupportedSpecial"))
            {
                failures.Add(new AuditFailure(
                    "ProviderResolution",
                    item.SourceIndex,
                    effect.SourceIndex,
                    "A required ordinary effect is unsupported by provider resolution.",
                    effect.ProviderDiagnosticCode));
            }
        }

        return failures;
    }

    private static ClassificationSummary BuildClassificationSummary(IReadOnlyList<AuditItem> items)
    {
        var complete = new List<ClassifiedCorpusItem>();
        var partial = new List<ClassifiedCorpusItem>();
        var failed = new List<ClassifiedCorpusItem>();

        foreach (var group in items.GroupBy(item => item.Hash, StringComparer.Ordinal))
        {
            var groupedItems = group.OrderBy(item => item.SourceIndex).ToArray();
            var representative = groupedItems[0];
            var failures = groupedItems.SelectMany(item => item.Failures).ToArray();
            var unsupported = groupedItems
                .SelectMany(ExpectedUnsupportedEffects)
                .ToArray();
            var classified = new ClassifiedCorpusItem
            {
                Hash = group.Key,
                Label = representative.Label,
                SourceIndexes = groupedItems.Select(item => item.SourceIndex).ToArray(),
                FailureCount = failures.Length,
                UnsupportedEffects = unsupported,
                FailureStages = failures
                    .Select(failure => failure.Stage)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(stage => stage, StringComparer.Ordinal)
                    .ToArray(),
            };

            if (failures.Length > 0)
            {
                failed.Add(classified);
            }
            else if (unsupported.Length > 0)
            {
                partial.Add(classified);
            }
            else
            {
                complete.Add(classified);
            }
        }

        return new ClassificationSummary
        {
            CompleteOrdinaryPipelinePass = complete,
            PartialExpectedUnsupported = partial,
            Failed = failed,
        };
    }

    private static IEnumerable<ExpectedUnsupportedEffect> ExpectedUnsupportedEffects(AuditItem item)
    {
        if (!item.OutsideCurrentOrdinaryTradeSuccessSlice)
        {
            yield break;
        }

        var emitted = false;
        if (item.Live is not null)
        {
            foreach (var effect in item.Live.Effects.Where(effect => effect.Result == "UnsupportedSpecial"))
            {
                emitted = true;
                yield return new ExpectedUnsupportedEffect
                {
                    SourceIndex = effect.SourceIndex,
                    Text = effect.CleanedMatchingTemplate,
                    Diagnostic = effect.ProviderDiagnosticCode ?? "UnsupportedSpecial",
                };
            }
        }
        else
        {
            foreach (var effect in item.Effects.Where(effect =>
                         string.Equals(effect.SourceKind, "special implicit", StringComparison.OrdinalIgnoreCase) ||
                         !effect.IsSearchable))
            {
                emitted = true;
                yield return new ExpectedUnsupportedEffect
                {
                    SourceIndex = effect.SourceModifierIndex,
                    Text = effect.CleanedText,
                    Diagnostic = effect.NotSearchableReason ?? "ExpectedUnsupported",
                };
            }
        }

        if (!emitted)
        {
            yield return new ExpectedUnsupportedEffect
            {
                SourceIndex = null,
                Text = "Item is marked outside the current ordinary-item Trade success slice.",
                Diagnostic = "ExpectedUnsupported",
            };
        }
    }

    private static IEnumerable<AuditFailure> FindGenericInvariantFailures(AuditItem item)
    {
        foreach (var effect in item.Effects)
        {
            if (effect.CleanedText.Contains("Unscalable Value", StringComparison.Ordinal))
            {
                yield return new AuditFailure("LogicalEffectExtraction", item.SourceIndex, effect.SourceModifierIndex, "Unscalable Value remained in cleaned matching text.");
            }

            if (effect.CleanedText.StartsWith("(", StringComparison.Ordinal))
            {
                yield return new AuditFailure("LogicalEffectExtraction", item.SourceIndex, effect.SourceModifierIndex, "Reminder paragraph became a selectable effect.");
            }

            if (effect.CleanedText.Contains("Our flesh longs to move as one.", StringComparison.Ordinal))
            {
                yield return new AuditFailure("LogicalEffectExtraction", item.SourceIndex, effect.SourceModifierIndex, "Flavour text became a modifier.");
            }

            if (TierRangeRegex().IsMatch(effect.CleanedText) &&
                effect.ExtractedNumericValues.Count > effect.CleanedText.Count(char.IsDigit))
            {
                yield return new AuditFailure("LogicalEffectExtraction", item.SourceIndex, effect.SourceModifierIndex, "Tier-roll ranges may have been extracted as separate values.");
            }
        }
    }

    private static ProviderCandidateAudit ToCandidateAudit(PathOfExileTradeStatMatchCandidate candidate)
    {
        return new ProviderCandidateAudit
        {
            StatId = candidate.StatId,
            GroupId = candidate.GroupId,
            GroupLabel = candidate.GroupLabel,
            Type = candidate.Type,
            NormalizedText = candidate.NormalizedTemplate,
            ProviderKind = candidate.ProviderKind,
            ProviderLocality = candidate.ProviderLocality.ToString(),
        };
    }

    private static string ResultLabel(
        PathOfExileTradeStatMatchResult match,
        ResolvedSearchComponent component,
        ResolvedSearchComponent resolvedComponent)
    {
        if (resolvedComponent.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.BaseGuaranteed)
        {
            return "BaseGuaranteed";
        }

        if (!CanResolveProviderComponent(component))
        {
            return "UnsupportedSpecial";
        }

        return match.Status switch
        {
            PathOfExileTradeStatMatchStatus.Exact => "UniqueMatch",
            PathOfExileTradeStatMatchStatus.Ambiguous => "Ambiguous",
            PathOfExileTradeStatMatchStatus.NotFound => "Missing",
            _ => "UnsupportedSpecial",
        };
    }

    private static bool CanResolveProviderComponent(ResolvedSearchComponent component)
    {
        if (component.ParsedKind == ParsedModifierKind.Implicit &&
            !string.IsNullOrWhiteSpace(component.CanonicalSignature) &&
            !string.IsNullOrWhiteSpace(component.OriginalText))
        {
            return true;
        }

        return component.IsSearchable &&
            component.ResolutionStatus == ModifierCandidateResolutionStatus.Exact &&
            !string.IsNullOrWhiteSpace(component.ResolvedModifierId) &&
            component.ResolvedStatIds.Count > 0;
    }

    private static PathOfExileTradeStatMatchContext CreateMatchContext(
        TradeSearchDraft draft,
        ResolvedSearchComponent component)
    {
        return new PathOfExileTradeStatMatchContext
        {
            ItemClass = draft.ItemClass,
            ParsedBaseType = draft.ParsedBaseType,
            ModifierLocality = component.Locality,
            ResolvedModifierId = component.ResolvedModifierId,
            ResolvedModifierName = component.ResolvedModifierName,
            InternalStatIds = component.ResolvedStatIds,
        };
    }

    private static string SourceKindLabel(
        ResolvedSearchComponent component,
        ParsedModifier? sourceModifier)
    {
        if (sourceModifier?.IsCrafted == true)
        {
            return "crafted";
        }

        if (component.ParsedKind == ParsedModifierKind.Implicit &&
            (sourceModifier?.CategoryText?.Contains("Eater", StringComparison.OrdinalIgnoreCase) == true ||
                sourceModifier?.CategoryText?.Contains("Exarch", StringComparison.OrdinalIgnoreCase) == true))
        {
            return "special implicit";
        }

        return component.ParsedKind switch
        {
            ParsedModifierKind.Implicit => "implicit",
            ParsedModifierKind.Prefix => "prefix",
            ParsedModifierKind.Suffix => "suffix",
            _ => component.ParsedKind.ToString(),
        };
    }

    private static string SourceKindLabel(
        SearchComponentSourceProvenance source,
        ParsedModifier? sourceModifier)
    {
        if (sourceModifier?.IsCrafted == true)
        {
            return "crafted";
        }

        if (source.ParsedKind == ParsedModifierKind.Implicit &&
            (sourceModifier?.CategoryText?.Contains("Eater", StringComparison.OrdinalIgnoreCase) == true ||
                sourceModifier?.CategoryText?.Contains("Exarch", StringComparison.OrdinalIgnoreCase) == true))
        {
            return "special implicit";
        }

        return source.ParsedKind switch
        {
            ParsedModifierKind.Implicit => "implicit",
            ParsedModifierKind.Prefix => "prefix",
            ParsedModifierKind.Suffix => "suffix",
            _ => source.ParsedKind.ToString(),
        };
    }

    private static string ToProviderTemplate(string canonicalSignature)
    {
        return canonicalSignature
            .ReplaceLineEndings(" ")
            .Replace("+<number>", "+#", StringComparison.Ordinal)
            .Replace("-<number>", "-#", StringComparison.Ordinal)
            .Replace("<number>", "#", StringComparison.Ordinal);
    }

    private static string Label(
        ParsedItem? parsed,
        CorpusBlock block,
        ItemBaseResolutionResult? baseResolution)
    {
        var name = parsed?.DisplayName ?? block.DisplayName;
        var baseName = parsed?.BaseType ?? baseResolution?.ResolvedBaseName ?? block.BaseName;
        return string.IsNullOrWhiteSpace(baseName) || string.Equals(name, baseName, StringComparison.Ordinal)
            ? name ?? $"Item {block.SourceIndex}"
            : $"{name} / {baseName}";
    }

    private static GameDataCatalog LoadGameDataCatalog()
    {
        var packagePath = FindRepoFile("artifacts", "poenhance-game-data.json");
        var result = GameDataPackageLoader
            .LoadFromFileAsync(packagePath)
            .GetAwaiter()
            .GetResult();

        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Code)));
        Assert.NotNull(result.Package);
        return GameDataCatalog.FromPackage(result.Package!);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repo file: {Path.Combine(relativeParts)}");
    }

    private static PathOfExileTradeFilterCatalog TestFilterCatalog()
    {
        return new PathOfExileTradeFilterCatalog(
        [
            Category(0, "armour.chest", "Body Armour"),
            Category(1, "weapon.oneaxe", "One-Handed Axe"),
            Category(2, "weapon.bow", "Bow"),
            Category(3, "weapon.wand", "Wand"),
            Category(4, "jewel", "Base Jewel"),
            Category(5, "accessory.ring", "Ring"),
            Category(6, "accessory.belt", "Belt"),
            Category(7, "armour.shield", "Shield"),
            Category(8, "armour.boots", "Boots"),
            Category(9, "armour.helmet", "Helmet"),
            Category(10, "armour.gloves", "Gloves"),
            Category(11, "accessory.amulet", "Amulet"),
            Category(12, "weapon.onesceptre", "Sceptre"),
        ]);
    }

    private static PathOfExileTradeFilterOption Category(
        int order,
        string id,
        string text)
    {
        return new PathOfExileTradeFilterOption
        {
            ProviderOrder = order,
            GroupId = "type_filters",
            FilterId = "category",
            Id = id,
            Text = text,
        };
    }

    [GeneratedRegex(@"\(\d+(?:\.\d+)?-\d+(?:\.\d+)?\)", RegexOptions.CultureInvariant)]
    private static partial Regex TierRangeRegex();
}

internal static class OrdinaryItemCorpus
{
    private static readonly Regex ItemBoundary = new(
        @"\r?\n\s*\r?\n(?=Item Class:)",
        RegexOptions.CultureInvariant);

    public static CorpusFixture Load()
    {
        var path = FindCorpusPath();
        var raw = File.ReadAllText(path);
        var blocks = ItemBoundary
            .Split(raw.TrimEnd('\r', '\n'))
            .Where(block => !string.IsNullOrWhiteSpace(block))
            .Select((block, index) => CorpusBlock.Create(index, block))
            .ToArray();
        var duplicates = blocks
            .GroupBy(block => block.NormalizedFullTextHash, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group =>
            {
                var first = group.First();
                return new CorpusDuplicate
                {
                    Hash = group.Key,
                    SourceIndexes = group.Select(block => block.SourceIndex).ToArray(),
                    DisplayName = first.DisplayName,
                    BaseName = first.BaseName,
                };
            })
            .ToArray();
        return new CorpusFixture(path, blocks, duplicates);
    }

    private static string FindCorpusPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "PoEnhance.App.Tests",
                "TestData",
                "Items",
                "ordinary-item-price-checker-corpus.txt");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.Combine(
                directory.FullName,
                "TestData",
                "Items",
                "ordinary-item-price-checker-corpus.txt");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find ordinary-item-price-checker-corpus.txt.");
    }
}

internal static class AnchorExpectations
{
    public static IEnumerable<AuditFailure> Evaluate(IReadOnlyList<AuditItem> items)
    {
        foreach (var expectation in Expectations())
        {
            var matches = items.Where(expectation.Matches).ToArray();
            if (matches.Length == 0)
            {
                yield return expectation.Failure("CorpusInput", null, "Anchor item was not found.");
                continue;
            }

            foreach (var item in matches)
            {
                foreach (var failure in expectation.Evaluate(item))
                {
                    yield return failure;
                }
            }
        }
    }

    private static IReadOnlyList<AnchorExpectation> Expectations()
    {
        return
        [
            new("Normal Necrotic Armour", item => item.SourceIndex == 0)
            {
                ItemClass = "Body Armours",
                Rarity = "Normal",
                ParserBaseText = "Necrotic Armour",
                Category = "Body Armour",
                EffectCount = 0,
                ExactBaseByDefault = false,
            },
            new("Flaming Reaver Axe of the Marksman", item => item.SourceIndex == 1)
            {
                ResolvedBaseName = "Reaver Axe",
                Rarity = "Magic",
                Category = "One Hand Axe",
                EffectCount = 2,
                RequiredEffectFragments =
                [
                    "Adds 26(24-33) to 57(49-57) Fire Damage",
                    "+388(326-455) to Accuracy Rating",
                ],
                RequiredLocalFragments = ["Adds 26(24-33) to 57(49-57) Fire Damage"],
            },
            new("Golem Fletch / Ranger Bow", item => item.SourceIndex == 2)
            {
                Category = "Bow",
                EffectCount = 4,
                ExactBaseByDefault = false,
                RequiredEffectFragments = ["Cold Damage", "Fire Damage", "Lightning Damage", "Dexterity"],
                RequiredLocalFragments = ["Cold Damage", "Fire Damage", "Lightning Damage"],
            },
            new("Wrath Cry / Blasting Wand", item => item.SourceIndex == 4)
            {
                Category = "Wand",
                EffectCount = 6,
                RequiredEffectFragments = ["Cannot roll Caster Modifiers", "Lightning Damage to Spells", "Cold Damage"],
                RequiredSourceKinds = [("Cannot roll Caster Modifiers", "implicit")],
                ForbiddenEffectFragments = ["Unscalable Value", "Shocked enemies have increased damage taken"],
            },
            new("Eagle Spiral / Organic Ring", item => item.SourceIndex is 7 or 32)
            {
                Category = "Ring",
                EffectCount = 6,
                RequiredEffectFragments = ["additional Physical Damage Reduction", "Cannot roll Modifiers of Non-Physical Damage Types"],
                RequiredSourceKinds = [("additional Physical Damage Reduction", "implicit"), ("Cannot roll Modifiers of Non-Physical Damage Types", "implicit")],
                ForbiddenEffectFragments = ["Unscalable Value", "Our flesh longs to move as one.", "damage types are Physical"],
            },
            new("Dusk Shelter / Gladiator Plate", item => item.SourceIndex == 8)
            {
                EffectCount = 5,
                RequiredEffectFragments = ["Armour", "maximum Life"],
            },
            new("Skull Road / Conjurer Boots", item => item.SourceIndex == 9)
            {
                EffectCount = 7,
                RequiredEffectFragments = ["increased Energy Shield", "increased Stun and Block Recovery"],
            },
            new("Corruption Bond / Stygian Vise", item => item.SourceIndex == 10)
            {
                EffectCount = 6,
                RequiredEffectFragments = ["Has 1 Abyssal Socket"],
                RequiredSourceKinds = [("Has 1 Abyssal Socket", "implicit")],
                ForbiddenEffectFragments = ["Unscalable Value"],
            },
            new("Gale Wrap / Marshall's Brigandine", item => item.SourceIndex == 11)
            {
                EffectCount = 10,
                RequiredEffectFragments = ["Armour", "Evasion", "Lightning Resistance"],
                RequiredSourceKinds = [("Lightning Resistance", "crafted")],
                OutsideCurrentTradeSuccessSlice = true,
            },
            new("Miracle Bastion / Supreme Spiked Shield", item => item.SourceIndex == 13)
            {
                EffectCount = 8,
                RequiredEffectFragments =
                [
                    "+5% chance to Suppress Spell Damage",
                    "+15(11-15) to maximum Energy Shield",
                    "+27(24-28) to maximum Life",
                    "28(27-32)% increased Evasion and Energy Shield",
                    "13(12-13)% increased Stun and Block Recovery",
                    "+59(49-85) to Evasion Rating",
                    "+26(23-28) to maximum Energy Shield",
                    "+55(51-55) to Dexterity",
                ],
                RequiredSourceKinds = [("Suppress Spell Damage", "implicit")],
            },
            new("Cryonic Ring", item => item.SourceIndex == 14)
            {
                Rarity = "Rare",
                EffectCount = 2,
                MustBeUnidentified = true,
                RequiredEffectFragments =
                [
                    "+2% to maximum Cold Resistance",
                    "Cannot roll Modifiers of Non-Cold Damage Types",
                ],
                RequiredSourceKinds =
                [
                    ("+2% to maximum Cold Resistance", "implicit"),
                    ("Cannot roll Modifiers of Non-Cold Damage Types", "implicit"),
                ],
                ForbiddenEffectFragments = ["The Damage Types are Physical, Fire, Cold, Lightning, and Chaos"],
            },
            new("Apparition's Necrotic Armour of the Cloud", item => item.SourceIndex == 15)
            {
                ResolvedBaseName = "Necrotic Armour",
                Rarity = "Magic",
                EffectCount = 3,
            },
            new("Mosquito's Necrotic Armour of the Philosopher", item => item.SourceIndex == 16)
            {
                ResolvedBaseName = "Necrotic Armour",
                Rarity = "Magic",
                EffectCount = 3,
            },
            new("Agony Ram / Platinum Sceptre", item => item.SourceIndex == 18)
            {
                Rarity = "Rare",
                ParserBaseText = "Platinum Sceptre",
                ResolvedBaseName = "Platinum Sceptre",
                Category = "Sceptre",
                EffectCount = 5,
                RequiredEffectFragments =
                [
                    "30% increased Elemental Damage",
                    "Adds 2 to 28(25-29) Lightning Damage",
                    "+28(25-29)% to Global Critical Strike Multiplier",
                    "+23(20-23)% to Damage over Time Multiplier",
                    "15(13-15)% increased Cold Damage",
                ],
            },
            new("Skull Horn / Lacquered Helmet", item => item.SourceIndex == 24)
            {
                EffectCount = 5,
                RequiredDuplicateEffectFragments = ["increased Stun and Block Recovery"],
            },
            new("Cataclysm League / Slink Boots", item => item.SourceIndex == 33)
            {
                EffectCount = 5,
                RequiredDuplicateEffectFragments = ["increased Stun and Block Recovery"],
            },
            new("Normal Antique Greaves", item => item.SourceIndex == 34)
            {
                ParserBaseText = "Antique Greaves",
                EffectCount = 0,
            },
            new("Antique Greaves of the Penguin", item => item.SourceIndex == 35)
            {
                ResolvedBaseName = "Antique Greaves",
                EffectCount = 1,
                RequiredSourceKinds = [("Cold Resistance", "suffix")],
            },
            new("Rotund Antique Greaves", item => item.SourceIndex == 36)
            {
                ResolvedBaseName = "Antique Greaves",
                EffectCount = 1,
                RequiredSourceKinds = [("maximum Life", "prefix")],
            },
        ];
    }

    private sealed record AnchorExpectation(string Name, Func<AuditItem, bool> Matches)
    {
        public string? ItemClass { get; init; }

        public string? Rarity { get; init; }

        public string? ParserBaseText { get; init; }

        public string? ResolvedBaseName { get; init; }

        public string? Category { get; init; }

        public int? EffectCount { get; init; }

        public bool? ExactBaseByDefault { get; init; }

        public bool MustBeUnidentified { get; init; }

        public bool OutsideCurrentTradeSuccessSlice { get; init; }

        public IReadOnlyList<string> RequiredEffectFragments { get; init; } = [];

        public IReadOnlyList<string> RequiredLocalFragments { get; init; } = [];

        public IReadOnlyList<string> ForbiddenEffectFragments { get; init; } = [];

        public IReadOnlyList<string> RequiredDuplicateEffectFragments { get; init; } = [];

        public IReadOnlyList<(string Fragment, string Kind)> RequiredSourceKinds { get; init; } = [];

        public IEnumerable<AuditFailure> Evaluate(AuditItem item)
        {
            if (ItemClass is not null && !string.Equals(item.ItemClass, ItemClass, StringComparison.Ordinal))
            {
                yield return Failure("Parse", item, $"Expected class '{ItemClass}', got '{item.ItemClass}'.");
            }

            if (Rarity is not null && !string.Equals(item.Rarity, Rarity, StringComparison.Ordinal))
            {
                yield return Failure("Parse", item, $"Expected rarity '{Rarity}', got '{item.Rarity}'.");
            }

            if (ParserBaseText is not null && !string.Equals(item.ParserBaseText, ParserBaseText, StringComparison.Ordinal))
            {
                yield return Failure("Parse", item, $"Expected parser base text '{ParserBaseText}', got '{item.ParserBaseText}'.");
            }

            if (ResolvedBaseName is not null && !string.Equals(item.ResolvedBaseName, ResolvedBaseName, StringComparison.Ordinal))
            {
                yield return Failure("BaseRecognition", item, $"Expected resolved base '{ResolvedBaseName}', got '{item.ResolvedBaseName}'.");
            }

            if (Category is not null && !string.Equals(item.DefaultTradeCategory, Category, StringComparison.Ordinal))
            {
                yield return Failure("BaseRecognition", item, $"Expected category '{Category}', got '{item.DefaultTradeCategory}'.");
            }

            if (EffectCount is not null && item.LogicalEffectCount != EffectCount.Value)
            {
                yield return Failure("LogicalEffectExtraction", item, $"Expected {EffectCount.Value} logical effects, got {item.LogicalEffectCount}.");
            }

            if (ExactBaseByDefault is not null && item.ExactBaseSerializedByDefault != ExactBaseByDefault.Value)
            {
                yield return Failure("BaseRecognition", item, $"Expected exact-base default '{ExactBaseByDefault.Value}', got '{item.ExactBaseSerializedByDefault}'.");
            }

            if (MustBeUnidentified && !item.Unidentified)
            {
                yield return Failure("Parse", item, "Expected unidentified state to be preserved.");
            }

            foreach (var fragment in RequiredEffectFragments)
            {
                if (!item.Effects.Any(effect => Contains(effect.CleanedText, fragment)))
                {
                    yield return Failure("LogicalEffectExtraction", item, $"Missing logical effect containing '{fragment}'.");
                }
            }

            foreach (var fragment in RequiredLocalFragments)
            {
                if (!item.Effects.Any(effect => Contains(effect.CleanedText, fragment) && effect.Locality == "Local"))
                {
                    yield return Failure("Provenance", item, $"Expected local effect containing '{fragment}'.");
                }
            }

            foreach (var fragment in ForbiddenEffectFragments)
            {
                if (item.Effects.Any(effect => Contains(effect.CleanedText, fragment)))
                {
                    yield return Failure("LogicalEffectExtraction", item, $"Forbidden text remained selectable: '{fragment}'.");
                }
            }

            foreach (var fragment in RequiredDuplicateEffectFragments)
            {
                if (item.Effects.Count(effect => Contains(effect.CleanedText, fragment)) < 2)
                {
                    yield return Failure("LogicalEffectExtraction", item, $"Expected at least two separate effects containing '{fragment}'.");
                }
            }

            foreach (var (fragment, kind) in RequiredSourceKinds)
            {
                if (!item.Effects.Any(effect => Contains(effect.CleanedText, fragment) &&
                    string.Equals(effect.SourceKind, kind, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return Failure("Provenance", item, $"Expected '{fragment}' to have source kind '{kind}'.");
                }
            }

            if (OutsideCurrentTradeSuccessSlice)
            {
                item.OutsideCurrentOrdinaryTradeSuccessSlice = true;
            }
        }

        public AuditFailure Failure(
            string stage,
            AuditItem? item,
            string message)
        {
            return new AuditFailure(
                stage,
                item?.SourceIndex,
                null,
                $"{Name}: {message}");
        }

        private static bool Contains(string text, string fragment)
        {
            return text.Contains(fragment, StringComparison.OrdinalIgnoreCase);
        }
    }
}

internal static class OrdinaryItemCorpusReportWriter
{
    public static void Write(OrdinaryItemAuditReport report)
    {
        var directory = Path.Combine(FindRepoRoot(), "artifacts", "audits");
        Directory.CreateDirectory(directory);
        var jsonPath = Path.Combine(directory, "ordinary-item-corpus-audit.json");
        var markdownPath = Path.Combine(directory, "ordinary-item-corpus-audit.md");
        File.WriteAllText(
            jsonPath,
            JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                }),
            Encoding.UTF8);
        File.WriteAllText(markdownPath, ToMarkdown(report), Encoding.UTF8);
    }

    private static string ToMarkdown(OrdinaryItemAuditReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Ordinary Item Corpus Audit");
        builder.AppendLine();
        builder.AppendLine($"- Input blocks: {report.Corpus.InputBlockCount}");
        builder.AppendLine($"- Unique normalized hashes: {report.Corpus.UniqueHashCount}");
        builder.AppendLine($"- Live catalog audit: {(report.LiveCatalog?.WasRun == true ? (report.LiveCatalog.CatalogLoaded ? "loaded" : "failed") : "not run")}");
        builder.AppendLine($"- Failure count: {report.Failures.Count}");
        builder.AppendLine($"- Complete Ordinary Pipeline Pass: {report.Classification.CompleteOrdinaryPipelinePass.Count}");
        builder.AppendLine($"- Partial / Expected Unsupported: {report.Classification.PartialExpectedUnsupported.Count}");
        builder.AppendLine($"- Failed: {report.Classification.Failed.Count}");
        builder.AppendLine();
        builder.AppendLine("## Duplicate Detection");
        foreach (var duplicate in report.Corpus.Duplicates)
        {
            builder.AppendLine($"- {duplicate.DisplayName} / {duplicate.BaseName}: {string.Join(", ", duplicate.SourceIndexes)} ({duplicate.Hash})");
        }

        AppendCounts(builder, "Counts By Class", report.CountsByClass);
        AppendCounts(builder, "Counts By Rarity", report.CountsByRarity);
        AppendCounts(builder, "Failure Counts By Stage", report.FailureCountsByStage);
        AppendFailures(builder, "Parse Failures", report.Failures.Where(failure => failure.Stage == "Parse"));
        AppendFailures(builder, "Base Recognition Failures", report.Failures.Where(failure => failure.Stage == "BaseRecognition"));
        AppendFailures(builder, "Logical Effect Count Mismatches", report.Failures.Where(failure =>
            failure.Stage == "LogicalEffectExtraction" &&
            failure.Message.Contains("logical effects", StringComparison.OrdinalIgnoreCase)));
        AppendFailures(builder, "Provenance Failures", report.Failures.Where(failure => failure.Stage == "Provenance"));
        AppendFailures(builder, "Missing Provider Matches", report.Failures.Where(failure =>
            failure.Stage == "ProviderResolution" &&
            failure.Message.Contains("missing", StringComparison.OrdinalIgnoreCase)));
        AppendFailures(builder, "Ambiguous Provider Matches", report.Failures.Where(failure =>
            failure.Stage == "ProviderResolution" &&
            failure.Message.Contains("ambiguous", StringComparison.OrdinalIgnoreCase)));
        AppendFailures(builder, "Effects Silently Omitted From Final Query", report.Failures.Where(failure => failure.Message.Contains("silently", StringComparison.OrdinalIgnoreCase)));
        AppendFailures(builder, "Selected Count Versus Final Filter Count Mismatches", report.Failures.Where(failure => failure.Message.Contains("Selected-count", StringComparison.OrdinalIgnoreCase)));
        AppendFailures(builder, "Failures By First Responsible Stage", report.Failures.OrderBy(failure => failure.Stage, StringComparer.Ordinal));
        AppendFailures(builder, "Ten Highest Priority Defects", report.Failures.Take(10));

        AppendClassification(builder, "Complete Ordinary Pipeline Pass", report.Classification.CompleteOrdinaryPipelinePass);
        AppendClassification(builder, "Partial / Expected Unsupported", report.Classification.PartialExpectedUnsupported);
        AppendClassification(builder, "Failed", report.Classification.Failed);

        return builder.ToString();
    }

    private static void AppendCounts(
        StringBuilder builder,
        string title,
        IReadOnlyDictionary<string, int> counts)
    {
        builder.AppendLine();
        builder.AppendLine($"## {title}");
        foreach (var (key, value) in counts)
        {
            builder.AppendLine($"- {key}: {value}");
        }
    }

    private static void AppendFailures(
        StringBuilder builder,
        string title,
        IEnumerable<AuditFailure> failures)
    {
        var materialized = failures.ToArray();
        builder.AppendLine();
        builder.AppendLine($"## {title}");
        if (materialized.Length == 0)
        {
            builder.AppendLine("- None");
            return;
        }

        foreach (var failure in materialized)
        {
            builder.AppendLine($"- [{failure.Stage}] item {failure.SourceIndex?.ToString() ?? "-"} effect {failure.EffectIndex?.ToString() ?? "-"}: {failure.Message}{(string.IsNullOrWhiteSpace(failure.Diagnostic) ? string.Empty : $" ({failure.Diagnostic})")}");
        }
    }

    private static void AppendClassification(
        StringBuilder builder,
        string title,
        IReadOnlyList<ClassifiedCorpusItem> items)
    {
        builder.AppendLine();
        builder.AppendLine($"## {title}");
        if (items.Count == 0)
        {
            builder.AppendLine("- None");
            return;
        }

        foreach (var item in items.OrderBy(item => item.SourceIndexes[0]))
        {
            builder.AppendLine($"- {item.Label} [{string.Join(", ", item.SourceIndexes)}] failures={item.FailureCount}");
            foreach (var unsupported in item.UnsupportedEffects)
            {
                builder.AppendLine($"  - expected unsupported effect {unsupported.SourceIndex?.ToString() ?? "-"}: {unsupported.Text} ({unsupported.Diagnostic})");
            }
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find PoEnhance repo root.");
    }
}

internal static class OrdinaryItemCorpusAuditAssertions
{
    public static void AssertReportConsistency(OrdinaryItemAuditReport report)
    {
        var complete = report.Classification.CompleteOrdinaryPipelinePass;
        var partial = report.Classification.PartialExpectedUnsupported;
        var failed = report.Classification.Failed;
        var classified = complete.Concat(partial).Concat(failed).ToArray();
        var classifiedHashes = classified.Select(item => item.Hash).ToArray();

        Assert.Equal(report.Corpus.UniqueHashCount, classifiedHashes.Length);
        Assert.Equal(report.Corpus.UniqueHashCount, classifiedHashes.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            report.Corpus.UniqueHashCount,
            complete.Count + partial.Count + failed.Count);
        Assert.Equal(
            report.Corpus.UniqueHashCount,
            report.Items.Select(item => item.Hash).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(report.Corpus.Duplicates, duplicate => duplicate.SourceIndexes.Count == 2);

        var completeHashes = complete.Select(item => item.Hash).ToHashSet(StringComparer.Ordinal);
        var partialHashes = partial.Select(item => item.Hash).ToHashSet(StringComparer.Ordinal);
        var failedHashes = failed.Select(item => item.Hash).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(completeHashes.Intersect(failedHashes, StringComparer.Ordinal));
        Assert.Empty(completeHashes.Intersect(partialHashes, StringComparer.Ordinal));
        Assert.Empty(partialHashes.Intersect(failedHashes, StringComparer.Ordinal));

        var hashBySourceIndex = report.Items.ToDictionary(item => item.SourceIndex, item => item.Hash);
        foreach (var failure in report.Failures.Where(failure => failure.SourceIndex is not null))
        {
            Assert.DoesNotContain(hashBySourceIndex[failure.SourceIndex!.Value], completeHashes);
            Assert.DoesNotContain(hashBySourceIndex[failure.SourceIndex.Value], partialHashes);
        }

        foreach (var item in report.Items)
        {
            if (item.Live?.Effects.Any(effect => effect.Result == "Missing") == true)
            {
                Assert.DoesNotContain(item.Hash, completeHashes);
            }

            if (item.Failures.Any(failure => failure.Stage == "QueryBuild"))
            {
                Assert.DoesNotContain(item.Hash, completeHashes);
            }

            if (item.Failures.Count > 0)
            {
                Assert.Contains(item.Hash, failedHashes);
            }

            if (item.Live?.Effects.Any(effect => effect.Result == "UnsupportedSpecial") == true)
            {
                Assert.DoesNotContain(item.Hash, completeHashes);
                if (item.Failures.Count == 0)
                {
                    Assert.Contains(item.Hash, partialHashes);
                }
            }
        }

        Assert.Equal(report.Failures.Count, report.Items.Sum(item => item.Failures.Count));
        Assert.Equal(
            report.FailureCountsByStage,
            report.Failures
                .GroupBy(failure => failure.Stage, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
    }

    public static void AssertMarkdownAndJsonAgree()
    {
        var root = FindRepoRoot();
        var jsonPath = Path.Combine(root, "artifacts", "audits", "ordinary-item-corpus-audit.json");
        var markdownPath = Path.Combine(root, "artifacts", "audits", "ordinary-item-corpus-audit.md");
        using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var rootElement = document.RootElement;
        var markdown = File.ReadAllText(markdownPath);

        Assert.Equal(
            rootElement.GetProperty("failures").GetArrayLength(),
            MarkdownCount(markdown, "Failure count"));
        Assert.Equal(
            rootElement.GetProperty("classification").GetProperty("completeOrdinaryPipelinePass").GetArrayLength(),
            MarkdownCount(markdown, "Complete Ordinary Pipeline Pass"));
        Assert.Equal(
            rootElement.GetProperty("classification").GetProperty("partialExpectedUnsupported").GetArrayLength(),
            MarkdownCount(markdown, "Partial / Expected Unsupported"));
        Assert.Equal(
            rootElement.GetProperty("classification").GetProperty("failed").GetArrayLength(),
            MarkdownCount(markdown, "Failed"));
    }

    private static int MarkdownCount(string markdown, string label)
    {
        var match = Regex.Match(
            markdown,
            $@"^- {Regex.Escape(label)}: (?<count>\d+)\r?$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"Markdown count not found: {label}");
        return int.Parse(match.Groups["count"].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find PoEnhance repo root.");
    }
}

internal sealed record CorpusFixture(
    string Path,
    IReadOnlyList<CorpusBlock> Blocks,
    IReadOnlyList<CorpusDuplicate> Duplicates);

internal sealed record CorpusBlock
{
    public required int SourceIndex { get; init; }

    public required string RawText { get; init; }

    public required string NormalizedFullTextHash { get; init; }

    public string? DisplayName { get; init; }

    public string? BaseName { get; init; }

    public static CorpusBlock Create(int sourceIndex, string rawText)
    {
        var lines = rawText.ReplaceLineEndings("\n").Split('\n');
        var rarityIndex = Array.FindIndex(lines, line => line.StartsWith("Rarity:", StringComparison.Ordinal));
        var rarity = rarityIndex >= 0 ? lines[rarityIndex].Split(':', 2)[1].Trim() : null;
        var displayName = rarityIndex >= 0 && rarityIndex + 1 < lines.Length
            ? lines[rarityIndex + 1].Trim()
            : null;
        var baseName = string.Equals(rarity, "Rare", StringComparison.OrdinalIgnoreCase) &&
            rarityIndex + 2 < lines.Length
            ? lines[rarityIndex + 2].Trim()
            : displayName;
        var normalized = rawText.ReplaceLineEndings("\n").Trim();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return new CorpusBlock
        {
            SourceIndex = sourceIndex,
            RawText = rawText,
            NormalizedFullTextHash = Convert.ToHexString(bytes).ToLowerInvariant(),
            DisplayName = displayName,
            BaseName = baseName,
        };
    }
}

internal sealed record CorpusDuplicate
{
    public required string Hash { get; init; }

    public required IReadOnlyList<int> SourceIndexes { get; init; }

    public string? DisplayName { get; init; }

    public string? BaseName { get; init; }
}

internal sealed record OrdinaryItemAuditReport
{
    public required CorpusAuditSummary Corpus { get; init; }

    public IReadOnlyDictionary<string, int> CountsByClass { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CountsByRarity { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyList<AuditItem> Items { get; init; } = [];

    public IReadOnlyList<AuditFailure> Failures { get; init; } = [];

    public IReadOnlyDictionary<string, int> FailureCountsByStage { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyList<AuditFailure> AnchorFailures { get; init; } = [];

    public LiveCatalogAuditSummary? LiveCatalog { get; init; }

    public ClassificationSummary Classification { get; init; } = new();

    public IReadOnlyList<string> CompletePipelinePassedCases { get; init; } = [];
}

internal sealed record ClassificationSummary
{
    public IReadOnlyList<ClassifiedCorpusItem> CompleteOrdinaryPipelinePass { get; init; } = [];

    public IReadOnlyList<ClassifiedCorpusItem> PartialExpectedUnsupported { get; init; } = [];

    public IReadOnlyList<ClassifiedCorpusItem> Failed { get; init; } = [];
}

internal sealed record ClassifiedCorpusItem
{
    public required string Hash { get; init; }

    public required string Label { get; init; }

    public required IReadOnlyList<int> SourceIndexes { get; init; }

    public int FailureCount { get; init; }

    public IReadOnlyList<string> FailureStages { get; init; } = [];

    public IReadOnlyList<ExpectedUnsupportedEffect> UnsupportedEffects { get; init; } = [];
}

internal sealed record ExpectedUnsupportedEffect
{
    public int? SourceIndex { get; init; }

    public required string Text { get; init; }

    public required string Diagnostic { get; init; }
}

internal sealed record CorpusAuditSummary
{
    public required int InputBlockCount { get; init; }

    public required int UniqueHashCount { get; init; }

    public required IReadOnlyList<CorpusDuplicate> Duplicates { get; init; }
}

internal sealed record AuditItem
{
    public required int SourceIndex { get; init; }

    public required string Hash { get; init; }

    public required string Label { get; init; }

    public string? RawDisplayName { get; init; }

    public string? RawBaseName { get; init; }

    public bool ParseSucceeded { get; init; }

    public string? ItemClass { get; init; }

    public string? Rarity { get; init; }

    public string? DisplayName { get; init; }

    public string? ParserBaseText { get; init; }

    public IReadOnlyList<string> ItemStates { get; init; } = [];

    public bool Unidentified { get; init; }

    public string? BaseRecognitionStatus { get; init; }

    public string? ResolvedBaseName { get; init; }

    public string? DefaultTradeCategory { get; init; }

    public bool ExactBaseSerializedByDefault { get; init; }

    public int LogicalEffectCount { get; init; }

    public IReadOnlyList<AuditEffect> Effects { get; init; } = [];

    public IReadOnlyList<ModifierResolutionSummary> ModifierResolutionSummaries { get; init; } = [];

    public IReadOnlyList<string> ValidationDiagnosticCodes { get; init; } = [];

    public LiveItemAudit? Live { get; init; }

    public IReadOnlyList<AuditFailure> Failures { get; init; } = [];

    public bool OutsideCurrentOrdinaryTradeSuccessSlice { get; set; }
}

internal sealed record AuditEffect
{
    public required string ComponentId { get; init; }

    public int SourceModifierIndex { get; init; }

    public int SourceLineIndex { get; init; }

    public required string CleanedText { get; init; }

    public string? NormalizedTemplate { get; init; }

    public IReadOnlyList<decimal> ExtractedNumericValues { get; init; } = [];

    public required string SourceKind { get; init; }

    public string? ParsedKind { get; init; }

    public string? SourceModifierName { get; init; }

    public int? Tier { get; init; }

    public int? Rank { get; init; }

    public bool IsCrafted { get; init; }

    public string? Locality { get; init; }

    public bool ReminderTextExcluded { get; init; }

    public bool UnscalableValueExcluded { get; init; }

    public bool FlavourTextExcluded { get; init; }

    public string? ResolutionStatus { get; init; }

    public string? ResolvedModifierId { get; init; }

    public string? ResolvedModifierName { get; init; }

    public IReadOnlyList<string> ResolvedStatIds { get; init; } = [];

    public bool IsSearchable { get; init; }

    public string? NotSearchableReason { get; init; }
}

internal sealed record ModifierResolutionSummary
{
    public int ParsedModifierIndex { get; init; }

    public string? Status { get; init; }

    public int CandidateCount { get; init; }

    public string? GenerationType { get; init; }

    public string? Locality { get; init; }

    public string? ParsedModifierName { get; init; }
}

internal sealed record LiveCatalogAuditSummary
{
    public bool WasRun { get; init; }

    public bool CatalogLoaded { get; init; }

    public IReadOnlyList<string> DiagnosticCodes { get; init; } = [];
}

internal sealed record LiveItemAudit
{
    public IReadOnlyList<LiveEffectAudit> Effects { get; init; } = [];

    public SelectedQueryAudit? CombinedUniqueQuery { get; init; }

    public IReadOnlyList<AuditFailure> Failures { get; init; } = [];
}

internal sealed record LiveEffectAudit
{
    public required string ComponentId { get; init; }

    public int SourceIndex { get; init; }

    public required string CleanedMatchingTemplate { get; init; }

    public string? SourceProvenance { get; init; }

    public string? Locality { get; init; }

    public IReadOnlyList<ProviderCandidateAudit> CompatibleCandidates { get; init; } = [];

    public IReadOnlyList<ProviderCandidateRejectionAudit> RejectedCandidates { get; init; } = [];

    public required string Result { get; init; }

    public string? SelectedStatId { get; init; }

    public string? ProviderResolutionStatus { get; init; }

    public string? ProviderDiagnosticCode { get; init; }

    public SelectedQueryAudit? SingleSelectedQuery { get; init; }
}

internal sealed record ProviderCandidateAudit
{
    public string? StatId { get; init; }

    public string? GroupId { get; init; }

    public string? GroupLabel { get; init; }

    public string? Type { get; init; }

    public string? ProviderKind { get; init; }

    public string? ProviderLocality { get; init; }

    public string? NormalizedText { get; init; }
}

internal sealed record ProviderCandidateRejectionAudit
{
    public required ProviderCandidateAudit Candidate { get; init; }

    public required string Reason { get; init; }
}

internal sealed record SelectedQueryAudit
{
    public int SelectedEffectCount { get; init; }

    public bool MappingSucceeded { get; init; }

    public IReadOnlyList<string> MappingDiagnosticCodes { get; init; } = [];

    public IReadOnlyList<int> MappingSourceIndexes { get; init; } = [];

    public bool QuerySucceeded { get; init; }

    public IReadOnlyList<string> QueryDiagnosticCodes { get; init; } = [];

    public int EnabledFilterCount { get; init; }

    public IReadOnlyList<string> SerializedStatIds { get; init; } = [];

    public IReadOnlyList<decimal> SerializedMinValues { get; init; } = [];

    public IReadOnlyList<decimal> SerializedMaxValues { get; init; } = [];

    public IReadOnlyList<string> OmittedEffects { get; init; } = [];

    public IReadOnlyList<string> DuplicatedProviderIds { get; init; } = [];

    public string? StatsGroupOperator { get; init; }

    public bool ExactBaseSerialized { get; init; }

    public string? SerializedJson { get; init; }
}

internal sealed record AuditFailure(
    string Stage,
    int? SourceIndex,
    int? EffectIndex,
    string Message,
    string? Diagnostic = null);

internal sealed class StaticStatCatalogProvider : IPathOfExileTradeStatCatalogProvider
{
    private readonly PathOfExileTradeStatCatalog catalog;

    public StaticStatCatalogProvider(PathOfExileTradeStatCatalog catalog)
    {
        this.catalog = catalog;
    }

    public Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PathOfExileTradeStatCatalogProviderResult.Success(catalog));
    }
}

internal sealed class StaticFilterCatalogProvider : IPathOfExileTradeFilterCatalogProvider
{
    private readonly PathOfExileTradeFilterCatalog catalog;

    public StaticFilterCatalogProvider(PathOfExileTradeFilterCatalog catalog)
    {
        this.catalog = catalog;
    }

    public Task<PathOfExileTradeFilterCatalogProviderResult> GetCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PathOfExileTradeFilterCatalogProviderResult.Success(catalog));
    }
}

internal sealed class ThrowingSearchClient : IPathOfExileTradeSearchClient
{
    public Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
        PathOfExileTradeSearchRequest? request,
        string? leagueIdentifier,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Ordinary item corpus audit must not call Trade Search.");
    }
}

internal sealed class ThrowingFetchClient : IPathOfExileTradeFetchClient
{
    public Task<PathOfExileTradeFetchExecutionResult> FetchAsync(
        string? queryId,
        IReadOnlyList<string?>? resultIds,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Ordinary item corpus audit must not call Trade Fetch.");
    }
}

internal sealed class ThrowingItemCatalogProvider : IPathOfExileTradeItemCatalogProvider
{
    public Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Ordinary item corpus audit must not call the Trade item catalog.");
    }
}

internal sealed class ThrowingItemIdentityMapper : IPathOfExileTradeItemIdentityMapper
{
    public PathOfExileTradeItemIdentityMappingResult Map(
        TradeSearchDraft? draft,
        PathOfExileTradeItemCatalog? catalog)
    {
        throw new InvalidOperationException("Ordinary item corpus audit does not map Unique item identity.");
    }
}
