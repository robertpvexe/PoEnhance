namespace PoEnhance.Core.Items.GameData;

/// <summary>
/// Structured Core aggregation diagnostics for Historical encoding / source-mechanic conflicts
/// that must not erase already-proven Current Unique mechanics.
/// </summary>
public static class UniqueHistoricalEncodingAggregationCodes
{
    /// <summary>
    /// Historical ExactConflict of deprecated/current encoding or source-mechanic records remained
    /// fail-closed in GameData, but was proven compatible with the already-resolved Current
    /// mechanical vector and therefore did not override Current provenance for an unpinned runtime
    /// copy.
    /// </summary>
    public const string HistoricalEncodingConflictDidNotOverrideCurrentProof =
        "HISTORICAL_ENCODING_CONFLICT_DID_NOT_OVERRIDE_CURRENT_PROOF";

    /// <summary>
    /// Historical broad <c>UNIQUE_MECHANICS_CONFLICT</c> (Ambiguous without a proven conflicting
    /// StatId vector) remained diagnostic and did not override already-resolved Current Unique
    /// Exact / EquivalentSourceSet provenance for an unpinned runtime copy.
    /// </summary>
    public const string HistoricalMechanicsConflictDidNotOverrideCurrentProof =
        "HISTORICAL_MECHANICS_CONFLICT_DID_NOT_OVERRIDE_CURRENT_PROOF";
}
