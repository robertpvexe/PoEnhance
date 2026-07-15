namespace PoEnhance.App.Features.PriceChecking;

internal static class PriceCheckerRelativeTimeFormatter
{
    public const string UnavailableText = "—";

    public static string Format(DateTimeOffset? listedAt, DateTimeOffset now)
    {
        if (!listedAt.HasValue)
        {
            return UnavailableText;
        }

        var elapsed = now.ToUniversalTime() - listedAt.Value.ToUniversalTime();
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "1 min ago";
        }

        var completedMinutes = (long)Math.Floor(elapsed.TotalMinutes);
        if (completedMinutes < 60)
        {
            return $"{completedMinutes} min ago";
        }

        var completedHours = (long)Math.Floor(elapsed.TotalHours);
        if (completedHours < 24)
        {
            return $"{completedHours}h ago";
        }

        var completedDays = (long)Math.Floor(elapsed.TotalDays);
        if (completedDays < 30)
        {
            return $"{completedDays}d ago";
        }

        return $"{completedDays / 30}mo ago";
    }
}
