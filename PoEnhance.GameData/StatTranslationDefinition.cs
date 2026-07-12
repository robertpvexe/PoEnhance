namespace PoEnhance.GameData;

public sealed record StatTranslationDefinition
{
    public string? Id { get; init; }

    public IReadOnlyList<string> StatIds { get; init; } = [];

    public string? Language { get; init; }

    public IReadOnlyList<StatTranslationVariant> Variants { get; init; } = [];

    public IReadOnlyList<GameDataSourceReference> Sources { get; init; } = [];
}
