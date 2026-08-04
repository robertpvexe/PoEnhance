namespace PoEnhance.Core.Trade;

public enum SearchComponentProviderResolutionStatus
{
    NotResolved,
    Exact,
    ExactEquivalentSet,
    Ambiguous,
    NotFound,
    BaseGuaranteed,
    Unsupported,
    Approximate,
}
