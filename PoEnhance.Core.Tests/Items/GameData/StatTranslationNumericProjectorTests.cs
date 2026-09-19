using PoEnhance.Core.Items.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class StatTranslationNumericProjectorTests
{
    [Theory]
    [InlineData(null, 10, 10)]
    [InlineData("", 10, 10)]
    [InlineData("divide_by_one_hundred", 50, 0.5)]
    [InlineData("divide_by_one_hundred_2dp", 33, 0.33)]
    [InlineData("locations_to_metres", 2, 0.2)]
    [InlineData("locations_to_metres", 3, 0.3)]
    [InlineData("locations_to_metres", 4, 0.4)]
    [InlineData("divide_by_ten_1dp", 2, 0.2)]
    [InlineData("divide_by_ten_0dp", 25, 2)]
    [InlineData("negate", 7, -7)]
    [InlineData("double", 4, 8)]
    [InlineData("negate_and_double", 4, -8)]
    [InlineData("milliseconds_to_seconds", 1500, 1.5)]
    [InlineData("milliseconds_to_seconds_2dp", 1234, 1.23)]
    [InlineData("old_leech_percent", 10, 2)]
    [InlineData("old_leech_permyriad", 500, 1)]
    public void TryProjectValue_SupportedHandler_ProjectsAuthoritatively(
        string? handler,
        double source,
        double expectedDisplay)
    {
        Assert.True(StatTranslationNumericProjector.TryProjectValue(
            handler,
            (decimal)source,
            out var projected));
        Assert.Equal((decimal)expectedDisplay, projected);
    }

    [Fact]
    public void TryProjectBounds_LocationsToMetres_ProjectsWeaponRangeShape()
    {
        Assert.True(StatTranslationNumericProjector.TryProjectBounds(
            ["locations_to_metres"],
            2m,
            4m,
            out var minimum,
            out var maximum));
        Assert.Equal(0.2m, minimum);
        Assert.Equal(0.4m, maximum);
    }

    [Fact]
    public void TryProjectBounds_Negate_ReordersMinMax()
    {
        Assert.True(StatTranslationNumericProjector.TryProjectBounds(
            ["negate"],
            1m,
            5m,
            out var minimum,
            out var maximum));
        Assert.Equal(-5m, minimum);
        Assert.Equal(-1m, maximum);
    }

    [Fact]
    public void TryProjectValue_ComposedHandlers_AppliesInOrder()
    {
        Assert.True(StatTranslationNumericProjector.TryProjectValue(
            ["negate", "divide_by_one_hundred"],
            50m,
            out var projected));
        Assert.Equal(-0.5m, projected);
    }

    [Theory]
    [InlineData("passive_hash")]
    [InlineData("canonical_stat")]
    [InlineData("unknown_numeric_handler")]
    [InlineData("affliction_reward_type")]
    public void TryProjectValue_UnsupportedHandler_FailsClosed(string handler)
    {
        Assert.False(StatTranslationNumericProjector.TryProjectValue(handler, 10m, out _));
        Assert.False(StatTranslationNumericProjector.IsSupported(handler));
    }

    [Fact]
    public void TryProjectValue_FixedAndRangedDiscreteValues_UseExactDecimalEquality()
    {
        Assert.True(StatTranslationNumericProjector.TryProjectValue(
            "locations_to_metres",
            3m,
            out var fixedValue));
        Assert.Equal(0.3m, fixedValue);

        Assert.True(StatTranslationNumericProjector.TryProjectBounds(
            ["locations_to_metres"],
            2m,
            4m,
            out var minimum,
            out var maximum));
        Assert.Equal(0.2m, minimum);
        Assert.Equal(0.4m, maximum);
        Assert.True(fixedValue >= minimum && fixedValue <= maximum);
    }
}
