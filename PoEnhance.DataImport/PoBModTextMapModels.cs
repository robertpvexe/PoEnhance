namespace PoEnhance.DataImport;

/// <summary>
/// Deterministic index of pinned PoB <c>src/Export/Uniques/ModTextMap.lua</c>.
/// Keys are normalized display lines; values are ordered ModifierId lists as declared.
/// </summary>
public sealed class PoBModTextMapIndex
{
    private readonly Dictionary<string, IReadOnlyList<string>> _modifierIdsByNormalizedKey;

    public PoBModTextMapIndex(IReadOnlyDictionary<string, IReadOnlyList<string>> entries)
    {
        _modifierIdsByNormalizedKey = new Dictionary<string, IReadOnlyList<string>>(
            entries,
            StringComparer.Ordinal);
        EntryCount = _modifierIdsByNormalizedKey.Count;
        MultiModifierEntryCount = _modifierIdsByNormalizedKey.Count(entry => entry.Value.Count > 1);
    }

    public int EntryCount { get; }

    public int MultiModifierEntryCount { get; }

    public bool TryGetModifierIds(string displayLine, out IReadOnlyList<string> modifierIds)
    {
        var key = PoBModTextMapParser.NormalizeKey(displayLine);
        if (key.Length == 0 ||
            !_modifierIdsByNormalizedKey.TryGetValue(key, out var matched) ||
            matched.Count == 0)
        {
            modifierIds = [];
            return false;
        }

        modifierIds = matched;
        return true;
    }
}
