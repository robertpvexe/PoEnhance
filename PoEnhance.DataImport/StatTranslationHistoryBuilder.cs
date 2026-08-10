using System.Security.Cryptography;
using System.Text;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

internal static class StatTranslationHistoryBuilder
{
    public const string CurrentSnapshotId = "repoe-current-stat-translations";
    public const string HistoricalSnapshotId = "repoe-historical-stat-translations";

    public static StatTranslationHistoryCatalog Build(
        string currentRepositoryUri,
        string currentCommitSha,
        string currentDataVersion,
        IReadOnlyList<ModifierDefinition> currentModifiers,
        IReadOnlyList<StatDefinition> currentStats,
        IReadOnlyList<StatTranslationDefinition> currentTranslations,
        string historicalRepositoryUri,
        string historicalCommitSha,
        string historicalDataVersion,
        IReadOnlyList<ModifierDefinition> historicalModifiers,
        IReadOnlyList<StatDefinition> historicalStats,
        IReadOnlyList<StatTranslationDefinition> historicalTranslations)
    {
        var currentByVector = UniqueTranslations(currentTranslations);
        var historicalByVector = UniqueTranslations(historicalTranslations);
        var currentStatsById = StatsById(currentStats);
        var historicalStatsById = StatsById(historicalStats);
        var observations = new List<StatTranslationObservation>();
        var changes = new List<StatTranslationCompatibilityChange>();

        foreach (var vector in currentByVector.Keys
            .Union(historicalByVector.Keys, StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal))
        {
            var hasCurrent = currentByVector.TryGetValue(vector, out var current);
            var hasHistorical = historicalByVector.TryGetValue(vector, out var historical);
            if (!hasCurrent || !hasHistorical)
            {
                continue;
            }

            if (string.Equals(
                    StatTranslationStructuralSemantics.RenderingSignature(current!),
                    StatTranslationStructuralSemantics.RenderingSignature(historical!),
                    StringComparison.Ordinal))
            {
                continue;
            }

            var currentUsage = FindUsage(current!.StatIds, currentModifiers);
            var historicalUsage = FindUsage(historical!.StatIds, historicalModifiers);
            var allUsage = currentUsage.Concat(historicalUsage)
                .DistinctBy(modifier => modifier.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var specialOnly = allUsage.Length > 0 &&
                (allUsage.All(IsSpecialOnly) || IsSpecialVector(current.StatIds));
            var currentLocalities = Localities(current.StatIds, currentStatsById);
            var historicalLocalities = Localities(historical.StatIds, historicalStatsById);
            var comparison = StatTranslationCompatibilityClassifier.Compare(
                current,
                historical,
                allUsage.Length,
                specialOnly,
                currentLocalities,
                historicalLocalities);
            var keyHash = Hash(vector);
            var currentObservationId = $"{CurrentSnapshotId}:{keyHash}";
            var historicalObservationId = $"{HistoricalSnapshotId}:{keyHash}";

            observations.Add(Observation(
                currentObservationId,
                CurrentSnapshotId,
                current,
                comparison.CurrentMechanicalSignature,
                comparison.CurrentRenderingSignature,
                comparison.CurrentNumericShapeSignature,
                currentUsage.Length,
                rewriteSourceId: null));
            observations.Add(Observation(
                historicalObservationId,
                HistoricalSnapshotId,
                historical,
                comparison.HistoricalMechanicalSignature,
                comparison.HistoricalRenderingSignature,
                comparison.HistoricalNumericShapeSignature,
                historicalUsage.Length,
                BaseImplicitHistoryBuilder.HistoricalManifestSourceId));

            var equivalent = comparison.Classification is
                StatTranslationCompatibilityClassification.MechanicallyEquivalentRendering or
                StatTranslationCompatibilityClassification.EquivalentWithCanonicalizationChange;
            changes.Add(new StatTranslationCompatibilityChange
            {
                Id = $"translation-change:{keyHash}",
                CurrentObservationId = currentObservationId,
                HistoricalObservationId = historicalObservationId,
                Classification = comparison.Classification,
                RuntimeRelevance = allUsage.Length == 0
                    ? StatTranslationRuntimeRelevance.None
                    : specialOnly
                        ? StatTranslationRuntimeRelevance.SpecialOnly
                        : StatTranslationRuntimeRelevance.OrdinaryItemModifier,
                ParserRisk = currentUsage.Length > 0 && !specialOnly,
                CanonicalizationRisk = comparison.LiteralMechanicsChanged ||
                    !string.Equals(
                        comparison.CurrentMechanicalSignature,
                        comparison.HistoricalMechanicalSignature,
                        StringComparison.Ordinal),
                NumericShapeRisk = !string.Equals(
                    comparison.CurrentNumericShapeSignature,
                    comparison.HistoricalNumericShapeSignature,
                    StringComparison.Ordinal),
                ChangesRuntimeBehaviorInT3A = equivalent && currentUsage.Length > 0 && !specialOnly,
                RequiresProviderWorkInT3B = comparison.Classification ==
                    StatTranslationCompatibilityClassification.EquivalentWithCanonicalizationChange &&
                    currentUsage.Length > 0 && !specialOnly,
            });
        }

        return new StatTranslationHistoryCatalog
        {
            SourceSnapshots =
            [
                Source(CurrentSnapshotId, StatTranslationSnapshotRole.CurrentCandidate,
                    RePoeBaseItemImporter.SourceId, currentRepositoryUri, currentCommitSha, currentDataVersion, false),
                Source(HistoricalSnapshotId, StatTranslationSnapshotRole.HistoricalObserved,
                    BaseImplicitHistoryBuilder.HistoricalManifestSourceId, historicalRepositoryUri,
                    historicalCommitSha, historicalDataVersion, true),
            ],
            Observations = observations,
            Changes = changes,
        };
    }

    private static StatTranslationObservation Observation(
        string id,
        string snapshotId,
        StatTranslationDefinition translation,
        string mechanicalSignature,
        string renderingSignature,
        string numericShapeSignature,
        int modifierUsageCount,
        string? rewriteSourceId) => new()
    {
        Id = id,
        SourceSnapshotId = snapshotId,
        StatIds = translation.StatIds.ToArray(),
        Translation = rewriteSourceId is null
            ? translation
            : translation with
            {
                Sources = [new GameDataSourceReference { SourceId = rewriteSourceId }],
            },
        MechanicalSignature = mechanicalSignature,
        RenderingSignature = renderingSignature,
        NumericShapeSignature = numericShapeSignature,
        ModifierUsageCount = modifierUsageCount,
    };

    private static StatTranslationSourceSnapshot Source(
        string id,
        StatTranslationSnapshotRole role,
        string manifestSourceId,
        string repositoryUri,
        string commitSha,
        string dataVersion,
        bool historical)
    {
        var prefix = historical ? "historical-" : string.Empty;
        return new StatTranslationSourceSnapshot
        {
            Id = id,
            Role = role,
            ManifestSourceId = manifestSourceId,
            RepositoryUri = repositoryUri,
            CommitSha = commitSha,
            DataVersion = dataVersion,
            Files =
            [
                new StatTranslationSourceFile { LogicalRole = "modifiers", PackageInputLabel = $"{prefix}mods.json" },
                new StatTranslationSourceFile { LogicalRole = "stats", PackageInputLabel = $"{prefix}stats.json" },
                new StatTranslationSourceFile { LogicalRole = "statTranslations", PackageInputLabel = $"{prefix}stat_translations.json" },
            ],
        };
    }

    private static Dictionary<string, StatTranslationDefinition> UniqueTranslations(
        IEnumerable<StatTranslationDefinition> translations) => translations
        .GroupBy(translation => VectorKey(translation.StatIds), StringComparer.Ordinal)
        .Where(group => group.Count() == 1)
        .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

    private static Dictionary<string, StatDefinition> StatsById(IEnumerable<StatDefinition> stats) => stats
        .Where(stat => !string.IsNullOrWhiteSpace(stat.Id))
        .GroupBy(stat => stat.Id!.Trim(), StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() == 1)
        .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<bool> Localities(
        IReadOnlyList<string> statIds,
        IReadOnlyDictionary<string, StatDefinition> stats) => statIds
        .Select(statId => stats.TryGetValue(statId.Trim(), out var stat) && stat.IsLocal)
        .ToArray();

    private static ModifierDefinition[] FindUsage(
        IReadOnlyList<string> statIds,
        IReadOnlyList<ModifierDefinition> modifiers) => modifiers
        .Where(modifier => ContainsContiguousVector(
            modifier.Stats.OrderBy(stat => stat.Index).Select(stat => stat.StatId?.Trim()).ToArray(),
            statIds))
        .ToArray();

    private static bool ContainsContiguousVector(
        IReadOnlyList<string?> candidate,
        IReadOnlyList<string> vector)
    {
        for (var start = 0; start + vector.Count <= candidate.Count; start++)
        {
            if (Enumerable.Range(0, vector.Count).All(offset => string.Equals(
                    candidate[start + offset], vector[offset].Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSpecialOnly(ModifierDefinition modifier)
    {
        var domain = modifier.Domain?.Trim();
        var sourceGeneration = modifier.SourceGenerationType?.Trim();
        return modifier.GenerationType is not (
                ModifierGenerationType.Prefix or ModifierGenerationType.Suffix or ModifierGenerationType.Implicit) ||
            string.Equals(sourceGeneration, "unique", StringComparison.OrdinalIgnoreCase) ||
            domain is not null && domain is not ("item" or "crafted" or "unveiled");
    }

    private static bool IsSpecialVector(IReadOnlyList<string> statIds) => statIds.All(statId =>
        statId.StartsWith("map_", StringComparison.OrdinalIgnoreCase) ||
        statId.StartsWith("area_", StringComparison.OrdinalIgnoreCase) ||
        statId.Contains("fishing", StringComparison.OrdinalIgnoreCase) ||
        statId.Contains("timeless", StringComparison.OrdinalIgnoreCase));

    private static string VectorKey(IEnumerable<string> statIds) =>
        string.Join('\u001f', statIds.Select(statId => statId.Trim()));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
