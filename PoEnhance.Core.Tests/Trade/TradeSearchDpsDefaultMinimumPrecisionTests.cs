using PoEnhance.Core.Trade;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeSearchDpsDefaultMinimumPrecisionTests
{
    [Theory]
    [InlineData(335.625, 335.62)]
    [InlineData(168.125, 168.12)]
    [InlineData(167.5, 167.5)]
    [InlineData(226.08, 226.08)]
    [InlineData(100, 100)]
    [InlineData(12.999, 12.99)]
    [InlineData(0.019, 0.01)]
    [InlineData(0, 0)]
    public void FloorAutoMinimum_FloorsTowardNegativeInfinityAtTwoDecimals(
        decimal exact,
        decimal expected)
    {
        Assert.Equal(expected, TradeSearchDpsDefaultMinimumPrecision.FloorAutoMinimum(exact));
    }

    [Fact]
    public void FloorAutoMinimum_DoesNotRoundMathematicallyUp()
    {
        Assert.Equal(335.62m, TradeSearchDpsDefaultMinimumPrecision.FloorAutoMinimum(335.625m));
        Assert.NotEqual(335.63m, TradeSearchDpsDefaultMinimumPrecision.FloorAutoMinimum(335.625m));
        Assert.Equal(168.12m, TradeSearchDpsDefaultMinimumPrecision.FloorAutoMinimum(168.125m));
        Assert.NotEqual(168.13m, TradeSearchDpsDefaultMinimumPrecision.FloorAutoMinimum(168.125m));
    }

    [Theory]
    [InlineData(TradeSearchItemPropertyKind.TotalDps, true)]
    [InlineData(TradeSearchItemPropertyKind.PhysicalDps, true)]
    [InlineData(TradeSearchItemPropertyKind.ElementalDps, true)]
    [InlineData(TradeSearchItemPropertyKind.ChaosDps, true)]
    [InlineData(TradeSearchItemPropertyKind.AttacksPerSecond, false)]
    [InlineData(TradeSearchItemPropertyKind.CriticalStrikeChance, false)]
    [InlineData(TradeSearchItemPropertyKind.Armour, false)]
    [InlineData(TradeSearchItemPropertyKind.EvasionRating, false)]
    [InlineData(TradeSearchItemPropertyKind.EnergyShield, false)]
    [InlineData(TradeSearchItemPropertyKind.Ward, false)]
    [InlineData(TradeSearchItemPropertyKind.ChanceToBlock, false)]
    public void IsDpsKind_MatchesOnlyDpsFamilies(TradeSearchItemPropertyKind kind, bool expected)
    {
        Assert.Equal(expected, TradeSearchDpsDefaultMinimumPrecision.IsDpsKind(kind));
    }

    [Fact]
    public void FloorAutoMinimumIfDps_LeavesNonDpsValuesUntouched()
    {
        Assert.Equal(
            1.234m,
            TradeSearchDpsDefaultMinimumPrecision.FloorAutoMinimumIfDps(
                TradeSearchItemPropertyKind.AttacksPerSecond,
                1.234m));
        Assert.Equal(
            5.555m,
            TradeSearchDpsDefaultMinimumPrecision.FloorAutoMinimumIfDps(
                TradeSearchItemPropertyKind.CriticalStrikeChance,
                5.555m));
        Assert.Equal(
            1241.999m,
            TradeSearchDpsDefaultMinimumPrecision.FloorAutoMinimumIfDps(
                TradeSearchItemPropertyKind.Armour,
                1241.999m));
    }

    [Fact]
    public void FloorAutoMinimumIfDps_FloorsChaosDpsWithExtraDecimals()
    {
        Assert.Equal(
            44.44m,
            TradeSearchDpsDefaultMinimumPrecision.FloorAutoMinimumIfDps(
                TradeSearchItemPropertyKind.ChaosDps,
                44.449m));
    }
}
