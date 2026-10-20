namespace PoEnhance.Core.Trade;

/// <summary>
/// Floor policy for auto-generated DPS Trade minimums. Does not alter
/// <see cref="TradeSearchItemProperty.ObservedValue"/> mechanical precision.
/// </summary>
public static class TradeSearchDpsDefaultMinimumPrecision
{
    public const int MaxDecimalPlaces = 2;

    public static bool IsDpsKind(TradeSearchItemPropertyKind kind) =>
        kind is TradeSearchItemPropertyKind.TotalDps or
            TradeSearchItemPropertyKind.PhysicalDps or
            TradeSearchItemPropertyKind.ElementalDps or
            TradeSearchItemPropertyKind.ChaosDps;

    /// <summary>
    /// Floors a positive DPS auto-Min toward negative infinity to at most two decimal places
    /// so the Trade filter is never stricter than the exact derived value.
    /// </summary>
    public static decimal FloorAutoMinimum(decimal value)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "DPS auto-Min must be non-negative.");
        }

        var scale = 100m;
        return decimal.Floor(value * scale) / scale;
    }

    public static decimal? FloorAutoMinimumIfDps(
        TradeSearchItemPropertyKind kind,
        decimal? value)
    {
        if (!value.HasValue || !IsDpsKind(kind))
        {
            return value;
        }

        return FloorAutoMinimum(value.Value);
    }
}
