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

    private readonly PriceCheckerPlacementCalculator calculator = new();

    [Fact]
    public void CalculatePlacement_UsesClientTopAndFullClientHeight()
    {
        var placement = calculator.CalculatePlacement(ClientBounds, horizontalCorrection: 0);

        Assert.Equal(50, placement.Top);
        Assert.Equal(800, placement.Height);
        Assert.Equal(360, placement.Width);
    }

    [Fact]
    public void CalculatePlacement_DerivesAutomaticPositionFromInventoryHeuristic()
    {
        var placement = calculator.CalculatePlacement(ClientBounds, horizontalCorrection: 0);

        var inventoryLeft = calculator.CalculateEstimatedInventoryLeft(ClientBounds);
        Assert.Equal(248, placement.Left);
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

        Assert.Equal(208, placement.Left);
    }

    [Theory]
    [InlineData(-1000, 100)]
    [InlineData(1000, 248)]
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

        Assert.Equal(223, dragged.Left);
        Assert.Equal(placement.Top, dragged.Top);
        Assert.Equal(placement.Width, dragged.Width);
        Assert.Equal(placement.Height, dragged.Height);
    }

    [Fact]
    public void ApplyHorizontalDrag_KeepsCurrentWidthAndPinsYHeightToClientArea()
    {
        var placement = new PriceCheckerPlacement(Left: 200, Top: 999, Width: 400, Height: 456);

        var dragged = calculator.ApplyHorizontalDrag(ClientBounds, placement, horizontalChange: -10);

        Assert.Equal(ClientBounds.Top, dragged.Top);
        Assert.Equal(400, dragged.Width);
        Assert.Equal(ClientBounds.Height, dragged.Height);
    }

    [Theory]
    [InlineData(0, 0, 1920, 1080, 837.6)]
    [InlineData(0, 0, 1920, 1200, 765.6)]
    [InlineData(0, 0, 3440, 1440, 2084)]
    [InlineData(50, 80, 1280, 720, 526)]
    [InlineData(-800, 120, 1600, 900, -112)]
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
    [InlineData(1600, 900, 360)]
    [InlineData(1920, 1080, 422.4)]
    [InlineData(3440, 1440, 480)]
    [InlineData(1200, 720, 360)]
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
    [InlineData(1600, 900, 658)]
    [InlineData(1920, 1080, 807.6)]
    [InlineData(3440, 1440, 2054)]
    [InlineData(1200, 720, 366)]
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

    [Fact]
    public void CalculatePlacement_UsesExplicitPanelWidthAndPreservesAutomaticRightEdge()
    {
        var placement = calculator.CalculatePlacement(
            ClientBounds,
            horizontalCorrection: 0,
            requestedPanelWidth: 420);

        var inventoryLeft = calculator.CalculateEstimatedInventoryLeft(ClientBounds);
        Assert.Equal(420, placement.Width);
        Assert.Equal(inventoryLeft - calculator.InventorySafetyGap, placement.Right);
    }

    [Fact]
    public void ApplyHorizontalResizeFromLeft_PreservesRightEdgeAndChangesWidthOnly()
    {
        var placement = calculator.CalculatePlacement(ClientBounds, horizontalCorrection: 0);

        var resized = calculator.ApplyHorizontalResizeFromLeft(
            ClientBounds,
            placement,
            horizontalChange: -50);

        Assert.Equal(placement.Right, resized.Right);
        Assert.Equal(410, resized.Width);
        Assert.Equal(198, resized.Left);
        Assert.Equal(ClientBounds.Top, resized.Top);
        Assert.Equal(ClientBounds.Height, resized.Height);
    }

    [Fact]
    public void ApplyHorizontalResizeFromLeft_ClampsMinimumWidth()
    {
        var placement = calculator.CalculatePlacement(ClientBounds, horizontalCorrection: 0);

        var resized = calculator.ApplyHorizontalResizeFromLeft(
            ClientBounds,
            placement,
            horizontalChange: 100);

        Assert.Equal(320, resized.Width);
        Assert.Equal(placement.Right, resized.Right);
    }

    [Fact]
    public void ApplyHorizontalResizeFromLeft_ClampsMaximumWidthToClientLeftMargin()
    {
        var placement = calculator.CalculatePlacement(ClientBounds, horizontalCorrection: 0);

        var resized = calculator.ApplyHorizontalResizeFromLeft(
            ClientBounds,
            placement,
            horizontalChange: -200);

        Assert.Equal(500, resized.Width);
        Assert.Equal(placement.Right, resized.Right);
        Assert.Equal(
            ClientBounds.Left + PriceCheckerPlacementCalculator.UserPanelLeftMargin,
            resized.Left);
    }

    [Fact]
    public void CalculateMaximumUserPanelWidth_IsNotCappedAtOldSevenHundredTwentyDipLimit()
    {
        var bounds = Bounds(width: 2200, height: 900);

        var maximumWidth = calculator.CalculateMaximumUserPanelWidth(
            bounds,
            preservedRight: 1000);

        Assert.Equal(992, maximumWidth);
        Assert.True(maximumWidth > 720);
    }

    [Fact]
    public void CalculateMaximumUserPanelWidth_IsNotCappedAtOldFortyFivePercentClientRatio()
    {
        var bounds = Bounds(width: 1000, height: 800);

        var maximumWidth = calculator.CalculateMaximumUserPanelWidth(
            bounds,
            preservedRight: 900);

        Assert.Equal(892, maximumWidth);
        Assert.True(maximumWidth > bounds.Width * 0.45d);
    }

    [Fact]
    public void CalculateHorizontalCorrection_UsesCurrentPanelWidth()
    {
        var automatic = calculator.CalculatePlacement(
            ClientBounds,
            horizontalCorrection: 0,
            requestedPanelWidth: 420);
        var moved = automatic with { Left = automatic.Left - 20 };

        var correction = calculator.CalculateHorizontalCorrection(ClientBounds, moved);

        Assert.Equal(-20, correction);
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
