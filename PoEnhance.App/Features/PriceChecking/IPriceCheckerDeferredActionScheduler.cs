namespace PoEnhance.App.Features.PriceChecking;

internal interface IPriceCheckerDeferredActionScheduler
{
    void Schedule(Action action);
}
