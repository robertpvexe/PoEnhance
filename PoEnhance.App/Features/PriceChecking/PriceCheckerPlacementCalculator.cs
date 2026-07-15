namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PriceCheckerPlacementCalculator
{
    public const double ProvisionalPanelWidthRatio = 0.30d;
    public const double ProvisionalPanelMinimumWidth = 360d;
    public const double ProvisionalPanelMaximumWidth = 560d;
    public const double ProvisionalInventoryWidthToClientHeightRatio = 0.60d;
    public const double ProvisionalInventorySafetyGap = 12d;
    public const double UserPanelMinimumWidth = 320d;
    public const double UserPanelLeftMargin = 8d;

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
        return CalculatePlacement(
            clientBounds,
            horizontalCorrection,
            CalculatePanelWidth(clientBounds));
    }

    public PriceCheckerPlacement CalculatePlacement(
        PathOfExileClientBounds clientBounds,
        double horizontalCorrection,
        double requestedPanelWidth)
    {
        var panelWidth = ClampPanelWidth(clientBounds, requestedPanelWidth);
        var automaticLeft = CalculateAutomaticLeft(clientBounds, panelWidth);
        var correctedLeft = ClampLeft(clientBounds, automaticLeft + horizontalCorrection, panelWidth);

        return new PriceCheckerPlacement(
            correctedLeft,
            clientBounds.Top,
            panelWidth,
            clientBounds.Height);
    }

    public double CalculateAutomaticLeft(PathOfExileClientBounds clientBounds)
    {
        return CalculateAutomaticLeft(clientBounds, CalculatePanelWidth(clientBounds));
    }

    public double CalculateAutomaticLeft(
        PathOfExileClientBounds clientBounds,
        double requestedPanelWidth)
    {
        var panelWidth = ClampPanelWidth(clientBounds, requestedPanelWidth);
        var inventoryLeft = CalculateEstimatedInventoryLeft(clientBounds);
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

    public double ClampPanelWidth(
        PathOfExileClientBounds clientBounds,
        double requestedPanelWidth)
    {
        return ClampUserPanelWidth(
            clientBounds,
            requestedPanelWidth,
            CalculateInventorySafeRight(clientBounds));
    }

    public double ClampUserPanelWidth(
        PathOfExileClientBounds clientBounds,
        double requestedPanelWidth,
        double preservedRight)
    {
        var maximumWidth = CalculateMaximumUserPanelWidth(clientBounds, preservedRight);
        var minimumWidth = Math.Min(UserPanelMinimumWidth, maximumWidth);
        return Math.Clamp(requestedPanelWidth, minimumWidth, maximumWidth);
    }

    public double CalculateMaximumUserPanelWidth(
        PathOfExileClientBounds clientBounds,
        double preservedRight)
    {
        return Math.Max(
            0d,
            preservedRight - clientBounds.Left - UserPanelLeftMargin);
    }

    public PriceCheckerPlacement ApplyHorizontalDrag(
        PathOfExileClientBounds clientBounds,
        PriceCheckerPlacement currentPlacement,
        double horizontalChange)
    {
        var panelWidth = ClampPanelWidth(clientBounds, currentPlacement.Width);

        return currentPlacement with
        {
            Left = ClampLeft(clientBounds, currentPlacement.Left + horizontalChange, panelWidth),
            Top = clientBounds.Top,
            Width = panelWidth,
            Height = clientBounds.Height,
        };
    }

    public PriceCheckerPlacement ApplyHorizontalResizeFromLeft(
        PathOfExileClientBounds clientBounds,
        PriceCheckerPlacement currentPlacement,
        double horizontalChange)
    {
        var preservedRight = currentPlacement.Right;
        var requestedPanelWidth = currentPlacement.Width - horizontalChange;
        var panelWidth = ClampUserPanelWidth(clientBounds, requestedPanelWidth, preservedRight);

        return currentPlacement with
        {
            Left = preservedRight - panelWidth,
            Top = clientBounds.Top,
            Width = panelWidth,
            Height = clientBounds.Height,
        };
    }

    public double CalculateHorizontalCorrection(
        PathOfExileClientBounds clientBounds,
        PriceCheckerPlacement currentPlacement)
    {
        return currentPlacement.Left - CalculateAutomaticLeft(clientBounds, currentPlacement.Width);
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

    private double ClampRight(
        PathOfExileClientBounds clientBounds,
        double requestedRight)
    {
        return Math.Clamp(
            requestedRight,
            clientBounds.Left,
            CalculateInventorySafeRight(clientBounds));
    }

    private double CalculateInventorySafeRight(PathOfExileClientBounds clientBounds)
    {
        return Math.Min(
            clientBounds.Right,
            CalculateEstimatedInventoryLeft(clientBounds) - InventorySafetyGap);
    }
}
