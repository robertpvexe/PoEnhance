using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed partial class PoBUniqueCatalogImporter
{
    public const string GeneratedOptionDomainExactReason = "generated-option-domain-exact";

    /// <summary>
    /// Domains owned by PoB Generated Unique option pools (veiledMods / ModJewelCharm) and by
    /// Fixed Unique option-axis choices that ExactText-match those catalog rows (e.g. Betrayal
    /// flask unveil lines). Never owned by bare Fixed Unique SourceGenerationType=unique rows.
    /// Affliction_jewel Cluster notables are NOT included here: adding them would prefer
    /// Affliction rows over Unique-generation Exact for Fixed Cluster lines (Voices / Megalomaniac
    /// "Adds N Passive Skills") whenever the item also carries GeneratedCandidate evidence.
    /// Disabled AfflictionNotable recovery for Megalomaniac is handled only in FilterCandidates.
    /// </summary>
    internal static bool IsGeneratedOptionOwnedDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        return domain.Equals("unveiled", StringComparison.OrdinalIgnoreCase) ||
            domain.Equals("affliction_charm", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Strict Exact enrollment: Unique-generation always; unveiled/affliction_charm for Generated
    /// option ownership and Fixed option-axis ExactText. Never globally enables
    /// crafted/prefix/suffix for Fixed Unique Exact.
    /// </summary>
    internal static bool IsEligibleForStrictExactEnrollment(ModifierDefinition modifier)
    {
        if (string.Equals(
                modifier.SourceGenerationType?.Trim(),
                "unique",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsGeneratedOptionOwnedDomain(modifier.Domain);
    }
}
