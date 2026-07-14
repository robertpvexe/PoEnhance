using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal interface IPathOfExileTradeStatMatcher
{
    PathOfExileTradeStatMatchResult Match(
        ParsedModifier? modifier,
        PathOfExileTradeStatCatalog? catalog);
}
