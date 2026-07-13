using System.Net;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeHttpDiagnostic(
    string Code,
    string Message,
    HttpStatusCode? HttpStatusCode = null,
    string? ProviderCode = null,
    int? ResultIndex = null);
