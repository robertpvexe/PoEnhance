namespace PoEnhance.GameData;

public sealed record StatTranslationVariant
{
    public IReadOnlyList<StatTranslationCondition> Conditions { get; init; } = [];

    public IReadOnlyList<string> ValueFormats { get; init; } = [];

    public IReadOnlyList<StatTranslationIndexHandler> IndexHandlers { get; init; } = [];

    public IReadOnlyList<string> FormatLines { get; init; } = [];
}
