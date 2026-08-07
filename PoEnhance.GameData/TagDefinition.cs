namespace PoEnhance.GameData;

public sealed record TagDefinition
{
    public string? Id { get; init; }

    public IReadOnlyList<GameDataSourceReference> Sources { get; init; } = [];
}
