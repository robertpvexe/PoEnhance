using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class CoreTradeSearchDraftValidatorAdapter : ITradeSearchDraftValidator
{
    private readonly TradeSearchDraftValidator validator = new();

    public TradeSearchValidationResult Validate(TradeSearchDraft draft)
    {
        return validator.Validate(draft);
    }
}
