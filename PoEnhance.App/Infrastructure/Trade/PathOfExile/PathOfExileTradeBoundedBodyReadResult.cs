namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeBoundedBodyReadResult(bool IsSuccess, string Content)
{
    public static PathOfExileTradeBoundedBodyReadResult Success(string content)
    {
        return new PathOfExileTradeBoundedBodyReadResult(true, content);
    }

    public static PathOfExileTradeBoundedBodyReadResult TooLarge()
    {
        return new PathOfExileTradeBoundedBodyReadResult(false, string.Empty);
    }
}
