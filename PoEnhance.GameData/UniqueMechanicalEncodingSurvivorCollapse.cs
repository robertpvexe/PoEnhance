namespace PoEnhance.GameData;

/// <summary>
/// Shared Current-role ExactConflict survivor collapse checks for encoding-filter resolvers.
/// </summary>
public static class UniqueMechanicalEncodingSurvivorCollapse
{
    /// <summary>
    /// True when surviving candidates collapse to exactly one mechanical StatIds vector and one
    /// semantic-fingerprint equivalence family.
    /// </summary>
    public static bool TryValidate(
        IReadOnlyList<IReadOnlyList<string>> survivingStatIdVectors,
        IReadOnlyList<string> survivingSemanticEquivalenceKeys)
    {
        if (survivingStatIdVectors.Count == 0 ||
            survivingSemanticEquivalenceKeys.Count == 0 ||
            survivingStatIdVectors.Count != survivingSemanticEquivalenceKeys.Count)
        {
            return false;
        }

        var mechanicalVectors = survivingStatIdVectors
            .Select(statIds => string.Join('\u001f', statIds))
            .Where(vector => vector.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (mechanicalVectors.Length != 1)
        {
            return false;
        }

        var fingerprintKeys = survivingSemanticEquivalenceKeys
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return fingerprintKeys.Length == 1;
    }
}
