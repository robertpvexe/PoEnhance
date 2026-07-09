namespace PoEnhance.GameData;

public sealed record ItemBaseRecord
{
    public string? Id { get; init; }

    public string? Name { get; init; }

    public string? ItemClass { get; init; }

    public int? RequiredLevel { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<GameDataSourceReference> Sources { get; init; } = [];
}
