using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed partial class PoBUniqueCatalogImporter
{
    public const string GeneratedOptionDomainExactReason = "generated-option-domain-exact";

    /// <summary>
    /// Domains owned by PoB Generated Unique option pools (veiledMods / ModJewelCharm) and by
    /// Fixed Unique option-axis choices that ExactText-match those catalog rows (e.g. Betrayal
    /// flask unveil lines). Never owned by bare Fixed Unique SourceGenerationType=unique rows.
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
