namespace PoEnhance.GameData;

public sealed record StatDefinition
{
    public string? Id { get; init; }

    public bool IsLocal { get; init; }

    public string? MainHandAliasId { get; init; }

    public string? OffHandAliasId { get; init; }

    public IReadOnlyList<GameDataSourceReference> Sources { get; init; } = [];
}
