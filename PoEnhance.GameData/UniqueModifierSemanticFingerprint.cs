namespace PoEnhance.GameData;

/// <summary>
/// Provider-neutral semantic evidence for a Unique source block or mechanical candidate.
/// Unknown or empty dimensions are deliberately non-comparable.
/// </summary>
public sealed record UniqueModifierSemanticFingerprint
{
    public UniqueModifierSemanticLocality Locality { get; init; }

    public IReadOnlyList<string> OrderedStatIds { get; init; } = [];

    public UniqueModifierSemanticValueShape ValueShape { get; init; }

    public IReadOnlyList<UniqueModifierSemanticValue> Values { get; init; } = [];

    public IReadOnlyList<string> AuxiliaryStatIds { get; init; } = [];

    public IReadOnlyList<string> EvidenceMethods { get; init; } = [];
}
