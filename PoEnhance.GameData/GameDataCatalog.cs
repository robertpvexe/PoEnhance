using System.Collections.ObjectModel;

namespace PoEnhance.GameData;

public sealed class GameDataCatalog
{
    private static readonly IReadOnlyList<ItemBaseRecord> EmptyItemBases =
        Array.AsReadOnly(Array.Empty<ItemBaseRecord>());
    private static readonly IReadOnlyList<ModifierDefinition> EmptyModifiers =
        Array.AsReadOnly(Array.Empty<ModifierDefinition>());
    private static readonly IReadOnlyList<StatDefinition> EmptyStats =
        Array.AsReadOnly(Array.Empty<StatDefinition>());
    private static readonly IReadOnlyList<StatTranslationDefinition> EmptyStatTranslations =
        Array.AsReadOnly(Array.Empty<StatTranslationDefinition>());

    private readonly IReadOnlyDictionary<string, IReadOnlyList<ItemBaseRecord>> _itemBasesById;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ItemBaseRecord>> _itemBasesByExactName;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ItemBaseRecord>> _itemBasesByNormalizedName;

    private readonly IReadOnlyDictionary<string, IReadOnlyList<ModifierDefinition>> _modifiersById;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ModifierDefinition>> _modifiersByGroupId;
    private readonly IReadOnlyDictionary<ModifierGenerationType, IReadOnlyList<ModifierDefinition>> _modifiersByGenerationType;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ModifierDefinition>> _modifiersByStatId;

    private readonly IReadOnlyDictionary<string, IReadOnlyList<StatDefinition>> _statsById;

    private readonly IReadOnlyDictionary<string, IReadOnlyList<StatTranslationDefinition>> _translationsById;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<StatTranslationDefinition>> _translationsByStatId;

    private GameDataCatalog(
        IReadOnlyDictionary<string, IReadOnlyList<ItemBaseRecord>> itemBasesById,
        IReadOnlyDictionary<string, IReadOnlyList<ItemBaseRecord>> itemBasesByExactName,
        IReadOnlyDictionary<string, IReadOnlyList<ItemBaseRecord>> itemBasesByNormalizedName,
        IReadOnlyDictionary<string, IReadOnlyList<ModifierDefinition>> modifiersById,
        IReadOnlyDictionary<string, IReadOnlyList<ModifierDefinition>> modifiersByGroupId,
        IReadOnlyDictionary<ModifierGenerationType, IReadOnlyList<ModifierDefinition>> modifiersByGenerationType,
        IReadOnlyDictionary<string, IReadOnlyList<ModifierDefinition>> modifiersByStatId,
        IReadOnlyDictionary<string, IReadOnlyList<StatDefinition>> statsById,
        IReadOnlyDictionary<string, IReadOnlyList<StatTranslationDefinition>> translationsById,
        IReadOnlyDictionary<string, IReadOnlyList<StatTranslationDefinition>> translationsByStatId)
    {
        _itemBasesById = itemBasesById;
        _itemBasesByExactName = itemBasesByExactName;
        _itemBasesByNormalizedName = itemBasesByNormalizedName;
        _modifiersById = modifiersById;
        _modifiersByGroupId = modifiersByGroupId;
        _modifiersByGenerationType = modifiersByGenerationType;
        _modifiersByStatId = modifiersByStatId;
        _statsById = statsById;
        _translationsById = translationsById;
        _translationsByStatId = translationsByStatId;
    }

    public static GameDataCatalog FromPackage(GameDataPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var validation = GameDataPackageValidator.Validate(package);
        if (!validation.IsValid)
        {
            throw new ArgumentException(
                $"GameDataCatalog can only be built from a valid package. First error: {validation.Errors[0].Code}.",
                nameof(package));
        }

        return new GameDataCatalog(
            BuildIndex(
                package.ItemBases,
                itemBase => GameDataLookupNormalizer.NormalizeIdentifier(itemBase.Id),
                StringComparer.OrdinalIgnoreCase),
            BuildIndex(
                package.ItemBases,
                itemBase => GameDataLookupNormalizer.NormalizeName(itemBase.Name),
                StringComparer.Ordinal),
            BuildIndex(
                package.ItemBases,
                itemBase => GameDataLookupNormalizer.NormalizeName(itemBase.Name),
                StringComparer.OrdinalIgnoreCase),
            BuildIndex(
                package.Modifiers,
                modifier => GameDataLookupNormalizer.NormalizeIdentifier(modifier.Id),
                StringComparer.OrdinalIgnoreCase),
            BuildIndex(
                package.Modifiers,
                modifier => GameDataLookupNormalizer.NormalizeIdentifier(modifier.GroupId),
                StringComparer.OrdinalIgnoreCase),
            BuildIndex(
                package.Modifiers,
                modifier => modifier.GenerationType),
            BuildManyIndex(
                package.Modifiers,
                modifier => modifier.Stats
                    .Select(stat => GameDataLookupNormalizer.NormalizeIdentifier(stat.StatId))
                    .Where(statId => statId is not null)!,
                StringComparer.OrdinalIgnoreCase),
            BuildIndex(
                package.Stats,
                stat => GameDataLookupNormalizer.NormalizeIdentifier(stat.Id),
                StringComparer.OrdinalIgnoreCase),
            BuildIndex(
                package.StatTranslations,
                translation => GameDataLookupNormalizer.NormalizeIdentifier(translation.Id),
                StringComparer.OrdinalIgnoreCase),
            BuildManyIndex(
                package.StatTranslations,
                translation => translation.StatIds
                    .Select(GameDataLookupNormalizer.NormalizeIdentifier)
                    .Where(statId => statId is not null)!,
                StringComparer.OrdinalIgnoreCase));
    }

    public IReadOnlyList<ItemBaseRecord> FindItemBasesById(string? id)
    {
        return Find(_itemBasesById, GameDataLookupNormalizer.NormalizeIdentifier(id), EmptyItemBases);
    }

    public IReadOnlyList<ItemBaseRecord> FindItemBasesByExactName(string? name)
    {
        return Find(_itemBasesByExactName, GameDataLookupNormalizer.NormalizeName(name), EmptyItemBases);
    }

    public IReadOnlyList<ItemBaseRecord> FindItemBasesByNormalizedName(string? name)
    {
        return Find(_itemBasesByNormalizedName, GameDataLookupNormalizer.NormalizeName(name), EmptyItemBases);
    }

    public IReadOnlyList<ModifierDefinition> FindModifiersById(string? id)
    {
        return Find(_modifiersById, GameDataLookupNormalizer.NormalizeIdentifier(id), EmptyModifiers);
    }

    public IReadOnlyList<ModifierDefinition> FindModifiersByGroupId(string? groupId)
    {
        return Find(_modifiersByGroupId, GameDataLookupNormalizer.NormalizeIdentifier(groupId), EmptyModifiers);
    }

    public IReadOnlyList<ModifierDefinition> FindModifiersByGenerationType(ModifierGenerationType generationType)
    {
        return _modifiersByGenerationType.TryGetValue(generationType, out var modifiers)
            ? modifiers
            : EmptyModifiers;
    }

    public IReadOnlyList<ModifierDefinition> FindModifiersByStatId(string? statId)
    {
        return Find(_modifiersByStatId, GameDataLookupNormalizer.NormalizeIdentifier(statId), EmptyModifiers);
    }

    public IReadOnlyList<StatDefinition> FindStatsById(string? id)
    {
        return Find(_statsById, GameDataLookupNormalizer.NormalizeIdentifier(id), EmptyStats);
    }

    public IReadOnlyList<StatTranslationDefinition> FindStatTranslationsById(string? id)
    {
        return Find(_translationsById, GameDataLookupNormalizer.NormalizeIdentifier(id), EmptyStatTranslations);
    }

    public IReadOnlyList<StatTranslationDefinition> FindStatTranslationsByStatId(string? statId)
    {
        return Find(_translationsByStatId, GameDataLookupNormalizer.NormalizeIdentifier(statId), EmptyStatTranslations);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<TRecord>> BuildIndex<TRecord>(
        IEnumerable<TRecord> records,
        Func<TRecord, string?> keySelector,
        StringComparer comparer)
    {
        var mutable = new Dictionary<string, List<TRecord>>(comparer);
        foreach (var record in records)
        {
            var key = keySelector(record);
            if (key is not null)
            {
                Add(mutable, key, record);
            }
        }

        return Freeze(mutable, comparer);
    }

    private static IReadOnlyDictionary<TKey, IReadOnlyList<TRecord>> BuildIndex<TRecord, TKey>(
        IEnumerable<TRecord> records,
        Func<TRecord, TKey> keySelector)
        where TKey : notnull
    {
        var mutable = new Dictionary<TKey, List<TRecord>>();
        foreach (var record in records)
        {
            Add(mutable, keySelector(record), record);
        }

        return Freeze(mutable);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<TRecord>> BuildManyIndex<TRecord>(
        IEnumerable<TRecord> records,
        Func<TRecord, IEnumerable<string?>> keySelector,
        StringComparer comparer)
    {
        var mutable = new Dictionary<string, List<TRecord>>(comparer);
        foreach (var record in records)
        {
            foreach (var key in keySelector(record))
            {
                if (key is not null)
                {
                    Add(mutable, key, record);
                }
            }
        }

        return Freeze(mutable, comparer);
    }

    private static IReadOnlyList<TRecord> Find<TRecord>(
        IReadOnlyDictionary<string, IReadOnlyList<TRecord>> index,
        string? key,
        IReadOnlyList<TRecord> empty)
    {
        return key is not null && index.TryGetValue(key, out var records)
            ? records
            : empty;
    }

    private static void Add<TKey, TRecord>(
        Dictionary<TKey, List<TRecord>> index,
        TKey key,
        TRecord record)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var records))
        {
            records = [];
            index.Add(key, records);
        }

        records.Add(record);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<TRecord>> Freeze<TRecord>(
        Dictionary<string, List<TRecord>> index,
        StringComparer comparer)
    {
        var frozen = new Dictionary<string, IReadOnlyList<TRecord>>(comparer);
        foreach (var entry in index)
        {
            frozen.Add(entry.Key, new ReadOnlyCollection<TRecord>(entry.Value.ToArray()));
        }

        return new ReadOnlyDictionary<string, IReadOnlyList<TRecord>>(frozen);
    }

    private static IReadOnlyDictionary<TKey, IReadOnlyList<TRecord>> Freeze<TKey, TRecord>(
        Dictionary<TKey, List<TRecord>> index)
        where TKey : notnull
    {
        var frozen = new Dictionary<TKey, IReadOnlyList<TRecord>>();
        foreach (var entry in index)
        {
            frozen.Add(entry.Key, new ReadOnlyCollection<TRecord>(entry.Value.ToArray()));
        }

        return new ReadOnlyDictionary<TKey, IReadOnlyList<TRecord>>(frozen);
    }
}
