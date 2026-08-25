namespace PoEnhance.GameData;

/// <summary>
/// A provider-neutral source selection dimension that is independent of the containing
/// atomic item version. Multiple choices may coexist up to <see cref="SelectionLimit"/>.
/// </summary>
public sealed record UniqueItemOptionAxis
{
    public string? Id { get; init; }

    public int SelectionLimit { get; init; }

    public IReadOnlyList<UniqueItemOptionChoice> Choices { get; init; } = [];

    public IReadOnlyList<string> SourceObservationIds { get; init; } = [];
}
