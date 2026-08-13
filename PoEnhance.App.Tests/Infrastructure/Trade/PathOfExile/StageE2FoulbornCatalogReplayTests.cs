using System.Text;
using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class StageE2FoulbornCatalogReplayTests
{
    [Fact]
    public async Task CurrentFoulbornRelationships_AreExplicitAndSafeThroughFinalQuery()
    {
        var gameDataPath = Environment.GetEnvironmentVariable("POENHANCE_STAGE_E2_GAME_DATA_PATH");
        var liveDataDirectory = Environment.GetEnvironmentVariable("POENHANCE_STAGE_E2_LIVE_DATA_DIR");
        var reportPath = Environment.GetEnvironmentVariable("POENHANCE_STAGE_E2_REPLAY_REPORT_PATH");
        var csvPath = Environment.GetEnvironmentVariable("POENHANCE_STAGE_E2_REPLAY_CSV_PATH");
        if (string.IsNullOrWhiteSpace(gameDataPath) ||
            string.IsNullOrWhiteSpace(liveDataDirectory) ||
            string.IsNullOrWhiteSpace(reportPath) ||
            string.IsNullOrWhiteSpace(csvPath))
        {
            return;
        }

        var load = await GameDataPackageLoader.LoadFromFileAsync(gameDataPath);
        Assert.True(load.IsSuccess);
        var package = Assert.IsType<GameDataPackage>(load.Package);
        var gameData = GameDataCatalog.FromPackage(package);
        var uniqueItems = Assert.IsType<UniqueItemCatalog>(package.UniqueItems);
        var statCatalogResult = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(
            File.ReadAllText(Path.Combine(liveDataDirectory, "stats.json")));
        Assert.True(statCatalogResult.IsSuccess);
        var statCatalog = Assert.IsType<PathOfExileTradeStatCatalog>(statCatalogResult.Catalog);
        var matcher = new ModifierTextSignatureMatcher();
        var providerService = CreateProviderService();
        var selectedMapper = new PathOfExileTradeSelectedModifierMapper();
        var queryBuilder = new PathOfExileTradeQueryBuilder();
        var validator = new TradeSearchDraftValidator();
        var identities = uniqueItems.Items
            .Where(identity => !string.IsNullOrWhiteSpace(identity.Id))
            .ToDictionary(identity => identity.Id!, StringComparer.OrdinalIgnoreCase);
        var rows = new List<ReplayRow>();

        foreach (var relationship in uniqueItems.FoulbornModifierRelationships)
        {
            if (relationship.Status != UniqueFoulbornModifierRelationshipStatus.Exact ||
                string.IsNullOrWhiteSpace(relationship.UniqueItemId) ||
                !identities.TryGetValue(relationship.UniqueItemId, out var identity))
            {
                rows.Add(Row(
                    relationship,
                    "LEGITIMATELY_UNSUPPORTED",
                    relationship.DiagnosticCode,
                    relationship.Diagnostic));
                continue;
            }

            var modifiers = gameData.FindModifiersById(relationship.FoulbornModifierId);
            if (modifiers.Count != 1)
            {
                rows.Add(Row(
                    relationship,
                    "MECHANICS_UNRESOLVED",
                    "FOULBORN_REPLACEMENT_MODIFIER_AMBIGUOUS",
                    "The replacement modifier id did not resolve to exactly one GameData record."));
                continue;
            }

            var modifier = modifiers[0];
            var statIds = modifier.Stats
                .Where(stat => !string.IsNullOrWhiteSpace(stat.StatId))
                .OrderBy(stat => stat.Index)
                .Select(stat => stat.StatId!.Trim())
                .ToArray();
            var probe = matcher.Match(modifier, gameData, ["poenhance-stage-e2-probe"]);
            if (statIds.Length == 0 || probe.CandidateSignatures.Count != 1)
            {
                rows.Add(Row(
                    relationship,
                    "MECHANICS_UNRESOLVED",
                    probe.ReasonCode,
                    probe.Reason));
                continue;
            }

            var canonicalSignature = string.Join("\n", probe.CandidateSignatures[0].Lines);
            var copiedText = canonicalSignature.Replace("<number>", "1", StringComparison.Ordinal);
            var component = new ResolvedSearchComponent
            {
                ComponentId = $"foulborn:{relationship.Id}",
                SourceModifierIndex = 0,
                SourceLineIndex = copiedText.Contains('\n') ? -1 : 0,
                OriginalText = copiedText,
                CanonicalSignature = canonicalSignature,
                ParsedKind = ParsedModifierKind.Unique,
                UniqueOrigin = ParsedUniqueModifierOrigin.Foulborn,
                GenerationType = modifier.GenerationType,
                ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                ResolvedModifierId = modifier.Id,
                ResolvedModifierName = modifier.Name,
                ResolvedStatIds = statIds,
                ResolvedStatLocalities = statIds.Select(statId =>
                {
                    var stats = gameData.FindStatsById(statId);
                    return stats.Count == 1
                        ? stats[0].IsLocal ? ModifierLocality.Local : ModifierLocality.Global
                        : ModifierLocality.Unknown;
                }).ToArray(),
                ProviderSearchSignatures = [canonicalSignature],
                UniqueFoulbornRelationshipIds = [relationship.Id!],
                UniqueNormalCounterpartModifierIds = [relationship.NormalModifierId!],
                UniqueSourceObservationIds = [relationship.SourceObservationId!],
                IsSearchable = true,
                ValueBoundShape = ModifierBoundShape.PresenceOnly,
            };
            var draft = Draft(identity, component);
            var providerIdentity = new PathOfExileTradeItemIdentity
            {
                CanonicalName = identity.CanonicalName!,
                CanonicalType = identity.BaseTypeEvidence[0],
                Foulborn = TradeTriState.Yes,
            };
            var resolvedDraft = providerService.ResolveProviderComponents(
                draft,
                statCatalog,
                providerIdentity);
            var resolved = Assert.Single(resolvedDraft.ModifierFilters);
            if (resolved.ProviderResolutionStatus is not (
                    SearchComponentProviderResolutionStatus.Exact or
                    SearchComponentProviderResolutionStatus.ExactEquivalentSet) ||
                !resolved.IsSearchable)
            {
                var classification = resolved.ProviderResolutionStatus ==
                    SearchComponentProviderResolutionStatus.Ambiguous
                        ? "AMBIGUOUS"
                        : "PROVIDER_MAPPING_GAP";
                rows.Add(Row(
                    relationship,
                    classification,
                    resolved.ProviderDiagnosticCode,
                    resolved.ProviderDiagnosticMessage ?? resolved.NotSearchableReason,
                    canonicalSignature,
                    resolved.ProviderResolutionStatus.ToString()));
                continue;
            }

            var selectedDraft = resolvedDraft with
            {
                ModifierFilters = [resolved with { IsSelected = true }],
            };
            var mapping = selectedMapper.Map(selectedDraft, statCatalog);
            var query = mapping.IsSuccess
                ? queryBuilder.Build(
                    selectedDraft,
                    validator.Validate(selectedDraft),
                    "Allflame",
                    mapping.Filters,
                    providerIdentity)
                : null;
            var reachesFinalQuery = mapping.IsSuccess && query?.IsSuccess == true;
            rows.Add(Row(
                relationship,
                reachesFinalQuery ? "SAFE" : "LATE_REJECT",
                reachesFinalQuery
                    ? null
                    : mapping.Diagnostics.FirstOrDefault()?.Code ?? query?.Diagnostics.FirstOrDefault()?.Code,
                reachesFinalQuery
                    ? null
                    : mapping.Diagnostics.FirstOrDefault()?.Message ?? query?.Diagnostics.FirstOrDefault()?.Message,
                canonicalSignature,
                resolved.ProviderResolutionStatus.ToString(),
                resolved.ProviderStatId,
                resolved.ProviderStatAlternativeIds,
                reachesFinalQuery));
        }

        var classifications = rows
            .GroupBy(row => row.Classification, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var itemClassifications = rows
            .GroupBy(row => row.ItemName, StringComparer.Ordinal)
            .Select(group =>
            {
                var safe = group.Count(row => row.Classification == "SAFE");
                var unresolved = group.Count(row => row.Classification == "LEGITIMATELY_UNSUPPORTED");
                var mappingGaps = group.Count(row => row.Classification is
                    "PROVIDER_MAPPING_GAP" or "AMBIGUOUS" or "MECHANICS_UNRESOLVED");
                var classification = unresolved > 0
                    ? "DATA_GAP"
                    : safe == group.Count()
                        ? "FULLY_SUPPORTED"
                        : safe > 0
                            ? "PARTIALLY_SUPPORTED"
                            : mappingGaps > 0
                                ? "MAPPING_GAP"
                                : "LEGITIMATELY_UNSUPPORTED";
                return new ReplayItem(group.Key, group.Count(), safe, classification);
            })
            .OrderBy(item => item.ItemName, StringComparer.Ordinal)
            .ToArray();
        var itemCounts = itemClassifications
            .GroupBy(item => item.Classification, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var report = new ReplayReport(
            package.Manifest.SchemaVersion,
            package.Manifest.DataVersion!,
            uniqueItems.FoulbornRelationshipSources.Single().SourceFileSha256!,
            rows.Count,
            classifications,
            itemClassifications.Length,
            itemCounts,
            itemClassifications,
            rows);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
        File.WriteAllText(csvPath, ToCsv(rows));

        Assert.Equal(353, rows.Count);
        Assert.Equal(0, Count(classifications, "LATE_REJECT"));
        Assert.All(rows.Where(row => row.Classification == "SAFE"), row => Assert.True(row.ReachesFinalQuery));
        Assert.Equal(4, Count(classifications, "LEGITIMATELY_UNSUPPORTED"));
    }

    private static TradeSearchDraft Draft(
        UniqueItemIdentity identity,
        ResolvedSearchComponent component)
    {
        var baseType = identity.BaseTypeEvidence[0];
        return new TradeSearchDraft
        {
            ItemClass = "Unique",
            Rarity = "Unique",
            DisplayName = $"Foulborn {identity.CanonicalName}",
            ParsedBaseType = baseType,
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = $"base:{baseType}",
                ResolvedBaseName = baseType,
                Observed = new ObservedBaseIdentity
                {
                    Status = ItemBaseResolutionStatus.Exact,
                    ExactBaseId = $"base:{baseType}",
                    ExactBaseName = baseType,
                },
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    ExactBase = new BaseSearchCriterion
                    {
                        Mode = BaseSearchMode.ExactBase,
                        ExactBaseName = baseType,
                    },
                },
                ActiveCriterion = new BaseSearchCriterion
                {
                    Mode = BaseSearchMode.ExactBase,
                    ExactBaseName = baseType,
                },
            },
            ItemVariantCriteria = new TradeItemVariantCriteria { Foulborn = TradeTriState.Yes },
            ModifierFilters = [component],
        };
    }

    private static ReplayRow Row(
        UniqueFoulbornModifierRelationship relationship,
        string classification,
        string? diagnosticCode,
        string? diagnostic,
        string? canonicalSignature = null,
        string? providerStatus = null,
        string? providerStatId = null,
        IReadOnlyList<string>? providerAlternativeIds = null,
        bool reachesFinalQuery = false) => new(
            relationship.Id!,
            relationship.ItemName!,
            relationship.UniqueItemId,
            relationship.NormalModifierId!,
            relationship.FoulbornModifierId!,
            relationship.NormalModifierBlockIds.Count,
            relationship.SourceObservationId!,
            classification,
            canonicalSignature,
            providerStatus,
            providerStatId,
            providerAlternativeIds ?? [],
            reachesFinalQuery,
            diagnosticCode,
            diagnostic);

    private static int Count(IReadOnlyDictionary<string, int> counts, string key) =>
        counts.TryGetValue(key, out var count) ? count : 0;

    private static string ToCsv(IEnumerable<ReplayRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("relationshipId,itemName,uniqueItemId,normalModifierId,foulbornModifierId,normalBlockLinks,sourceObservationId,classification,providerStatus,providerStatId,providerAlternativeIds,reachesFinalQuery,diagnosticCode,diagnostic");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Csv(row.RelationshipId),
                Csv(row.ItemName),
                Csv(row.UniqueItemId),
                Csv(row.NormalModifierId),
                Csv(row.FoulbornModifierId),
                row.NormalBlockLinks.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Csv(row.SourceObservationId),
                Csv(row.Classification),
                Csv(row.ProviderStatus),
                Csv(row.ProviderStatId),
                Csv(string.Join(';', row.ProviderAlternativeIds)),
                row.ReachesFinalQuery ? "true" : "false",
                Csv(row.DiagnosticCode),
                Csv(row.Diagnostic),
            }));
        }
        return builder.ToString();
    }

    private static string Csv(string? value) =>
        $"\"{(value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static PathOfExileTradePriceCheckService CreateProviderService() => new(
        new PathOfExileTradeQueryBuilder(),
        new PathOfExileTradeStatMatcher(),
        new UnusedStatCatalogProvider(),
        new UnusedItemCatalogProvider(),
        new PathOfExileTradeSelectedModifierMapper(),
        new PathOfExileTradeItemIdentityMapper(),
        new UnusedSearchClient(),
        new UnusedFetchClient());

    private sealed class UnusedStatCatalogProvider : IPathOfExileTradeStatCatalogProvider
    {
        public Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Catalog provider is not used by this focused replay.");
    }

    private sealed class UnusedItemCatalogProvider : IPathOfExileTradeItemCatalogProvider
    {
        public Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Item provider is not used by this focused replay.");
    }

    private sealed class UnusedSearchClient : IPathOfExileTradeSearchClient
    {
        public Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
            PathOfExileTradeSearchRequest? request,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Network search is not used by this focused replay.");
    }

    private sealed class UnusedFetchClient : IPathOfExileTradeFetchClient
    {
        public Task<PathOfExileTradeFetchExecutionResult> FetchAsync(
            string? queryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Network fetch is not used by this focused replay.");
    }

    private sealed record ReplayReport(
        int SchemaVersion,
        string DataVersion,
        string SourceSha256,
        int RelationshipCount,
        IReadOnlyDictionary<string, int> RelationshipClassifications,
        int ItemCount,
        IReadOnlyDictionary<string, int> ItemClassifications,
        IReadOnlyList<ReplayItem> Items,
        IReadOnlyList<ReplayRow> Rows);

    private sealed record ReplayItem(
        string ItemName,
        int Relationships,
        int SafeRelationships,
        string Classification);

    private sealed record ReplayRow(
        string RelationshipId,
        string ItemName,
        string? UniqueItemId,
        string NormalModifierId,
        string FoulbornModifierId,
        int NormalBlockLinks,
        string SourceObservationId,
        string Classification,
        string? CanonicalSignature,
        string? ProviderStatus,
        string? ProviderStatId,
        IReadOnlyList<string> ProviderAlternativeIds,
        bool ReachesFinalQuery,
        string? DiagnosticCode,
        string? Diagnostic);
}
