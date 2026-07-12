namespace PoEnhance.GameData;

public sealed record StatTranslationIndexHandler
{
    public int Index { get; init; }

    public IReadOnlyList<string> Handlers { get; init; } = [];
}
