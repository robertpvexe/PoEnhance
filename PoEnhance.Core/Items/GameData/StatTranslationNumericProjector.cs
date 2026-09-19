namespace PoEnhance.Core.Items.GameData;

/// <summary>
/// Projects authoritative source-domain numeric values into the displayed numeric space
/// defined by GameData translation index handlers. Fail-closed for unsupported handlers.
/// </summary>
internal static class StatTranslationNumericProjector
{
    public static bool TryProjectValue(
        string? handler,
        decimal value,
        out decimal projected)
    {
        projected = value;
        switch (Normalize(handler))
        {
            case null:
            case "":
                return true;

            case "divide_by_one_hundred":
            case "divide_by_one_hundred_2dp":
            case "divide_by_one_hundred_2dp_if_required":
                projected = Round(value / 100m, 2);
                return true;

            case "divide_by_one_hundred_and_negate":
                projected = Round(-value / 100m, 2);
                return true;

            case "divide_by_ten_0dp":
                projected = TruncateTowardZero(value / 10m);
                return true;

            case "divide_by_ten_1dp":
            case "divide_by_ten_1dp_if_required":
            case "locations_to_metres":
            case "deciseconds_to_seconds":
                projected = Round(value / 10m, 1);
                return true;

            case "divide_by_two_0dp":
            case "divide_by_two_0dp_if_required":
                projected = TruncateTowardZero(value / 2m);
                return true;

            case "divide_by_five_0dp":
                projected = TruncateTowardZero(value / 5m);
                return true;

            case "double":
                projected = value * 2m;
                return true;

            case "negate":
                projected = -value;
                return true;

            case "negate_and_double":
                projected = -value * 2m;
                return true;

            case "milliseconds_to_seconds":
                projected = value / 1000m;
                return true;

            case "milliseconds_to_seconds_0dp":
                projected = Round(value / 1000m, 0);
                return true;

            case "milliseconds_to_seconds_1dp":
                projected = Round(value / 1000m, 1);
                return true;

            case "milliseconds_to_seconds_2dp":
            case "milliseconds_to_seconds_2dp_if_required":
                projected = Round(value / 1000m, 2);
                return true;

            case "per_minute_to_per_second":
            case "per_minute_to_per_second_1dp":
                projected = Round(value / 60m, 1);
                return true;

            case "per_minute_to_per_second_0dp":
                projected = Round(value / 60m, 0);
                return true;

            case "per_minute_to_per_second_2dp":
            case "per_minute_to_per_second_2dp_if_required":
                projected = Round(value / 60m, 2);
                return true;

            case "old_leech_percent":
                projected = value / 5m;
                return true;

            case "old_leech_permyriad":
                projected = value / 500m;
                return true;

            default:
                return false;
        }
    }

    public static bool TryProjectValue(
        IReadOnlyList<string> handlers,
        decimal value,
        out decimal projected)
    {
        projected = value;
        foreach (var handler in handlers)
        {
            if (!TryProjectValue(handler, projected, out projected))
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryProjectBounds(
        IReadOnlyList<string> handlers,
        decimal minimum,
        decimal maximum,
        out decimal displayMinimum,
        out decimal displayMaximum)
    {
        displayMinimum = 0m;
        displayMaximum = 0m;
        if (!TryProjectValue(handlers, minimum, out var projectedMinimum) ||
            !TryProjectValue(handlers, maximum, out var projectedMaximum))
        {
            return false;
        }

        displayMinimum = Math.Min(projectedMinimum, projectedMaximum);
        displayMaximum = Math.Max(projectedMinimum, projectedMaximum);
        return true;
    }

    public static bool IsSupported(string? handler)
    {
        return TryProjectValue(handler, 1m, out _);
    }

    private static string? Normalize(string? handler)
    {
        var trimmed = handler?.Trim();
        return string.IsNullOrEmpty(trimmed) ? trimmed : trimmed.ToLowerInvariant();
    }

    private static decimal Round(decimal value, int decimals) =>
        decimal.Round(value, decimals, MidpointRounding.AwayFromZero);

    private static decimal TruncateTowardZero(decimal value) =>
        decimal.Truncate(value);
}
