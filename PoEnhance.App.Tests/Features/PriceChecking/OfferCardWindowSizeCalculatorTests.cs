using PoEnhance.App.Features.PriceChecking;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class OfferCardWindowSizeCalculatorTests
{
    private readonly OfferCardWindowSizeCalculator calculator = new();

    [Fact]
    public void ShortCardRemainsAtPracticalContentSizedMinimum()
    {
        var size = calculator.Calculate(
            clientHeight: 1000,
            headerHeight: 70,
            contentHeight: 35,
            footerHeight: 40,
            verticalChromeHeight: 18);

        Assert.Equal(180, size.Height);
        Assert.Equal(52, size.ContentViewportHeight);
        Assert.False(size.IsContentScrollingRequired);
    }

    [Fact]
    public void NormalCardUsesItsFullUsefulContentHeight()
    {
        var size = calculator.Calculate(
            clientHeight: 1000,
            headerHeight: 90,
            contentHeight: 430,
            footerHeight: 55,
            verticalChromeHeight: 18);

        Assert.Equal(593, size.Height);
        Assert.Equal(430, size.ContentViewportHeight);
        Assert.False(size.IsContentScrollingRequired);
    }

    [Theory]
    [InlineData(720, 619.2)]
    [InlineData(900, 774)]
    [InlineData(1080, 928.8)]
    public void LongCardCapsAtEightySixPercentAndScrollsOnlyContent(
        double clientHeight,
        double expectedHeight)
    {
        var size = calculator.Calculate(
            clientHeight,
            headerHeight: 90,
            contentHeight: 1600,
            footerHeight: 55,
            verticalChromeHeight: 18);

        Assert.Equal(expectedHeight, size.Height, precision: 6);
        Assert.Equal(expectedHeight, size.MaximumHeight, precision: 6);
        Assert.Equal(
            expectedHeight - 163,
            size.ContentViewportHeight,
            precision: 6);
        Assert.True(size.IsContentScrollingRequired);
    }
}
