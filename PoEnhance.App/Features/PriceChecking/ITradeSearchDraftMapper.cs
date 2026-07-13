using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Features.PriceChecking;

internal interface ITradeSearchDraftMapper
{
    TradeSearchDraftResult CreateDraft(
        ParsedItem parsedItem,
        ItemBaseResolutionResult? itemBaseResolution,
        IReadOnlyList<ModifierCandidateResolutionResult> modifierResolutions);
}
