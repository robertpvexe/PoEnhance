using PoEnhance.App.Features.PriceChecking;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerPlacementCalculatorTests
{
    private static readonly PathOfExileClientBounds ClientBounds = new(
        Left: 100,
        Top: 50,
        Width: 1000,
        Height: 800,
        DisplayDeviceName: @"\\.\DISPLAY1",
        DpiScaleX: 1,
        DpiScaleY: 1);

    private readonly PriceCheckerPlacementCalculator calculator = new(
        panelWidthRatio: 0.18,
        minimumPanelWidth: 280,
        maximumPanelWidth: 360,
        inventoryWidthToClientHeightRatio: 0.60,
        inventorySafetyGap: 12);

    [Fact]
    public void CalculatePlacement_UsesClientTopAndFullClientHeight()
    {
        var placement = calculator.CalculatePlacement(ClientBounds, horizontalCorrection: 0);

        Assert.Equal(50, placement.Top);
        Assert.Equal(800, placement.Height);
        Assert.Equal(280, placement.Width);
    }

    [Fact]
    public void CalculatePlacement_DerivesAutomaticPositionFromInventoryHeuristic()
    {
        var placement = calculator.CalculatePlacement(ClientBounds, horizontalCorrection: 0);

        var inventoryLeft = calculator.CalculateEstimatedInventoryLeft(ClientBounds);
        Assert.Equal(328, placement.Left);
        Assert.Equal(inventoryLeft - calculator.InventorySafetyGap, placement.Right);
    }

    [Fact]
    public void CalculatePlacement_ZeroCorrectionProducesAutomaticPosition()
    {
        var automaticLeft = calculator.CalculateAutomaticLeft(ClientBounds);
        var placement = calculator.CalculatePlacement(ClientBounds, horizontalCorrection: 0);

        Assert.Equal(automaticLeft, placement.Left);
    }

    [Fact]
    public void CalculatePlacement_PositiveHorizontalCorrectionCannotOverlapEstimatedInventory()
    {
        var placement = calculator.CalculatePlacement(ClientBounds, horizontalCorrection: 40);

        Assert.Equal(calculator.CalculateAutomaticLeft(ClientBounds), placement.Left);
    }

    [Fact]
    public void CalculatePlacement_NegativeHorizontalCorrectionIsApplied()
    {
        var placement = calculator.CalculatePlacement(ClientBounds, horizontalCorrection: -40);

        Assert.Equal(288, placement.Left);
    }

    [Theory]
    [InlineData(-1000, 100)]
    [InlineData(1000, 328)]
    public void CalculatePlacement_ClampsHorizontalPositionToClientBounds(
        double correction,
        double expectedLeft)
    {
        var placement = calculator.CalculatePlacement(ClientBounds, correction);

        Assert.Equal(expectedLeft, placement.Left);
    }

    [Fact]
    public void ApplyHorizontalDrag_ChangesXOnly()
    {
        var placement = calculator.CalculatePlacement(ClientBounds, horizontalCorrection: 0);

        var dragged = calculator.ApplyHorizontalDrag(ClientBounds, placement, horizontalChange: -25);

        Assert.Equal(303, dragged.Left);
        Assert.Equal(placement.Top, dragged.Top);
        Assert.Equal(placement.Width, dragged.Width);
        Assert.Equal(placement.Height, dragged.Height);
    }

    [Fact]
    public void ApplyHorizontalDrag_KeepsYWidthAndHeightPinnedToClientArea()
    {
        var placement = new PriceCheckerPlacement(Left: 500, Top: 999, Width: 123, Height: 456);

        var dragged = calculator.ApplyHorizontalDrag(ClientBounds, placement, horizontalChange: 10);

        Assert.Equal(ClientBounds.Top, dragged.Top);
        Assert.Equal(280, dragged.Width);
        Assert.Equal(ClientBounds.Height, dragged.Height);
    }

    [Theory]
    [InlineData(0, 0, 1920, 1080, 914.4)]
    [InlineData(0, 0, 1920, 1200, 842.4)]
    [InlineData(0, 0, 3440, 1440, 2204)]
    [InlineData(50, 80, 1280, 720, 606)]
    [InlineData(-800, 120, 1600, 900, -40)]
    public void CalculatePlacement_UsesRightAnchoredClientHeightHeuristicAcrossClientShapes(
        double left,
        double top,
        double width,
        double height,
        double expectedLeft)
    {
        var bounds = new PathOfExileClientBounds(
            left,
            top,
            width,
            height,
            @"\\.\DISPLAY1",
            DpiScaleX: 1,
            DpiScaleY: 1);

        var placement = calculator.CalculatePlacement(bounds, horizontalCorrection: 0);
        var inventoryLeft = calculator.CalculateEstimatedInventoryLeft(bounds);

        Assert.Equal(expectedLeft, placement.Left, precision: 6);
        Assert.Equal(inventoryLeft - calculator.InventorySafetyGap, placement.Right, precision: 6);
        Assert.True(placement.Right <= inventoryLeft - calculator.InventorySafetyGap);
        Assert.Equal(bounds.Top, placement.Top);
        Assert.Equal(bounds.Height, placement.Height);
    }

    [Theory]
    [InlineData(1600, 900, 288)]
    [InlineData(1920, 1080, 345.6)]
    [InlineData(3440, 1440, 360)]
    [InlineData(1200, 720, 280)]
    public void CalculatePanelWidth_UsesResponsiveProvisionalClamp(
        double clientWidth,
        double clientHeight,
        double expectedWidth)
    {
        var bounds = Bounds(width: clientWidth, height: clientHeight);

        var panelWidth = calculator.CalculatePanelWidth(bounds);

        Assert.Equal(expectedWidth, panelWidth, precision: 6);
    }

    [Theory]
    [InlineData(1600, 900, 730)]
    [InlineData(1920, 1080, 884.4)]
    [InlineData(3440, 1440, 2174)]
    [InlineData(1200, 720, 446)]
    public void CalculatePlacement_AppliesCorrectionWithResponsivePanelWidth(
        double clientWidth,
        double clientHeight,
        double expectedLeft)
    {
        var bounds = Bounds(width: clientWidth, height: clientHeight);

        var placement = calculator.CalculatePlacement(bounds, horizontalCorrection: -30);
        var inventoryLeft = calculator.CalculateEstimatedInventoryLeft(bounds);

        Assert.Equal(expectedLeft, placement.Left, precision: 6);
        Assert.Equal(
            calculator.CalculateAutomaticLeft(bounds) - 30,
            placement.Left,
            precision: 6);
        Assert.True(placement.Right <= inventoryLeft - calculator.InventorySafetyGap);
    }

    private static PathOfExileClientBounds Bounds(double width, double height)
    {
        return new PathOfExileClientBounds(
            Left: 0,
            Top: 0,
            Width: width,
            Height: height,
            DisplayDeviceName: @"\\.\DISPLAY1",
            DpiScaleX: 1,
            DpiScaleY: 1);
    }
}
