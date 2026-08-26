namespace PoEnhance.GameData;

/// <summary>
/// One ordered display-line member of a source-proven compound modifier.
/// </summary>
public sealed record UniqueModifierCompositionComponent
{
    public string? Id { get; init; }

    public int Order { get; init; }

    public IReadOnlyList<string> Lines { get; init; } = [];

    public IReadOnlyList<string> CanonicalSignatures { get; init; } = [];

    public IReadOnlyList<string> StatIds { get; init; } = [];

    public IReadOnlyList<string> SourceObservationIds { get; init; } = [];
}
