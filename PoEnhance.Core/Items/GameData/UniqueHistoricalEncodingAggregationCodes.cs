namespace PoEnhance.Core.Items.GameData;

/// <summary>
/// Structured Core aggregation diagnostics for Historical encoding conflicts that must not erase
/// already-proven Current Unique mechanics.
/// </summary>
public static class UniqueHistoricalEncodingAggregationCodes
{
    /// <summary>
    /// Historical ExactConflict of deprecated/current encoding remained fail-closed in GameData, but
    /// was proven compatible with the already-resolved Current mechanical vector and therefore did
    /// not override Current provenance for an unpinned runtime copy.
    /// </summary>
    public const string HistoricalEncodingConflictDidNotOverrideCurrentProof =
        "HISTORICAL_ENCODING_CONFLICT_DID_NOT_OVERRIDE_CURRENT_PROOF";
}
