using PoEnhance.App.Features.PriceChecking;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PinnedOfferCardPlacementCalculatorTests
{
    private readonly PinnedOfferCardPlacementCalculator calculator = new();

    [Fact]
    public void PlaceNewOffsetsStrongOverlapAndClampsAtClientEdge()
    {
        var bounds = Bounds();
        var preview = new PriceCheckerPlacement(700, 300, 460, 450);

        var placement = calculator.PlaceNew(preview, [preview], bounds);

        Assert.Equal(724, placement.Left);
        Assert.Equal(324, placement.Top);
        Assert.True(placement.Right <= bounds.Right);
        Assert.True(placement.Top + placement.Height <= bounds.Bottom);
    }

    [Fact]
    public void ApplyDragClampsAllEdgesInsideClient()
    {
        var bounds = Bounds();
        var placement = new PriceCheckerPlacement(300, 200, 460, 450);

        var topLeft = calculator.ApplyDrag(placement, -5000, -5000, bounds);
        var bottomRight = calculator.ApplyDrag(placement, 5000, 5000, bounds);

        Assert.Equal(bounds.Left, topLeft.Left);
        Assert.Equal(bounds.Top, topLeft.Top);
        Assert.Equal(bounds.Right, bottomRight.Right);
        Assert.Equal(bounds.Bottom, bottomRight.Top + bottomRight.Height);
    }

    [Fact]
    public void ClampUsesDipBoundsAndDoesNotApplyDpiScaleTwice()
    {
        var standard = Bounds(dpiScale: 1);
        var scaled = Bounds(dpiScale: 1.5);
        var placement = new PriceCheckerPlacement(1000, 700, 460, 450);

        var standardResult = calculator.Clamp(placement, standard);
        var scaledResult = calculator.Clamp(placement, scaled);

        Assert.Equal(standardResult, scaledResult);
        Assert.Equal(standardResult.Left * 1.5, scaledResult.Left * scaled.DpiScaleX);
        Assert.Equal(standardResult.Top * 1.5, scaledResult.Top * scaled.DpiScaleY);
    }

    private static PathOfExileClientBounds Bounds(double dpiScale = 1) => new(
        Left: 100,
        Top: 50,
        Width: 1200,
        Height: 800,
        DisplayDeviceName: @"\\.\DISPLAY1",
        DpiScaleX: dpiScale,
        DpiScaleY: dpiScale);
}
