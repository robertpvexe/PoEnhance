namespace PoEnhance.App.Features.PriceChecking;

internal sealed record PriceCheckerWindowUpdateResult(
    bool IsSuccess,
    string Diagnostic)
{
    public static PriceCheckerWindowUpdateResult Success()
    {
        return new PriceCheckerWindowUpdateResult(true, "Price Checker updated");
    }

    public static PriceCheckerWindowUpdateResult Failure(string diagnostic)
    {
        return new PriceCheckerWindowUpdateResult(false, diagnostic);
    }
}
