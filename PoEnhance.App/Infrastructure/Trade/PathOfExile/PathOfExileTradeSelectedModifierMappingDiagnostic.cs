namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed record PathOfExileTradeSelectedModifierMappingDiagnostic(
    string Code,
    string Message,
    int? SourceIndex = null,
    string? SourceCode = null);
