namespace PoEnhance.App.Features.PriceChecking;

internal static class PriceCheckerOfferCapacityCalculator
{
    public const double RowHeight = 36d;

    public static int Calculate(double availableHeight)
    {
        return !double.IsFinite(availableHeight) || availableHeight <= 0d
            ? 0
            : Math.Max(0, (int)Math.Floor(availableHeight / RowHeight));
    }
}
