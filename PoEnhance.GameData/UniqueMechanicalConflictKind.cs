namespace PoEnhance.GameData;

/// <summary>
/// Structural ExactConflict subclass derived only from competing candidate provenance.
/// Classification is diagnostic/provenance metadata and never selects a winner.
/// </summary>
public enum UniqueMechanicalConflictKind
{
    /// <summary>No proven structural subclass applies.</summary>
    Unclassified,

    /// <summary>
    /// Competing candidates mix current permyriad-style encoding with deprecated percent-style
    /// encoding of the same semantic family.
    /// </summary>
    CurrentVsDeprecatedEncodingPermyriadPercent,

    /// <summary>
    /// Competing candidates represent the same displayed semantic family through modern
    /// positive/increased-style versus inverse/legacy handler or encoding transforms.
    /// </summary>
    InverseLegacyHandlerEncoding,

    /// <summary>
    /// Multiple distinct mechanical stat vectors share exact-text-compatible display evidence and
    /// later correspond to duplicated Trade possibilities. Reserved for provider-owned Trade
    /// enrichment; the importer does not assign this without Trade evidence.
    /// </summary>
    SameDisplayTextDifferentStatIdsWithTradeDuplicates,

    /// <summary>
    /// Competing candidates differ in source mechanics with explicit current/deprecated provenance,
    /// beyond a pure numeric unit-encoding split.
    /// </summary>
    CurrentVsDeprecatedSourceMechanics,

    /// <summary>
    /// Competing vectors structurally represent level-based versus chance-based on-hit semantics.
    /// </summary>
    LevelVsChanceOnHit,

    /// <summary>
    /// Multiple distinct mechanical stat vectors share the same source/display signature when no
    /// safer specialized subtype applies.
    /// </summary>
    SameDisplayTextDifferentStatIds,
}
