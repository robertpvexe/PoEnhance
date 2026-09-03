namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal enum PathOfExileTradeStatMatchStatus
{
    Exact,
    ExactEquivalentSet,
    /// <summary>
    /// One proven source composition maps to multiple distinct Trade stats that must all be
    /// required together (logical AND), not treated as equivalent alternatives.
    /// </summary>
    ExactConjunctiveSet,
    Ambiguous,
    NotFound,
    InvalidInput,
}
