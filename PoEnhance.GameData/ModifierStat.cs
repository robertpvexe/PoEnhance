namespace PoEnhance.GameData;

public sealed record ModifierStat
{
    public int Index { get; init; }

    public string? StatId { get; init; }

    public decimal? MinValue { get; init; }

    public decimal? MaxValue { get; init; }
}
