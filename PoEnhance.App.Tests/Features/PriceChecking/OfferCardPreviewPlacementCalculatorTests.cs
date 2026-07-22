using PoEnhance.App.Features.PriceChecking;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class OfferCardPreviewPlacementCalculatorTests
{
    private readonly OfferCardPreviewPlacementCalculator calculator = new();

    [Fact]
    public void Calculate_PrefersLeftOfPriceCheckerWhenItFits()
    {
        var client = Bounds(left: 100, top: 50, width: 1200, height: 800);
        var priceChecker = new PriceCheckerPlacement(800, 100, 300, 700);

        var placement = calculator.Calculate(
            client,
            priceChecker,
            new OfferCardPreviewSize(460, 600));

        Assert.Equal(330, placement.Left);
        Assert.Equal(100, placement.Top);
        Assert.Equal(460, placement.Width);
        Assert.Equal(600, placement.Height);
    }

    [Fact]
    public void Calculate_FallsBackToRightWhenLeftDoesNotFit()
    {
        var client = Bounds(left: 100, top: 50, width: 1200, height: 800);
        var priceChecker = new PriceCheckerPlacement(150, 100, 400, 700);

        var placement = calculator.Calculate(
            client,
            priceChecker,
            new OfferCardPreviewSize(460, 600));

        Assert.Equal(560, placement.Left);
        Assert.Equal(100, placement.Top);
    }

    [Fact]
    public void Calculate_ClampsInsideClientWhenNeitherSideFits()
    {
        var client = Bounds(left: 100, top: 50, width: 700, height: 500);
        var priceChecker = new PriceCheckerPlacement(300, 20, 300, 700);

        var placement = calculator.Calculate(
            client,
            priceChecker,
            new OfferCardPreviewSize(460, 620));

        Assert.Equal(100, placement.Left);
        Assert.Equal(50, placement.Top);
        Assert.Equal(460, placement.Width);
        Assert.Equal(500, placement.Height);
        Assert.True(placement.Left >= client.Left);
        Assert.True(placement.Right <= client.Right);
        Assert.True(placement.Top >= client.Top);
        Assert.True(placement.Top + placement.Height <= client.Bottom);
    }

    [Fact]
    public void Calculate_UsesExistingDipCoordinatesAtNonHundredPercentDpi()
    {
        var oneHundredPercent = Bounds(100, 50, 1200, 800, dpiScale: 1d);
        var oneHundredFiftyPercent = Bounds(100, 50, 1200, 800, dpiScale: 1.5d);
        var priceChecker = new PriceCheckerPlacement(800, 100, 300, 700);
        var size = new OfferCardPreviewSize(460, 600);

        var standard = calculator.Calculate(oneHundredPercent, priceChecker, size);
        var scaled = calculator.Calculate(oneHundredFiftyPercent, priceChecker, size);

        Assert.Equal(standard, scaled);
        Assert.Equal(495, scaled.Left * oneHundredFiftyPercent.DpiScaleX);
        Assert.Equal(150, scaled.Top * oneHundredFiftyPercent.DpiScaleY);
        Assert.Equal(690, scaled.Width * oneHundredFiftyPercent.DpiScaleX);
    }

    private static PathOfExileClientBounds Bounds(
        double left,
        double top,
        double width,
        double height,
        double dpiScale = 1d)
    {
        return new PathOfExileClientBounds(
            left,
            top,
            width,
            height,
            @"\.\DISPLAY1",
            dpiScale,
            dpiScale);
    }
}
