namespace PoEnhance.GameData;

/// <summary>
/// A source-proven partition of one mechanical modifier across independently displayed lines.
/// The parent block remains the version, option-axis, and provenance boundary.
/// </summary>
public sealed record UniqueModifierComposition
{
    public string? Id { get; init; }

    public IReadOnlyList<UniqueModifierCompositionComponent> Components { get; init; } = [];

    /// <summary>
    /// Mechanics retained by the source modifier that intentionally render no component line,
    /// such as a zero-valued auxiliary stat in a legacy compound modifier.
    /// </summary>
    public IReadOnlyList<string> AuxiliaryStatIds { get; init; } = [];
}
