namespace PoEnhance.GameData;

public sealed record ModifierDefinition
{
    public string? Id { get; init; }

    public string? GroupId { get; init; }

    public string? Name { get; init; }

    public ModifierGenerationType GenerationType { get; init; }

    /// <summary>
    /// The provider-neutral source generation-family identity retained from the imported
    /// game data (for example prefix, corrupted, or searing_exarch_implicit).
    /// </summary>
    public string? SourceGenerationType { get; init; }

    public int? Tier { get; init; }

    public int? RequiredLevel { get; init; }

    public string? Domain { get; init; }

    public bool IsEssenceOnly { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<ModifierStat> Stats { get; init; } = [];

    public IReadOnlyList<ModifierSpawnWeight> SpawnWeights { get; init; } = [];

    public IReadOnlyList<GameDataSourceReference> Sources { get; init; } = [];
}
