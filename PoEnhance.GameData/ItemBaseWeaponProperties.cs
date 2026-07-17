namespace PoEnhance.GameData;

public sealed record ItemBaseWeaponProperties
{
    public int? PhysicalDamageMinimum { get; init; }

    public int? PhysicalDamageMaximum { get; init; }

    public int? AttackTimeMilliseconds { get; init; }

    public decimal? CriticalStrikeChancePercent { get; init; }

    public IReadOnlyList<GameDataSourceReference> Sources { get; init; } = [];
}
