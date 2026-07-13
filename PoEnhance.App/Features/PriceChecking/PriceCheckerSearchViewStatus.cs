namespace PoEnhance.App.Features.PriceChecking;

public enum PriceCheckerSearchViewStatus
{
    Idle,
    Loading,
    Success,
    ZeroResults,
    ValidationError,
    ProviderOrTransportError,
    Cancelled,
}
