namespace PoEnhance.Core.Trade;

public enum SearchComponentProviderResolutionStatus
{
    NotResolved,
    Exact,
    ExactEquivalentSet,
    /// <summary>
    /// One exact Unique source composition projects to multiple Trade filters that are required
    /// conjunctively (AND), not as interchangeable alternatives.
    /// </summary>
    ExactConjunctiveSet,
    Ambiguous,
    NotFound,
    BaseGuaranteed,
    Unsupported,
    Approximate,
}
