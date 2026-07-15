using PoEnhance.App.Features.PriceChecking;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerRelativeTimeFormatterTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(-30, "1 min ago")]
    [InlineData(-59, "1 min ago")]
    [InlineData(-60, "1 min ago")]
    [InlineData(-59 * 60, "59 min ago")]
    [InlineData(-60 * 60, "1h ago")]
    [InlineData(-23 * 60 * 60, "23h ago")]
    [InlineData(-24 * 60 * 60, "1d ago")]
    [InlineData(-29 * 24 * 60 * 60, "29d ago")]
    [InlineData(-30 * 24 * 60 * 60, "1mo ago")]
    [InlineData(-61 * 24 * 60 * 60, "2mo ago")]
    public void Format_UsesWholeCompletedRelativeUnits(int elapsedSeconds, string expected)
    {
        Assert.Equal(
            expected,
            PriceCheckerRelativeTimeFormatter.Format(Now.AddSeconds(elapsedSeconds), Now));
    }

    [Fact]
    public void Format_UsesUtcSafeCalculationAndClampsFutureTimestamps()
    {
        var listedAtWithOffset = new DateTimeOffset(2026, 7, 15, 13, 30, 0, TimeSpan.FromHours(2));

        Assert.Equal("30 min ago", PriceCheckerRelativeTimeFormatter.Format(listedAtWithOffset, Now));
        Assert.Equal("1 min ago", PriceCheckerRelativeTimeFormatter.Format(Now.AddMinutes(5), Now));
    }

    [Fact]
    public void Format_UsesUnavailableMarkerForMissingTimestamp()
    {
        Assert.Equal("—", PriceCheckerRelativeTimeFormatter.Format(null, Now));
    }
}
