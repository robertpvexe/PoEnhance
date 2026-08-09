namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeLeagueEntry(
    string ProviderId,
    string DisplayText,
    string Realm,
    int ProviderOrder);
