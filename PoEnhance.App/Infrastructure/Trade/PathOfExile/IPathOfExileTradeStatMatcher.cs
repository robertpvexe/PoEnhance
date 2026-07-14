using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeStatMatcher
{
    PathOfExileTradeStatMatchResult Match(
        ParsedModifier? modifier,
        PathOfExileTradeStatCatalog? catalog,
        PathOfExileTradeStatMatchContext? context = null);

    PathOfExileTradeStatMatchResult Match(
        ResolvedSearchComponent? component,
        PathOfExileTradeStatCatalog? catalog,
        PathOfExileTradeStatMatchContext? context = null);
}
