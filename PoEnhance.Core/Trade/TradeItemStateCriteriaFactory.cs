using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Trade;

/// <summary>
/// Builds requested item-state search defaults. Observed facts on <see cref="ParsedItem"/>
/// remain separate; only positively observed Mirrored, Fractured, and Foulborn become a
/// requested Yes. Absence stays Any (never automatic No).
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
            Fractured = parsedItem.IsFractured ? TradeTriState.Yes : TradeTriState.Any,
            Foulborn = parsedItem.IsFoulborn ? TradeTriState.Yes : TradeTriState.Any,
        };
    }
}
