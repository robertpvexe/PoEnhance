namespace PoEnhance.App.Features.PriceChecking;

/// <summary>
/// Presentation-only grouping for the Price Checker stats list.
/// Does not affect Trade draft indexes, query serialization, or source ownership.
/// </summary>
public enum PriceCheckerStatsDisplaySection
{
    Derived = 0,
    Implicit = 1,
    Ordinary = 2,
}
