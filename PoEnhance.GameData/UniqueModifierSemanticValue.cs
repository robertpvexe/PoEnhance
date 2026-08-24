namespace PoEnhance.GameData;

/// <summary>One deterministic value-format and transform component of a semantic fingerprint.</summary>
public sealed record UniqueModifierSemanticValue
{
    public int Index { get; init; }

    public string? StatId { get; init; }

    public string? Format { get; init; }

    public string? Unit { get; init; }

    public IReadOnlyList<string> Transformations { get; init; } = [];

    public bool IsAuxiliary { get; init; }
}
