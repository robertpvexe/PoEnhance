using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

public sealed record PriceCheckerWindowState(
    TradeSearchDraft Draft,
    TradeSearchValidationResult ValidationResult);
