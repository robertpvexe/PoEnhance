using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

internal interface ITradeSearchDraftValidator
{
    TradeSearchValidationResult Validate(TradeSearchDraft draft);
}
