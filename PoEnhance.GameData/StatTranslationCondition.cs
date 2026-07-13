namespace PoEnhance.GameData;

public sealed record StatTranslationCondition
{
    public int Index { get; init; }

    public decimal? MinValue { get; init; }

    public decimal? MaxValue { get; init; }

    public bool IsNegated { get; init; }
}
