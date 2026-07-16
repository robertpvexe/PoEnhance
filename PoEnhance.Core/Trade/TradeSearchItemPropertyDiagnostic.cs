using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Trade;

public sealed record TradeSearchItemPropertyDiagnostic(
    string Code,
    string Message,
    ParsedItemProperty? SourceProperty = null);
