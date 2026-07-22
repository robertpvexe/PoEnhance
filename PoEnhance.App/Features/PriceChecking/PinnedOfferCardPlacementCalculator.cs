namespace PoEnhance.App.Features.PriceChecking;

internal sealed class PinnedOfferCardPlacementCalculator
{
    public const double CascadeOffset = 24d;

    public PriceCheckerPlacement PlaceNew(
        PriceCheckerPlacement previewPlacement,
        IReadOnlyCollection<PriceCheckerPlacement> existingPlacements,
        PathOfExileClientBounds clientBounds)
    {
        ArgumentNullException.ThrowIfNull(previewPlacement);
        ArgumentNullException.ThrowIfNull(existingPlacements);
        ArgumentNullException.ThrowIfNull(clientBounds);

        var placement = Clamp(previewPlacement, clientBounds);
        for (var offsetIndex = 0;
             offsetIndex < existingPlacements.Count &&
             existingPlacements.Any(existing => StronglyOverlaps(placement, existing));
             offsetIndex++)
        {
            placement = Clamp(
                placement with
                {
                    Left = placement.Left + CascadeOffset,
                    Top = placement.Top + CascadeOffset,
                },
                clientBounds);
        }

        return placement;
    }

    public PriceCheckerPlacement ApplyDrag(
        PriceCheckerPlacement placement,
        double horizontalChange,
        double verticalChange,
        PathOfExileClientBounds clientBounds)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(clientBounds);
        if (!double.IsFinite(horizontalChange))
        {
            throw new ArgumentOutOfRangeException(nameof(horizontalChange));
        }

        if (!double.IsFinite(verticalChange))
        {
            throw new ArgumentOutOfRangeException(nameof(verticalChange));
        }

        return Clamp(
            placement with
            {
                Left = placement.Left + horizontalChange,
                Top = placement.Top + verticalChange,
            },
            clientBounds);
    }

    public PriceCheckerPlacement Clamp(
        PriceCheckerPlacement placement,
        PathOfExileClientBounds clientBounds)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(clientBounds);
        if (!clientBounds.IsUsable ||
            !double.IsFinite(placement.Left) ||
            !double.IsFinite(placement.Top) ||
            !double.IsFinite(placement.Width) ||
            !double.IsFinite(placement.Height) ||
            placement.Width <= 0d ||
            placement.Height <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(placement));
        }

        var width = Math.Min(placement.Width, clientBounds.Width);
        var height = Math.Min(placement.Height, clientBounds.Height);
        return new PriceCheckerPlacement(
            Math.Clamp(
                placement.Left,
                clientBounds.Left,
                Math.Max(clientBounds.Left, clientBounds.Right - width)),
            Math.Clamp(
                placement.Top,
                clientBounds.Top,
                Math.Max(clientBounds.Top, clientBounds.Bottom - height)),
            width,
            height);
    }

    private static bool StronglyOverlaps(
        PriceCheckerPlacement first,
        PriceCheckerPlacement second)
    {
        var overlapWidth = Math.Max(
            0d,
            Math.Min(first.Right, second.Right) - Math.Max(first.Left, second.Left));
        var overlapHeight = Math.Max(
            0d,
            Math.Min(first.Top + first.Height, second.Top + second.Height) -
            Math.Max(first.Top, second.Top));
        var overlapArea = overlapWidth * overlapHeight;
        var smallerArea = Math.Min(
            first.Width * first.Height,
            second.Width * second.Height);
        return smallerArea > 0d && overlapArea / smallerArea >= 0.5d;
    }
}
