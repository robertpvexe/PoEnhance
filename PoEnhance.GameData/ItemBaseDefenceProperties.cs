namespace PoEnhance.GameData;

public sealed record ItemBaseDefenceProperties
{
    public int? EnergyShieldMinimum { get; init; }
    public int? EnergyShieldMaximum { get; init; }
    public int? ArmourMinimum { get; init; }
    public int? ArmourMaximum { get; init; }
    public int? EvasionRatingMinimum { get; init; }
    public int? EvasionRatingMaximum { get; init; }
    public int? WardMinimum { get; init; }
    public int? WardMaximum { get; init; }
    public int? ChanceToBlockPercent { get; init; }
    public IReadOnlyList<GameDataSourceReference> Sources { get; init; } = [];
}
