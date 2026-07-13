namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerPlacementCalculator
{
    public const double ProvisionalPanelWidthRatio = 0.18d;
    public const double ProvisionalPanelMinimumWidth = 280d;
    public const double ProvisionalPanelMaximumWidth = 360d;
    public const double ProvisionalInventoryWidthToClientHeightRatio = 0.60d;
    public const double ProvisionalInventorySafetyGap = 12d;

    public PriceCheckerPlacementCalculator()
        : this(
            ProvisionalPanelWidthRatio,
            ProvisionalPanelMinimumWidth,
            ProvisionalPanelMaximumWidth,
            ProvisionalInventoryWidthToClientHeightRatio,
            ProvisionalInventorySafetyGap)
    {
    }

    public PriceCheckerPlacementCalculator(
        double panelWidthRatio,
        double minimumPanelWidth,
        double maximumPanelWidth,
        double inventoryWidthToClientHeightRatio,
        double inventorySafetyGap)
    {
        if (panelWidthRatio <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(panelWidthRatio));
        }

        if (minimumPanelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumPanelWidth));
        }

        if (maximumPanelWidth < minimumPanelWidth)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPanelWidth));
        }

        if (inventoryWidthToClientHeightRatio <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inventoryWidthToClientHeightRatio));
        }

        if (inventorySafetyGap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inventorySafetyGap));
        }

        PanelWidthRatio = panelWidthRatio;
        MinimumPanelWidth = minimumPanelWidth;
        MaximumPanelWidth = maximumPanelWidth;
        InventoryWidthToClientHeightRatio = inventoryWidthToClientHeightRatio;
        InventorySafetyGap = inventorySafetyGap;
    }

    public double PanelWidthRatio { get; }

    public double MinimumPanelWidth { get; }

    public double MaximumPanelWidth { get; }

    public double InventoryWidthToClientHeightRatio { get; }

    public double InventorySafetyGap { get; }

    public PriceCheckerPlacement CalculatePlacement(
        PathOfExileClientBounds clientBounds,
        double horizontalCorrection)
    {
        var automaticLeft = CalculateAutomaticLeft(clientBounds);
        var panelWidth = CalculatePanelWidth(clientBounds);
        var correctedLeft = ClampLeft(clientBounds, automaticLeft + horizontalCorrection, panelWidth);

        return new PriceCheckerPlacement(
            correctedLeft,
            clientBounds.Top,
            panelWidth,
            clientBounds.Height);
    }

    public double CalculateAutomaticLeft(PathOfExileClientBounds clientBounds)
    {
        var inventoryLeft = CalculateEstimatedInventoryLeft(clientBounds);
        var panelWidth = CalculatePanelWidth(clientBounds);
        return ClampLeft(
            clientBounds,
            inventoryLeft - InventorySafetyGap - panelWidth,
            panelWidth);
    }

    public double CalculatePanelWidth(PathOfExileClientBounds clientBounds)
    {
        return Math.Clamp(
            clientBounds.Width * PanelWidthRatio,
            MinimumPanelWidth,
            MaximumPanelWidth);
    }

    public double CalculateEstimatedInventoryLeft(PathOfExileClientBounds clientBounds)
    {
        return clientBounds.Right - (clientBounds.Height * InventoryWidthToClientHeightRatio);
    }

    public PriceCheckerPlacement ApplyHorizontalDrag(
        PathOfExileClientBounds clientBounds,
        PriceCheckerPlacement currentPlacement,
        double horizontalChange)
    {
        var panelWidth = CalculatePanelWidth(clientBounds);

        return currentPlacement with
        {
            Left = ClampLeft(clientBounds, currentPlacement.Left + horizontalChange, panelWidth),
            Top = clientBounds.Top,
            Width = panelWidth,
            Height = clientBounds.Height,
        };
    }

    public double CalculateHorizontalCorrection(
        PathOfExileClientBounds clientBounds,
        PriceCheckerPlacement currentPlacement)
    {
        return currentPlacement.Left - CalculateAutomaticLeft(clientBounds);
    }

    private double ClampLeft(
        PathOfExileClientBounds clientBounds,
        double requestedLeft,
        double panelWidth)
    {
        var minimumLeft = clientBounds.Left;
        var maximumClientLeft = clientBounds.Right - panelWidth;
        var maximumInventorySafeLeft =
            CalculateEstimatedInventoryLeft(clientBounds) - InventorySafetyGap - panelWidth;
        var maximumLeft = Math.Max(
            minimumLeft,
            Math.Min(maximumClientLeft, maximumInventorySafeLeft));

        return Math.Clamp(requestedLeft, minimumLeft, maximumLeft);
    }
}
