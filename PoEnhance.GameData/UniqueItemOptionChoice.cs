namespace PoEnhance.GameData;

public sealed record UniqueItemOptionChoice
{
    public string? Id { get; init; }

    public IReadOnlyList<string> SourceObservationIds { get; init; } = [];
}
