using System.Net;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradePriceCheckDiagnostic(
    string Code,
    string Message,
    PathOfExileTradePriceCheckStage Stage,
    string? SourceCode = null,
    HttpStatusCode? HttpStatusCode = null,
    string? ProviderCode = null,
    int? ResultIndex = null);
