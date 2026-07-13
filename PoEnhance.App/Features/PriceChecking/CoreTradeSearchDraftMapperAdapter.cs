using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class CoreTradeSearchDraftMapperAdapter : ITradeSearchDraftMapper
{
    private readonly TradeSearchDraftMapper mapper = new();

    public TradeSearchDraftResult CreateDraft(
        ParsedItem parsedItem,
        ItemBaseResolutionResult? itemBaseResolution,
        IReadOnlyList<ModifierCandidateResolutionResult> modifierResolutions)
    {
        return mapper.CreateDraft(
            parsedItem,
            itemBaseResolution,
            modifierResolutions);
    }
}
