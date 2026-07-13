namespace PoEnhance.Core.Trade;

public sealed record TradeSearchValidationDiagnostic(
    string Code,
    TradeSearchValidationSeverity Severity,
    string Message,
    int? ModifierFilterIndex = null);
