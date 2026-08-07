namespace PoEnhance.GameData;

public sealed record ItemClassDefinition
{
    public string? Id { get; init; }

    /// <summary>Optional source display name; the stable identity is <see cref="Id"/>.</summary>
    public string? Name { get; init; }

    public string? CategoryId { get; init; }

    public string? CategoryName { get; init; }

    public IReadOnlyList<string> InfluenceTagIds { get; init; } = [];

    public IReadOnlyList<GameDataSourceReference> Sources { get; init; } = [];
}
