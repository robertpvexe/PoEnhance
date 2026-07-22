using PoEnhance.App.Features.PriceChecking;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class OfferCardWindowSizeCalculatorTests
{
    private readonly OfferCardWindowSizeCalculator calculator = new();

    [Fact]
    public void ShortCardUsesItsNaturalHeightWithoutEmptyMinimumSpace()
    {
        var size = calculator.Calculate(
            clientHeight: 1000,
            headerHeight: 70,
            contentHeight: 35,
            footerHeight: 40,
            verticalChromeHeight: 18);

        Assert.Equal(163, size.Height);
        Assert.Equal(35, size.ContentViewportHeight);
        Assert.False(size.IsContentScrollingRequired);
    }

    [Theory]
    [InlineData(240, 1920, 400)]
    [InlineData(480, 1920, 480)]
    [InlineData(620, 1920, 620)]
    [InlineData(1000, 1920, 700)]
    [InlineData(620, 540, 540)]
    [InlineData(240, 360, 360)]
    public void WidthUsesMeasuredContentWithinUsefulAndClientBounds(
        double measuredContentWidth,
        double clientWidth,
        double expectedWidth)
    {
        Assert.Equal(
            expectedWidth,
            calculator.CalculateWidth(measuredContentWidth, clientWidth));
    }

    [Fact]
    public void LongerMeasuredModifierLineIncreasesAdaptiveWidth()
    {
        var shortItemWidth = calculator.CalculateWidth(
            measuredContentWidth: 330,
            clientWidth: 1600);
        var longerModifierWidth = calculator.CalculateWidth(
            measuredContentWidth: 575,
            clientWidth: 1600);

        Assert.Equal(OfferCardWindowSizeCalculator.MinimumUsefulWidth, shortItemWidth);
        Assert.True(longerModifierWidth > shortItemWidth);
        Assert.Equal(575, longerModifierWidth);
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

    [Fact]
    public void RepresentativeSevenModifierCardDoesNotScrollAtNormalClientHeight()
    {
        var size = calculator.Calculate(
            clientHeight: 900,
            headerHeight: 82,
            contentHeight: 390,
            footerHeight: 50,
            verticalChromeHeight: 14);

        Assert.Equal(536, size.Height);
        Assert.Equal(390, size.ContentViewportHeight);
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
