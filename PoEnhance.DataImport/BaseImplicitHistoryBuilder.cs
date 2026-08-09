using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

internal static class BaseImplicitHistoryBuilder
{
    public const string CurrentSnapshotId = "repoe-current-candidate";
    public const string HistoricalSnapshotId = "repoe-historical-base-implicit";
    public const string HistoricalManifestSourceId = "repoe-historical-base-implicit";

    public static BaseImplicitHistoryCatalog Build(
        string currentRepositoryUri,
        string currentCommitSha,
        string currentDataVersion,
        IReadOnlyList<ItemBaseRecord> currentBases,
        IReadOnlyList<ModifierDefinition> currentModifiers,
        IReadOnlyList<StatDefinition> currentStats,
        IReadOnlyList<StatTranslationDefinition> currentTranslations,
        string historicalRepositoryUri,
        string historicalCommitSha,
        string historicalDataVersion,
        IReadOnlyList<ItemBaseRecord> historicalBases,
        IReadOnlyList<ModifierDefinition> historicalModifiers,
        IReadOnlyList<StatDefinition> historicalStats,
        IReadOnlyList<StatTranslationDefinition> historicalTranslations)
    {
        var effects = new List<BaseImplicitMechanicalEffect>();
        var observations = new List<BaseImplicitObservation>();

        AddSnapshot(
            CurrentSnapshotId,
            currentBases,
            currentModifiers,
            currentStats,
            currentTranslations,
            effects,
            observations,
            rewriteSourceId: null);
        AddSnapshot(
            HistoricalSnapshotId,
            historicalBases,
            historicalModifiers,
            historicalStats,
            historicalTranslations,
            effects,
            observations,
            HistoricalManifestSourceId);

        return new BaseImplicitHistoryCatalog
        {
            SourceSnapshots =
            [
                Source(
                    CurrentSnapshotId,
                    BaseImplicitSnapshotRole.CurrentCandidate,
                    RePoeBaseItemImporter.SourceId,
                    currentRepositoryUri,
                    currentCommitSha,
                    currentDataVersion,
                    historical: false),
                Source(
                    HistoricalSnapshotId,
                    BaseImplicitSnapshotRole.HistoricalObserved,
                    HistoricalManifestSourceId,
                    historicalRepositoryUri,
                    historicalCommitSha,
                    historicalDataVersion,
                    historical: true),
            ],
            MechanicalEffects = effects,
            Observations = observations,
        };
    }

    private static BaseImplicitSourceSnapshot Source(
        string id,
        BaseImplicitSnapshotRole role,
        string manifestSourceId,
        string repositoryUri,
        string commitSha,
        string dataVersion,
        bool historical)
    {
        var prefix = historical ? "historical-" : string.Empty;
        return new BaseImplicitSourceSnapshot
        {
            Id = id,
            Role = role,
            ManifestSourceId = manifestSourceId,
            RepositoryUri = repositoryUri,
            CommitSha = commitSha,
            DataVersion = dataVersion,
            Files =
            [
                new() { LogicalRole = "baseItems", PackageInputLabel = $"{prefix}base_items.json" },
                new() { LogicalRole = "modifiers", PackageInputLabel = $"{prefix}mods.json" },
                new() { LogicalRole = "stats", PackageInputLabel = $"{prefix}stats.json" },
                new() { LogicalRole = "statTranslations", PackageInputLabel = $"{prefix}stat_translations.json" },
            ],
        };
    }

    private static void AddSnapshot(
        string snapshotId,
        IReadOnlyList<ItemBaseRecord> bases,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatDefinition> stats,
        IReadOnlyList<StatTranslationDefinition> translations,
        List<BaseImplicitMechanicalEffect> effects,
        List<BaseImplicitObservation> observations,
        string? rewriteSourceId)
    {
        var modifiersById = UniqueIndex(modifiers, record => record.Id);
        var statsById = UniqueIndex(stats, record => record.Id);
        var translationsByVector = translations
            .Where(record => record.StatIds.Count > 0)
            .GroupBy(record => VectorKey(record.StatIds), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var effectsByModifierId = new Dictionary<string, BaseImplicitMechanicalEffect>(StringComparer.OrdinalIgnoreCase);

        foreach (var itemBase in bases.OrderBy(record => record.Id, StringComparer.Ordinal))
        {
            var effectIds = new List<string?>();
            var diagnostics = new List<string>();
            foreach (var modifierId in itemBase.ImplicitModifierIds)
            {
                if (!effectsByModifierId.TryGetValue(modifierId, out var effect))
                {
                    effect = BuildEffect(
                        snapshotId,
                        modifierId,
                        modifiersById,
                        statsById,
                        translationsByVector,
                        rewriteSourceId);
                    effectsByModifierId.Add(modifierId, effect);
                    effects.Add(effect);
                }

                effectIds.Add(effect.IsResolved ? effect.Id : null);
                if (!effect.IsResolved)
                {
                    diagnostics.Add($"{modifierId}: {effect.DiagnosticCode} - {effect.Diagnostic}");
                }
            }

            var setSignature = Hash(string.Join(
                "\n",
                effectIds.Select((effectId, index) => effectId is null
                    ? $"unresolved:{itemBase.ImplicitModifierIds[index]}"
                    : effectsByModifierId[itemBase.ImplicitModifierIds[index]].MechanicalSignature)));
            observations.Add(new BaseImplicitObservation
            {
                CanonicalBaseId = itemBase.Id,
                SourceSnapshotId = snapshotId,
                ImplicitModifierIds = itemBase.ImplicitModifierIds.ToArray(),
                MechanicalEffectIds = effectIds,
                ImplicitSetMechanicalSignature = setSignature,
                Diagnostics = diagnostics,
            });
        }
    }

    private static BaseImplicitMechanicalEffect BuildEffect(
        string snapshotId,
        string modifierId,
        IReadOnlyDictionary<string, ModifierDefinition> modifiers,
        IReadOnlyDictionary<string, StatDefinition> stats,
        IReadOnlyDictionary<string, StatTranslationDefinition[]> translationsByVector,
        string? rewriteSourceId)
    {
        var effectId = $"{snapshotId}:{modifierId}";
        if (!modifiers.TryGetValue(modifierId, out var modifier))
        {
            return Unresolved(effectId, snapshotId, modifierId, "modifier-missing", "The source modifier id was not present in mods.json.");
        }

        var orderedModifierStats = modifier.Stats.OrderBy(stat => stat.Index).ToArray();
        if (orderedModifierStats.Length == 0 || orderedModifierStats.Any(stat => string.IsNullOrWhiteSpace(stat.StatId)))
        {
            return Unresolved(effectId, snapshotId, modifierId, "modifier-stats-missing", "The source modifier has no complete ordered stat vector.");
        }

        var retainedStats = new List<StatDefinition>();
        foreach (var modifierStat in orderedModifierStats)
        {
            if (!stats.TryGetValue(modifierStat.StatId!, out var stat))
            {
                return Unresolved(effectId, snapshotId, modifierId, "stat-missing", $"Stat '{modifierStat.StatId}' was not present in stats.json.");
            }

            retainedStats.Add(Rewrite(stat, rewriteSourceId) with
            {
                MainHandAliasId = null,
                OffHandAliasId = null,
            });
        }

        var retainedTranslations = new List<StatTranslationDefinition>();
        var position = 0;
        while (position < orderedModifierStats.Length)
        {
            StatTranslationDefinition[]? matches = null;
            var matchedLength = 0;
            for (var length = orderedModifierStats.Length - position; length >= 1; length--)
            {
                var key = VectorKey(orderedModifierStats.Skip(position).Take(length).Select(stat => stat.StatId!));
                if (translationsByVector.TryGetValue(key, out matches))
                {
                    matchedLength = length;
                    break;
                }
            }

            if (matches is null)
            {
                return Unresolved(effectId, snapshotId, modifierId, "translation-missing", "No exact ordered stat-translation group covers the modifier stat vector.");
            }

            if (matches.Length != 1)
            {
                return Unresolved(effectId, snapshotId, modifierId, "translation-ambiguous", "Multiple translations cover the same ordered stat vector.");
            }

            retainedTranslations.Add(Rewrite(matches[0], rewriteSourceId));
            position += matchedLength;
        }

        var retainedModifier = Rewrite(modifier with { Stats = orderedModifierStats }, rewriteSourceId);
        var canonical = new
        {
            retainedModifier.GenerationType,
            retainedModifier.SourceGenerationType,
            retainedModifier.Domain,
            Stats = orderedModifierStats.Select((stat, index) => new
            {
                stat.Index,
                StatId = stat.StatId!.Trim(),
                stat.MinValue,
                stat.MaxValue,
                retainedStats[index].IsLocal,
            }),
            Translations = retainedTranslations.Select(translation => new
            {
                StatIds = translation.StatIds.Select(value => value.Trim()),
                translation.Language,
                Variants = translation.Variants.Select(variant => new
                {
                    Conditions = variant.Conditions.OrderBy(condition => condition.Index),
                    variant.ValueFormats,
                    Handlers = variant.IndexHandlers.OrderBy(handler => handler.Index),
                    variant.FormatLines,
                }),
            }),
        };

        return new BaseImplicitMechanicalEffect
        {
            Id = effectId,
            SourceSnapshotId = snapshotId,
            SourceModifierId = modifierId,
            IsResolved = true,
            MechanicalSignature = Hash(JsonSerializer.Serialize(canonical)),
            Modifier = retainedModifier,
            Stats = retainedStats,
            StatTranslations = retainedTranslations,
        };
    }

    private static BaseImplicitMechanicalEffect Unresolved(
        string effectId,
        string snapshotId,
        string modifierId,
        string diagnosticCode,
        string diagnostic)
    {
        return new BaseImplicitMechanicalEffect
        {
            Id = effectId,
            SourceSnapshotId = snapshotId,
            SourceModifierId = modifierId,
            IsResolved = false,
            DiagnosticCode = diagnosticCode,
            Diagnostic = diagnostic,
        };
    }

    private static TRecord Rewrite<TRecord>(TRecord record, string? sourceId) where TRecord : notnull
    {
        if (sourceId is null)
        {
            return record;
        }

        var source = new GameDataSourceReference { SourceId = sourceId };
        return record switch
        {
            ModifierDefinition modifier => (TRecord)(object)(modifier with { Sources = [source] }),
            StatDefinition stat => (TRecord)(object)(stat with
            {
                MainHandAliasId = null,
                OffHandAliasId = null,
                Sources = [source],
            }),
            StatTranslationDefinition translation => (TRecord)(object)(translation with { Sources = [source] }),
            _ => record,
        };
    }

    private static IReadOnlyDictionary<string, TRecord> UniqueIndex<TRecord>(
        IEnumerable<TRecord> records,
        Func<TRecord, string?> idSelector)
    {
        return records
            .Where(record => !string.IsNullOrWhiteSpace(idSelector(record)))
            .GroupBy(record => idSelector(record)!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);
    }

    private static string VectorKey(IEnumerable<string> statIds) =>
        string.Join('\u001F', statIds.Select(statId => statId.Trim()));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
