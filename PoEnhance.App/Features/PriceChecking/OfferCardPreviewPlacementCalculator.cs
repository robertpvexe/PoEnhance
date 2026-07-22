namespace PoEnhance.App.Features.PriceChecking;

internal sealed class OfferCardPreviewPlacementCalculator
{
    public const double HorizontalGap = 10d;

    public PriceCheckerPlacement Calculate(
        PathOfExileClientBounds clientBounds,
        PriceCheckerPlacement priceCheckerBounds,
        OfferCardPreviewSize requestedSize)
    {
        ArgumentNullException.ThrowIfNull(clientBounds);
        ArgumentNullException.ThrowIfNull(priceCheckerBounds);
        ArgumentNullException.ThrowIfNull(requestedSize);

        if (!clientBounds.IsUsable ||
            !double.IsFinite(requestedSize.Width) ||
            !double.IsFinite(requestedSize.Height) ||
            requestedSize.Width <= 0d ||
            requestedSize.Height <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedSize));
        }

        var width = Math.Min(requestedSize.Width, clientBounds.Width);
        var height = Math.Min(requestedSize.Height, clientBounds.Height);
        var top = Clamp(
            priceCheckerBounds.Top,
            clientBounds.Top,
            clientBounds.Bottom - height);
        var leftCandidate = priceCheckerBounds.Left - HorizontalGap - width;
        if (leftCandidate >= clientBounds.Left)
        {
            return new PriceCheckerPlacement(leftCandidate, top, width, height);
        }

        var rightCandidate = priceCheckerBounds.Right + HorizontalGap;
        if (rightCandidate + width <= clientBounds.Right)
        {
            return new PriceCheckerPlacement(rightCandidate, top, width, height);
        }

        var clampedLeft = Clamp(
            leftCandidate,
            clientBounds.Left,
            clientBounds.Right - width);
        return new PriceCheckerPlacement(clampedLeft, top, width, height);
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        return Math.Clamp(value, minimum, Math.Max(minimum, maximum));
    }
}
