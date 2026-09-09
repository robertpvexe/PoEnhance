namespace PoEnhance.DataImport;

/// <summary>
/// Typed Unique→ModifierId ownership parsed from pinned Path of Building
/// <c>src/Export/Uniques/*.lua</c> (not rendered <c>Data/Uniques</c> text).
/// </summary>
public sealed class PoBExportUniqueOwnershipIndex
{
    private readonly Dictionary<string, PoBExportUniqueOwnershipEntry> _entriesByName;
    private readonly Dictionary<string, HashSet<string>> _ownersByModifierId;

    public PoBExportUniqueOwnershipIndex(IReadOnlyList<PoBExportUniqueOwnershipEntry> entries)
    {
        Entries = entries;
        _entriesByName = new Dictionary<string, PoBExportUniqueOwnershipEntry>(StringComparer.Ordinal);
        _ownersByModifierId = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            _entriesByName[entry.CanonicalName] = entry;
            foreach (var line in entry.EffectLines)
            {
                if (!line.IsTypedModifier || line.ModifierId is null)
                {
                    continue;
                }

                if (!_ownersByModifierId.TryGetValue(line.ModifierId, out var owners))
                {
                    owners = new HashSet<string>(StringComparer.Ordinal);
                    _ownersByModifierId[line.ModifierId] = owners;
                }

                owners.Add(entry.CanonicalName);
            }
        }
    }

    public IReadOnlyList<PoBExportUniqueOwnershipEntry> Entries { get; }

    public int EntryCount => Entries.Count;

    public bool TryGetEntry(string canonicalName, out PoBExportUniqueOwnershipEntry entry) =>
        _entriesByName.TryGetValue(canonicalName, out entry!);

    public bool IsOwnedByOtherUnique(string modifierId, string canonicalUniqueName)
    {
        if (!_ownersByModifierId.TryGetValue(modifierId, out var owners))
        {
            return false;
        }

        return owners.Any(owner =>
            !string.Equals(owner, canonicalUniqueName, StringComparison.Ordinal));
    }
}

public sealed class PoBExportUniqueOwnershipEntry
{
    public required string CanonicalName { get; init; }

    public required string RelativePath { get; init; }

    /// <summary>1-based Export <c>Variant:</c> labels in declaration order.</summary>
    public required IReadOnlyList<string> VariantLabels { get; init; }

    public required IReadOnlyList<PoBExportUniqueEffectLine> EffectLines { get; init; }
}

public sealed class PoBExportUniqueEffectLine
{
    public required bool IsTypedModifier { get; init; }

    public string? ModifierId { get; init; }

    /// <summary>Residual rendered/crafted Export text after directive stripping; never ownership proof.</summary>
    public string? ResidualText { get; init; }

    /// <summary>
    /// Empty means the line applies to every variant of the entry (or the entry has no variants).
    /// </summary>
    public required IReadOnlySet<int> VariantIndices { get; init; }

    public bool IsCrafted { get; init; }

    public bool AppliesToVariant(int variantIndex) =>
        VariantIndices.Count == 0 || VariantIndices.Contains(variantIndex);
}
