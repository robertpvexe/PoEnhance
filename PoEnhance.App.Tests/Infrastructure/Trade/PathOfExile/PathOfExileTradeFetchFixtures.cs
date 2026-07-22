namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradeFetchFixtures
{
    public static string OfferCardResponse()
    {
        return File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "Trade",
            "fetch-offer-card-response.json"));
    }

    public static string LiveModifierObjectResponse()
    {
        return File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "Trade",
            "fetch-offer-card-live-modifier-response.json"));
    }
}
