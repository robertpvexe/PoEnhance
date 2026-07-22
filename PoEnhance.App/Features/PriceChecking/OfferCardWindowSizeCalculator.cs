namespace PoEnhance.App.Features.PriceChecking;

internal sealed class OfferCardWindowSizeCalculator
{
    public const double MinimumUsefulWidth = 400d;
    public const double MaximumUsefulWidth = 700d;
    public const double MaximumClientHeightRatio = 0.86d;

    public double CalculateWidth(double measuredContentWidth, double clientWidth)
    {
        ValidatePositiveFinite(measuredContentWidth, nameof(measuredContentWidth));
        ValidatePositiveFinite(clientWidth, nameof(clientWidth));
        var maximumAllowedWidth = Math.Min(MaximumUsefulWidth, clientWidth);
        return Math.Min(
            maximumAllowedWidth,
            Math.Max(MinimumUsefulWidth, measuredContentWidth));
    }

    public OfferCardWindowSize Calculate(
        double clientHeight,
        double headerHeight,
        double contentHeight,
        double footerHeight,
        double verticalChromeHeight)
    {
        ValidatePositiveFinite(clientHeight, nameof(clientHeight));
        ValidateNonNegativeFinite(headerHeight, nameof(headerHeight));
        ValidateNonNegativeFinite(contentHeight, nameof(contentHeight));
        ValidateNonNegativeFinite(footerHeight, nameof(footerHeight));
        ValidateNonNegativeFinite(verticalChromeHeight, nameof(verticalChromeHeight));

        var maximumHeight = clientHeight * MaximumClientHeightRatio;
        var naturalHeight =
            headerHeight + contentHeight + footerHeight + verticalChromeHeight;
        var height = Math.Max(1d, Math.Min(naturalHeight, maximumHeight));
        var contentViewportHeight = Math.Max(
            1d,
            height - headerHeight - footerHeight - verticalChromeHeight);
        return new OfferCardWindowSize(
            height,
            maximumHeight,
            contentViewportHeight,
            naturalHeight > maximumHeight);
    }

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0d)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateNonNegativeFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0d)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

internal sealed record OfferCardWindowSize(
    double Height,
    double MaximumHeight,
    double ContentViewportHeight,
    bool IsContentScrollingRequired);
