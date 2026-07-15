using PoEnhance.App.Features.PriceChecking;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerOfferCapacityCalculatorTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(35.99, 0)]
    [InlineData(36, 1)]
    [InlineData(828, 23)]
    [InlineData(593, 16)]
    public void Calculate_CountsOnlyCompleteCompactRows(double availableHeight, int expected)
    {
        Assert.Equal(expected, PriceCheckerOfferCapacityCalculator.Calculate(availableHeight));
    }
}
