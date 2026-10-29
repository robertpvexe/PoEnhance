using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Trade;

/// <summary>
/// Builds requested item-state search defaults. Observed facts on <see cref="ParsedItem"/>
/// remain separate; only positively observed Mirrored becomes a requested Yes.
/// </summary>
public static class TradeItemStateCriteriaFactory
{
    public static TradeItemStateCriteria CreateRequestedDefaults(ParsedItem parsedItem)
    {
        ArgumentNullException.ThrowIfNull(parsedItem);

        return new TradeItemStateCriteria
        {
            Mirrored = parsedItem.IsMirrored ? TradeTriState.Yes : TradeTriState.Any,
            Corrupted = TradeTriState.Any,
            Identified = TradeTriState.Any,
            Fractured = TradeTriState.Any,
        };
    }
}
